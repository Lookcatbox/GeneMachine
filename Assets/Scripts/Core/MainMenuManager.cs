using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    private bool showLoadWindow = false;
    private bool showSettingsTip = false;
    private SaveSlotInfo[] cachedSlots;
    private float lastRefreshTime = -1f;

    void OnGUI()
    {
        DrawMainButtons();
        DrawLoadWindow();
        DrawSettingsTip();
    }

    void DrawMainButtons()
    {
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

        RefreshSlots(false);

        float width = Mathf.Min(760f, Screen.width * 0.85f);
        float height = Mathf.Min(520f, Screen.height * 0.85f);
        Rect windowRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUI.Box(windowRect, "");
        GUI.Label(new Rect(windowRect.x + 16f, windowRect.y + 10f, windowRect.width - 64f, 24f), "读取存档");

        Rect closeRect = new Rect(windowRect.xMax - 34f, windowRect.y + 8f, 24f, 24f);
        if (GUI.Button(closeRect, "X"))
        {
            showLoadWindow = false;
            return;
        }

        float padding = 14f;
        float rowHeight = 86f;
        float rowSpacing = 8f;
        float y = windowRect.y + 44f;
        int slotCount = Mathf.Max(1, SimulationConfig.SaveSlotCount);

        for (int i = 0; i < slotCount; i++)
        {
            if (y + rowHeight > windowRect.yMax - padding)
                break;

            Rect rowRect = new Rect(windowRect.x + padding, y, windowRect.width - padding * 2f, rowHeight);
            GUI.Box(rowRect, "");

            SaveSlotInfo info = cachedSlots != null && i < cachedSlots.Length ? cachedSlots[i] : null;
            if (info != null && info.HasData)
            {
                if (GUI.Button(rowRect, ""))
                {
                    SaveSystem.SetPendingLoadSlot(i);
                    SceneManager.LoadScene(SimulationConfig.GameSceneName);
                    return;
                }

                float thumbSize = rowHeight - 16f;
                Rect thumbRect = new Rect(rowRect.x + 8f, rowRect.y + 8f, thumbSize, thumbSize);
                if (info.Screenshot != null)
                    GUI.DrawTexture(thumbRect, info.Screenshot, ScaleMode.ScaleToFit);

                float textX = thumbRect.xMax + 12f;
                Rect timeRect = new Rect(textX, rowRect.y + 10f, rowRect.width - textX - 10f, 24f);
                Rect playRect = new Rect(textX, rowRect.y + 36f, rowRect.width - textX - 10f, 24f);
                string timeText = string.Format("时间: {0}", info.SavedAt);
                string playText = string.Format("时长: {0}", SaveSystem.FormatPlayTime(info.PlaySeconds));
                GUI.Label(timeRect, timeText);
                GUI.Label(playRect, playText);
            }

            y += rowHeight + rowSpacing;
        }
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

        cachedSlots = SaveSystem.LoadAllSlotInfos();
        lastRefreshTime = now;
    }
}
