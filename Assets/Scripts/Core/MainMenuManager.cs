// MainMenuManager.cs - 主菜单场景：新游戏、读档、设置与槽位管理
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>主菜单 IMGUI：场景跳转、存档列表与删除确认。</summary>
public class MainMenuManager : MonoBehaviour
{
    private bool showLoadWindow = false;
    private bool showSettingsWindow = false;
    private bool showDeleteConfirm = false;
    private int pendingDeleteSlot = -1;
    private SaveSlotInfo[] cachedSlots;
    private float lastRefreshTime = -1f;
    private Rect lastLoadWindowRect;
    private Rect lastModalBlockerRect;
    private Rect lastDeleteConfirmRect;
    void OnGUI()
    {
        DrawMainButtons();
        DrawLoadWindow();
        DrawSettingsWindow();
        if (showDeleteConfirm)
            DrawLoadDimOverlay();
        DrawDeleteConfirmModal();
    }

    void OnDestroy()
    {
        SaveSystem.ReleaseSlotTextures(cachedSlots);
        cachedSlots = null;
    }

    void DrawMainButtons()
    {
        if (showLoadWindow || showSettingsWindow)
            return;

        float buttonWidth = Mathf.Min(320f, Screen.width * 0.5f);
        float buttonHeight = 54f;
        float spacing = 18f;
        float titleHeight = 118f;
        float totalHeight = titleHeight + buttonHeight * 3f + spacing * 3f;
        float x = (Screen.width - buttonWidth) * 0.5f;
        float y = (Screen.height - totalHeight) * 0.5f;

        Rect titleRect = new Rect(x - 54f, y, buttonWidth + 108f, titleHeight);
        GeneMachineGuiTheme.DrawPanel(titleRect);
        GeneMachineGuiTheme.DrawGrid(titleRect, 24f);
        GeneMachineGuiTheme.DrawGeneMark(new Rect(titleRect.x + 18f, titleRect.y + 18f, 70f, 70f), 0.92f);

        GUIStyle titleStyle = GeneMachineGuiTheme.BuildTitleStyle(34, TextAnchor.MiddleCenter);
        GUIStyle titleShadowStyle = GeneMachineGuiTheme.BuildShadowStyle(titleStyle);
        GUIStyle subtitleStyle = GeneMachineGuiTheme.BuildTitleStyle(14, TextAnchor.MiddleCenter);
        subtitleStyle.normal.textColor = GeneMachineGuiTheme.MutedText;
        GUIStyle subtitleShadowStyle = GeneMachineGuiTheme.BuildShadowStyle(subtitleStyle);

        GeneMachineGuiTheme.DrawText(new Rect(titleRect.x + 92f, titleRect.y + 22f, titleRect.width - 116f, 40f), "GeneMachine", titleStyle, titleShadowStyle);
        GeneMachineGuiTheme.DrawText(new Rect(titleRect.x + 92f, titleRect.y + 68f, titleRect.width - 116f, 22f), "基因自动机 · 细胞演化模拟", subtitleStyle, subtitleShadowStyle);

        y += titleHeight + spacing;

        if (GeneMachineGuiTheme.DrawButton(new Rect(x, y, buttonWidth, buttonHeight), "新游戏", true))
        {
            SaveSystem.ClearPendingLoadSlot();
            SceneManager.LoadScene(SimulationConfig.GameSceneName);
        }
        y += buttonHeight + spacing;

        if (GeneMachineGuiTheme.DrawButton(new Rect(x, y, buttonWidth, buttonHeight), "读取游戏", false))
        {
            showLoadWindow = true;
            RefreshSlots(true);
        }
        y += buttonHeight + spacing;

        if (GeneMachineGuiTheme.DrawButton(new Rect(x, y, buttonWidth, buttonHeight), "设置", false))
        {
            showSettingsWindow = true;
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

        GUIStyle labelStyle = GeneMachineGuiTheme.BuildLabelStyle(16);
        GUIStyle shadowStyle = GeneMachineGuiTheme.BuildShadowStyle(labelStyle);

        float width = Mathf.Min(760f, Screen.width * 0.85f);
        float height = Mathf.Min(520f, Screen.height * 0.85f);
        Rect windowRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
        lastLoadWindowRect = windowRect;

        GeneMachineGuiTheme.DrawPanel(windowRect);
        GeneMachineGuiTheme.DrawGrid(windowRect, 28f);

        Rect titleRect = new Rect(windowRect.x + 16f, windowRect.y + 10f, windowRect.width - 64f, 24f);
        GeneMachineGuiTheme.DrawText(titleRect, "读取存档", labelStyle, shadowStyle);

        Rect closeRect = new Rect(windowRect.xMax - 34f, windowRect.y + 8f, 24f, 24f);
        if (!showDeleteConfirm && GeneMachineGuiTheme.DrawCloseButton(closeRect))
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
            SaveSlotInfo info = cachedSlots != null && i < cachedSlots.Length ? cachedSlots[i] : null;
            bool hasData = info != null && info.HasData;
            GeneMachineGuiTheme.DrawCard(rowRect, hasData);

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
                GeneMachineGuiTheme.DrawText(slotLabelRect, slotLabel, labelStyle, shadowStyle);
                GeneMachineGuiTheme.DrawText(timeRect, timeText, labelStyle, shadowStyle);
                GeneMachineGuiTheme.DrawText(playRect, playText, labelStyle, shadowStyle);

                Rect deleteRect = new Rect(rowRect.xMax - deleteButtonWidth - 6f, rowRect.y + (rowHeight - 28f) * 0.5f, deleteButtonWidth, 28f);
                if (!showDeleteConfirm && GeneMachineGuiTheme.DrawButton(deleteRect, "删除", false))
                {
                    pendingDeleteSlot = i;
                    showDeleteConfirm = true;
                }

                if (!showDeleteConfirm && GeneMachineGuiTheme.DrawTransparentClick(loadRect))
                {
                    SaveSystem.SetPendingLoadSlot(i);
                    SceneManager.LoadScene(SimulationConfig.GameSceneName);
                    return;
                }
            }
            else
            {
                string emptyText = string.Format("槽位 {0}  ·  空槽位", i + 1);
                Rect emptyRect = new Rect(rowRect.x + 12f, rowRect.y + 8f, rowRect.width - 24f, rowHeight - 16f);
                GeneMachineGuiTheme.DrawText(emptyRect, emptyText, labelStyle, shadowStyle);
            }

            y += rowHeight + rowSpacing;
        }
    }

    void DrawLoadDimOverlay()
    {
        lastModalBlockerRect = new Rect(0f, 0f, Screen.width, Screen.height);
        GeneMachineGuiTheme.DrawBox(lastModalBlockerRect, new Color(0f, 0f, 0f, SimulationConfig.SaveModalOverlayAlpha), Color.clear);
    }

    void DrawDeleteConfirmModal()
    {
        if (!showDeleteConfirm)
            return;

        GUIStyle labelStyle = GeneMachineGuiTheme.BuildLabelStyle(16);
        GUIStyle shadowStyle = GeneMachineGuiTheme.BuildShadowStyle(labelStyle);

        float width = 360f;
        float height = 148f;
        Rect rect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        lastDeleteConfirmRect = rect;
        GeneMachineGuiTheme.DrawModalShell(rect, "删除存档", labelStyle, shadowStyle);
        string message = string.Format("确定删除槽位 {0} 的存档？", pendingDeleteSlot + 1);
        GeneMachineGuiTheme.DrawText(new Rect(rect.x + 18f, rect.y + 58f, rect.width - 36f, 24f), message, labelStyle, shadowStyle);

        Rect confirmRect = new Rect(rect.x + 18f, rect.yMax - 44f, (rect.width - 48f) * 0.5f, 28f);
        Rect cancelRect = new Rect(confirmRect.xMax + 12f, confirmRect.y, confirmRect.width, confirmRect.height);
        if (GeneMachineGuiTheme.DrawButton(confirmRect, "确定", true))
        {
            int slot = pendingDeleteSlot;
            showDeleteConfirm = false;
            pendingDeleteSlot = -1;
            SaveSystem.DeleteSlot(slot);
            RefreshSlots(true);
        }
        if (GeneMachineGuiTheme.DrawButton(cancelRect, "取消", false))
        {
            showDeleteConfirm = false;
            pendingDeleteSlot = -1;
        }
    }

    void DrawSettingsWindow()
    {
        if (!showSettingsWindow)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            showSettingsWindow = false;

        GUIStyle labelStyle = GeneMachineGuiTheme.BuildLabelStyle(16);
        GUIStyle shadowStyle = GeneMachineGuiTheme.BuildShadowStyle(labelStyle);
        GUIStyle sectionStyle = GeneMachineGuiTheme.BuildLabelStyle(14);
        sectionStyle.fontStyle = FontStyle.Bold;
        sectionStyle.normal.textColor = GeneMachineGuiTheme.Cyan;
        GUIStyle sectionShadow = GeneMachineGuiTheme.BuildShadowStyle(sectionStyle);

        float width = Mathf.Min(640f, Screen.width * 0.9f);
        float height = 220f;
        Rect windowRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GeneMachineGuiTheme.DrawPanel(windowRect);
        GeneMachineGuiTheme.DrawGrid(windowRect, 28f);

        Rect titleRect = new Rect(windowRect.x + 16f, windowRect.y + 10f, windowRect.width - 64f, 24f);
        GeneMachineGuiTheme.DrawText(titleRect, "设置", labelStyle, shadowStyle);

        Rect closeRect = new Rect(windowRect.xMax - 34f, windowRect.y + 8f, 24f, 24f);
        if (GeneMachineGuiTheme.DrawCloseButton(closeRect))
            showSettingsWindow = false;

        float padding = 18f;
        Rect sectionRect = new Rect(windowRect.x + padding, windowRect.y + 44f, windowRect.width - padding * 2f, 22f);
        GeneMachineGuiTheme.DrawText(sectionRect, "画面设置", sectionStyle, sectionShadow);

        Rect rowLabelRect = new Rect(windowRect.x + padding, sectionRect.yMax + 10f, 120f, 22f);
        GeneMachineGuiTheme.DrawText(rowLabelRect, "帧率上限", labelStyle, shadowStyle);

        int[] presets = SimulationConfig.TargetFpsCapPresets;
        int optionCount = presets.Length + 1;
        float chipGap = 8f;
        float chipHeight = 32f;
        float chipsX = rowLabelRect.xMax + 8f;
        float chipsWidth = windowRect.xMax - padding - chipsX;
        float chipWidth = Mathf.Max(44f, (chipsWidth - chipGap * (optionCount - 1)) / optionCount);
        float chipY = sectionRect.yMax + 8f;
        int currentCap = DisplaySettings.CurrentFpsCap;

        for (int i = 0; i < presets.Length; i++)
        {
            int cap = presets[i];
            Rect chipRect = new Rect(chipsX + i * (chipWidth + chipGap), chipY, chipWidth, chipHeight);
            if (GeneMachineGuiTheme.DrawButton(chipRect, cap.ToString(), cap == currentCap))
                DisplaySettings.SetFpsCap(cap);
        }

        int infinityCap = SimulationConfig.TargetFpsCapInfinity;
        Rect infinityRect = new Rect(chipsX + presets.Length * (chipWidth + chipGap), chipY, chipWidth, chipHeight);
        if (GeneMachineGuiTheme.DrawButton(infinityRect, SimulationConfig.TargetFpsCapInfinityLabel, currentCap == infinityCap))
            DisplaySettings.SetFpsCap(infinityCap);
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
