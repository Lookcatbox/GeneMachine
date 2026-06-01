using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    private bool showLoadWindow = false;
    private bool showSettingsTip = false;
    private bool showDeleteConfirm = false;
    private int pendingDeleteSlot = -1;
    private SaveSlotInfo[] cachedSlots;
    private float lastRefreshTime = -1f;
    private Rect lastLoadWindowRect;
    private Rect lastModalBlockerRect;
    private Rect lastDeleteConfirmRect;
    private GUIStyle loadModalLabelStyle;

    private const int DeleteConfirmModalId = 92001;

    void OnGUI()
    {
        DrawMainButtons();
        DrawLoadWindow();
        if (showDeleteConfirm)
            DrawLoadDimOverlay();
        DrawDeleteConfirmModal();
        DrawSettingsTip();
    }

    void OnDestroy()
    {
        SaveSystem.ReleaseSlotTextures(cachedSlots);
        cachedSlots = null;
    }

    void DrawMainButtons()
    {
        if (showLoadWindow)
            return;

        float buttonWidth = Mathf.Min(320f, Screen.width * 0.5f);
        float buttonHeight = 54f;
        float spacing = 18f;
        float totalHeight = buttonHeight * 3f + spacing * 2f;
        float x = (Screen.width - buttonWidth) * 0.5f;
        float y = (Screen.height - totalHeight) * 0.5f;

        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "新游戏"))
        {
            SaveSystem.ClearPendingLoadSlot();
            SceneManager.LoadScene(SimulationConfig.GameSceneName);
        }
        y += buttonHeight + spacing;

        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "读取游戏"))
        {
            showLoadWindow = true;
            RefreshSlots(true);
        }
        y += buttonHeight + spacing;

        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "设置"))
        {
            showSettingsTip = !showSettingsTip;
        }
    }

    void DrawLoadWindow()
    {
        if (!showLoadWindow)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (showDeleteConfirm)
            {
                showDeleteConfirm = false;
                pendingDeleteSlot = -1;
            }
            else
            {
                showLoadWindow = false;
            }
        }

        RefreshSlots(false);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 16;
        labelStyle.normal.textColor = Color.white;

        GUIStyle shadowStyle = new GUIStyle(labelStyle);
        shadowStyle.normal.textColor = Color.black;

        float width = Mathf.Min(760f, Screen.width * 0.85f);
        float height = Mathf.Min(520f, Screen.height * 0.85f);
        Rect windowRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
        lastLoadWindowRect = windowRect;

        GUI.Box(windowRect, "");

        Rect titleRect = new Rect(windowRect.x + 16f, windowRect.y + 10f, windowRect.width - 64f, 24f);
        GUI.Label(new Rect(titleRect.x + 1f, titleRect.y + 1f, titleRect.width, titleRect.height), "读取存档", shadowStyle);
        GUI.Label(titleRect, "读取存档", labelStyle);

        Rect closeRect = new Rect(windowRect.xMax - 34f, windowRect.y + 8f, 24f, 24f);
        if (!showDeleteConfirm && GUI.Button(closeRect, "X"))
        {
            showLoadWindow = false;
            return;
        }

        float padding = 14f;
        float rowHeight = 86f;
        float rowSpacing = 8f;
        float deleteButtonWidth = 56f;
        float y = windowRect.y + 44f;
        int slotCount = Mathf.Max(1, SimulationConfig.SaveSlotCount);

        for (int i = 0; i < slotCount; i++)
        {
            if (y + rowHeight > windowRect.yMax - padding)
                break;

            Rect rowRect = new Rect(windowRect.x + padding, y, windowRect.width - padding * 2f, rowHeight);
            GUI.Box(rowRect, "");

            SaveSlotInfo info = cachedSlots != null && i < cachedSlots.Length ? cachedSlots[i] : null;
            bool hasData = info != null && info.HasData;

            if (hasData)
            {
                Rect loadRect = new Rect(rowRect.x, rowRect.y, rowRect.width - deleteButtonWidth - 4f, rowRect.height);
                float thumbSize = rowHeight - 16f;
                Rect thumbRect = new Rect(rowRect.x + 8f, rowRect.y + 8f, thumbSize, thumbSize);
                if (info.Screenshot != null)
                    GUI.DrawTexture(thumbRect, info.Screenshot, ScaleMode.ScaleToFit);

                float textX = thumbRect.xMax + 12f;
                float textWidth = loadRect.xMax - textX - 8f;
                Rect slotLabelRect = new Rect(textX, rowRect.y + 6f, textWidth, 18f);
                Rect timeRect = new Rect(textX, rowRect.y + 24f, textWidth, 22f);
                Rect playRect = new Rect(textX, rowRect.y + 46f, textWidth, 22f);
                string slotLabel = string.Format("槽位 {0}", i + 1);
                string savedAt = string.IsNullOrEmpty(info.SavedAt) ? "--" : info.SavedAt;
                string timeText = string.Format("时间: {0}", savedAt);
                string playText = string.Format("时长: {0}", SaveSystem.FormatPlayTime(info.PlaySeconds));
                GUI.Label(new Rect(slotLabelRect.x + 1f, slotLabelRect.y + 1f, slotLabelRect.width, slotLabelRect.height), slotLabel, shadowStyle);
                GUI.Label(slotLabelRect, slotLabel, labelStyle);
                GUI.Label(new Rect(timeRect.x + 1f, timeRect.y + 1f, timeRect.width, timeRect.height), timeText, shadowStyle);
                GUI.Label(timeRect, timeText, labelStyle);
                GUI.Label(new Rect(playRect.x + 1f, playRect.y + 1f, playRect.width, playRect.height), playText, shadowStyle);
                GUI.Label(playRect, playText, labelStyle);

                Rect deleteRect = new Rect(rowRect.xMax - deleteButtonWidth - 6f, rowRect.y + (rowHeight - 28f) * 0.5f, deleteButtonWidth, 28f);
                if (!showDeleteConfirm && GUI.Button(deleteRect, "删除"))
                {
                    pendingDeleteSlot = i;
                    showDeleteConfirm = true;
                }

                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 1f, 1f, 0.01f);
                if (!showDeleteConfirm && GUI.Button(loadRect, GUIContent.none))
                {
                    SaveSystem.SetPendingLoadSlot(i);
                    SceneManager.LoadScene(SimulationConfig.GameSceneName);
                    return;
                }
                GUI.backgroundColor = prevBg;
            }
            else
            {
                string emptyText = string.Format("槽位 {0}  ·  空槽位", i + 1);
                Rect emptyRect = new Rect(rowRect.x + 12f, rowRect.y + 8f, rowRect.width - 24f, rowHeight - 16f);
                GUI.Label(new Rect(emptyRect.x + 1f, emptyRect.y + 1f, emptyRect.width, emptyRect.height), emptyText, shadowStyle);
                GUI.Label(emptyRect, emptyText, labelStyle);
            }

            y += rowHeight + rowSpacing;
        }
    }

    void DrawLoadDimOverlay()
    {
        lastModalBlockerRect = new Rect(0f, 0f, Screen.width, Screen.height);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, SimulationConfig.SaveModalOverlayAlpha);
        GUI.Box(lastModalBlockerRect, GUIContent.none);
        GUI.color = previousColor;
    }

    void DrawDeleteConfirmModal()
    {
        if (!showDeleteConfirm)
            return;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 16;
        labelStyle.normal.textColor = Color.white;
        loadModalLabelStyle = labelStyle;

        float width = 360f;
        float height = 130f;
        Rect rect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        int prevDepth = GUI.depth;
        GUI.depth = 1000;
        lastDeleteConfirmRect = GUI.ModalWindow(DeleteConfirmModalId, rect, DrawDeleteConfirmWindow, "删除存档");
        GUI.depth = prevDepth;
    }

    void DrawDeleteConfirmWindow(int windowId)
    {
        GUIStyle labelStyle = loadModalLabelStyle ?? GUI.skin.label;
        string message = string.Format("确定删除槽位 {0} 的存档？", pendingDeleteSlot + 1);
        GUILayout.Label(message, labelStyle);
        GUILayout.Space(12f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("确定", GUILayout.Height(30f)))
        {
            int slot = pendingDeleteSlot;
            showDeleteConfirm = false;
            pendingDeleteSlot = -1;
            SaveSystem.DeleteSlot(slot);
            RefreshSlots(true);
        }
        if (GUILayout.Button("取消", GUILayout.Height(30f)))
        {
            showDeleteConfirm = false;
            pendingDeleteSlot = -1;
        }
        GUILayout.EndHorizontal();
    }

    void DrawSettingsTip()
    {
        if (!showSettingsTip)
            return;

        float width = 280f;
        float height = 60f;
        Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 24f, width, height);
        GUI.Box(rect, "设置暂未实现");
    }

    void RefreshSlots(bool force)
    {
        float now = Time.realtimeSinceStartup;
        if (!force && now - lastRefreshTime < 0.5f)
            return;

        SaveSystem.ReleaseSlotTextures(cachedSlots);
        cachedSlots = SaveSystem.LoadAllSlotInfos();
        lastRefreshTime = now;
    }
}
