// MainManager.cs - Unity主管理器，负责启动模拟和UI显示
using UnityEngine;

public class MainManager : MonoBehaviour
{
    [Header("模拟设置")]
    [Range(1, 10)] public int simulationSpeed = 1;  // 1x=1步/秒, 10x=10步/秒

    [Header("渲染设置")]
    [SerializeField] private SimulationRenderSettingsData renderSettings = new SimulationRenderSettingsData();
    [SerializeField, HideInInspector] private int renderSettingsSourceHash;

    [Header("玩家操作面板")]
    [SerializeField] private bool playerPanelExpanded = true;

    private const int MaxPlayerPanelTabs = 100;
    private const float PlayerPanelWidthRatio = 1f / 3f;
    private const float PlayerPanelMinWidth = 360f;
    private const float PanelEdgeMargin = 16f;
    private const float PanelToggleWidth = 44f;
    private const float PanelToggleHeight = 88f;

    private readonly PlayerPanelTabPage[] playerPanelTabs = new PlayerPanelTabPage[MaxPlayerPanelTabs];
    private int playerPanelTabCount = 0;
    private int activePlayerPanelTabIndex = 0;

    private bool showUI = true;
    private bool draggingLightHandle = false;
    private bool hasHoveredEnvironmentCell = false;
    private int hoveredEnvironmentX = -1;
    private int hoveredEnvironmentY = -1;
    private bool hasLockedEnvironmentCell = false;
    private int lockedEnvironmentX = -1;
    private int lockedEnvironmentY = -1;
    private Texture2D playerPanelToggleTexture;

    void Reset()
    {
        InitializePlayerPanelTabs();
        EnsurePlayerPanelToggleTexture();
        SyncRenderSettingsFromSimulationConfig(true);
        ApplyRenderSettings();
    }

    void Awake()
    {
        InitializePlayerPanelTabs();
        EnsurePlayerPanelToggleTexture();
        ApplyRenderSettings();
    }

    void OnValidate()
    {
        InitializePlayerPanelTabs();
        EnsurePlayerPanelToggleTexture();
        ApplyRenderSettings();
    }

    void Start()
    {
        // 启动独立计算线程
        SimulationCore.SetSpeedMultiplier(simulationSpeed);
        SimulationCore.StartCalculationThread();
        Debug.Log("基因自动机已启动！计算线程独立运行中...");
    }

    void Update()
    {
        UpdateEnvironmentSelectionState();

        // 快捷键
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (SimulationCore.IsPaused())
                SimulationCore.ResumeSimulation();
            else
                SimulationCore.PauseSimulation();
        }

        // 切换UI显示
        if (Input.GetKeyDown(KeyCode.F1))
        {
            showUI = !showUI;
        }
    }

    void OnDestroy()
    {
        if (playerPanelToggleTexture != null)
        {
            if (Application.isPlaying)
                Destroy(playerPanelToggleTexture);
            else
                DestroyImmediate(playerPanelToggleTexture);
        }

        SimulationCore.StopCalculation();
        Debug.Log("模拟已停止");
    }

    void InitializePlayerPanelTabs()
    {
        playerPanelTabCount = 0;
        RegisterPlayerPanelTab(new EnvironmentPlayerPanelTab());
        RegisterPlayerPanelTab(new GenePlayerPanelTab());
        RegisterPlayerPanelTab(new ResearchPlayerPanelTab());
        RegisterPlayerPanelTab(new PopulationPlayerPanelTab());
        activePlayerPanelTabIndex = Mathf.Clamp(activePlayerPanelTabIndex, 0, Mathf.Max(0, playerPanelTabCount - 1));
    }

    void RegisterPlayerPanelTab(PlayerPanelTabPage tabPage)
    {
        if (tabPage == null || playerPanelTabCount >= playerPanelTabs.Length)
            return;

        playerPanelTabs[playerPanelTabCount++] = tabPage;
    }

    void EnsurePlayerPanelToggleTexture()
    {
        if (playerPanelToggleTexture != null)
            return;

        int width = 64;
        int height = 128;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill = new Color(0.14f, 0.16f, 0.22f, 0.92f);
        Color outline = new Color(0.72f, 0.78f, 0.92f, 0.95f);

        for (int y = 0; y < height; y++)
        {
            float t = y / Mathf.Max(1f, height - 1f);
            int left = Mathf.RoundToInt(Mathf.Lerp(width * 0.34f, width * 0.10f, t));

            for (int x = 0; x < width; x++)
            {
                bool inside = x >= left && x < width - 1;
                if (!inside)
                {
                    texture.SetPixel(x, y, clear);
                    continue;
                }

                bool edge = x == left || x == width - 2 || y == 0 || y == height - 1;
                texture.SetPixel(x, y, edge ? outline : fill);
            }
        }

        texture.Apply();
        playerPanelToggleTexture = texture;
    }

    void ApplyRenderSettings()
    {
        SyncRenderSettingsFromSimulationConfig(false);
        renderSettings.ApplyToSimulationConfig();
        renderSettingsSourceHash = renderSettings.ComputeHash();
    }

    void SyncRenderSettingsFromSimulationConfig(bool force)
    {
        if (renderSettings == null)
            renderSettings = new SimulationRenderSettingsData();

        int currentRenderHash = renderSettings.ComputeHash();
        if (force || renderSettingsSourceHash == 0 || currentRenderHash == renderSettingsSourceHash)
        {
            renderSettings.CopyFromSimulationConfig();
        }

        renderSettingsSourceHash = SimulationRenderSettingsData.ComputeSimulationConfigHash();
    }

    void OnGUI()
    {
        if (!showUI) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        GUIStyle shadowStyle = new GUIStyle(style);
        shadowStyle.normal.textColor = Color.black;

        float y = 10;
        float lineH = 22;

        string pauseText = SimulationCore.IsPaused() ? " [已暂停]" : "";

        string[] lines = new string[]
        {
            string.Format("基因自动机{0}", pauseText),
            string.Format("计算频率: {0} 步/秒 | 渲染帧率: {1:F0} FPS", SimulationCore.stepsPerSecond, 1f / Time.deltaTime),
            string.Format("存活细胞: {0} | 总步数: {1}", SimulationCore.aliveCellCount, SimulationCore.totalSteps),
            string.Format("模拟速度: {0}x", SimulationCore.speedMultiplier),
            "",
            "操作: 空格=暂停/继续  WASD=移动  滚轮=缩放  右键拖拽=平移  左键环境格=锁定  F1=隐藏UI"
        };

        for (int i = 0; i < lines.Length; i++)
        {
            // 文字阴影
            GUI.Label(new Rect(11, y + 1, 600, lineH), lines[i], shadowStyle);
            GUI.Label(new Rect(10, y, 600, lineH), lines[i], style);
            y += lineH;
        }

        DrawSpeedControl(style, shadowStyle);
        DrawPlayerOperationPanel(style, shadowStyle);
        DrawViewModeUI();
        DrawLightControlUI(style, shadowStyle);
    }

    void DrawSpeedControl(GUIStyle style, GUIStyle shadowStyle)
    {
        Rect panelRect = GetSpeedPanelRect();
        float panelWidth = panelRect.width;
        float panelHeight = panelRect.height;
        float x = panelRect.x;
        float y = panelRect.y;

        GUI.Box(new Rect(x, y, panelWidth, panelHeight), "");

        GUI.Label(new Rect(x + 13, y + 11, panelWidth - 20, 22), "游戏速度", shadowStyle);
        GUI.Label(new Rect(x + 12, y + 10, panelWidth - 20, 22), "游戏速度", style);

        int currentSpeed = SimulationCore.speedMultiplier;
        float sliderValue = GUI.HorizontalSlider(new Rect(x + 12, y + 42, panelWidth - 24, 20), currentSpeed, 1f, 10f);
        int newSpeed = Mathf.RoundToInt(sliderValue);
        if (newSpeed != currentSpeed)
        {
            SimulationCore.SetSpeedMultiplier(newSpeed);
        }

        string speedText = string.Format("{0}x  ({1:F1}秒/步)", SimulationCore.speedMultiplier, 1f / SimulationCore.speedMultiplier);
        GUI.Label(new Rect(x + 13, y + 60, panelWidth - 20, 22), speedText, shadowStyle);
        GUI.Label(new Rect(x + 12, y + 59, panelWidth - 20, 22), speedText, style);
    }

    void DrawPlayerOperationPanel(GUIStyle style, GUIStyle shadowStyle)
    {
        DrawPlayerPanelToggle();

        if (!playerPanelExpanded)
            return;

        Rect panelRect = GetPlayerPanelRect();
        GUI.Box(panelRect, "");

        GUI.Label(new Rect(panelRect.x + 19f, panelRect.y + 13f, panelRect.width - 38f, 22f), "玩家操作面板", shadowStyle);
        GUI.Label(new Rect(panelRect.x + 18f, panelRect.y + 12f, panelRect.width - 38f, 22f), "玩家操作面板", style);

        Rect tabStripRect = new Rect(panelRect.x + 14f, panelRect.y + 44f, panelRect.width - 28f, 42f);
        DrawPlayerPanelTabs(tabStripRect);

        Rect contentRect = new Rect(panelRect.x + 14f, tabStripRect.yMax + 8f, panelRect.width - 28f, panelRect.height - tabStripRect.yMax - 20f);
        GUI.Box(contentRect, "");

        PlayerPanelTabPage activeTab = GetActivePlayerPanelTab();
        if (activeTab != null)
            activeTab.Draw(contentRect, style, shadowStyle, BuildPlayerPanelContext());
    }

    void DrawPlayerPanelToggle()
    {
        EnsurePlayerPanelToggleTexture();
        Rect toggleRect = GetPlayerPanelToggleRect();
        GUIStyle toggleStyle = new GUIStyle(GUI.skin.button);
        toggleStyle.fontSize = 22;
        toggleStyle.fontStyle = FontStyle.Bold;
        toggleStyle.alignment = TextAnchor.MiddleCenter;
        toggleStyle.normal.background = playerPanelToggleTexture;
        toggleStyle.hover.background = playerPanelToggleTexture;
        toggleStyle.active.background = playerPanelToggleTexture;
        toggleStyle.focused.background = playerPanelToggleTexture;
        toggleStyle.normal.textColor = Color.white;
        toggleStyle.hover.textColor = Color.white;
        toggleStyle.active.textColor = new Color(0.92f, 0.96f, 1f);

        string label = playerPanelExpanded ? ">" : "<";
        if (GUI.Button(toggleRect, label, toggleStyle))
            playerPanelExpanded = !playerPanelExpanded;
    }

    void DrawPlayerPanelTabs(Rect tabStripRect)
    {
        if (playerPanelTabCount == 0)
            return;

        float spacing = 4f;
        float availableWidth = tabStripRect.width - spacing * (playerPanelTabCount - 1);
        float tabWidth = Mathf.Min(110f, availableWidth / Mathf.Max(1, playerPanelTabCount));
        float x = tabStripRect.x;
        Color originalBackground = GUI.backgroundColor;

        for (int i = 0; i < playerPanelTabCount; i++)
        {
            bool selected = i == activePlayerPanelTabIndex;
            Rect tabRect = new Rect(x, selected ? tabStripRect.y : tabStripRect.y + 4f, tabWidth, selected ? tabStripRect.height : tabStripRect.height - 4f);
            GUI.backgroundColor = selected ? new Color(0.24f, 0.34f, 0.52f) : new Color(0.22f, 0.22f, 0.22f);
            if (GUI.Button(tabRect, playerPanelTabs[i].Title))
                activePlayerPanelTabIndex = i;

            x += tabWidth + spacing;
        }

        GUI.backgroundColor = originalBackground;
    }

    void DrawViewModeUI()
    {
        float btnWidth = 100f;
        float btnHeight = 28f;
        float spacing = 4f;
        float totalHeight = btnHeight * 5 + spacing * 4f + 8f;
        float panelLeft = GetPlayerPanelLeftBoundary();
        float x = panelLeft - btnWidth;
        float y = Screen.height - totalHeight - PanelEdgeMargin;

        string[] labels = { "地形视图", "温度视图", "光照视图", "高度视图" };
        Color origBg = GUI.backgroundColor;

        for (int i = 0; i < labels.Length; i++)
        {
            bool selected = (int)CellRenderer.currentViewMode == i;
            GUI.backgroundColor = selected ? new Color(0.3f, 0.8f, 1f) : new Color(0.5f, 0.5f, 0.5f);
            if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), labels[i]))
            {
                CellRenderer.currentViewMode = (CellRenderer.ViewMode)i;
            }
            y += btnHeight + spacing;
        }

        float toggleY = y + 8f;
        bool normalEnabled = CellRenderer.normalLightingEnabled;
        GUI.backgroundColor = normalEnabled ? new Color(0.25f, 0.9f, 0.65f) : new Color(0.45f, 0.45f, 0.45f);
        if (GUI.Button(new Rect(x, toggleY, btnWidth, btnHeight), "3D"))
        {
            CellRenderer.normalLightingEnabled = !CellRenderer.normalLightingEnabled;
        }

        GUI.backgroundColor = origBg;
    }

    void DrawLightControlUI(GUIStyle style, GUIStyle shadowStyle)
    {
        float panelSize = 168f;
        float padding = 16f;
        float squareSize = 128f;
        float x = 16f;
        float y = Screen.height - panelSize - 16f;
        Rect panelRect = new Rect(x, y, panelSize, panelSize);
        Rect squareRect = new Rect(x + padding, y + 26f, squareSize, squareSize);
        float handleRadius = 13f;

        GUI.Box(panelRect, "");
        GUI.Label(new Rect(x + 11, y + 7, panelSize - 22, 20), "太阳方向", shadowStyle);
        GUI.Label(new Rect(x + 10, y + 6, panelSize - 22, 20), "太阳方向", style);

        DrawLightControlGuides(squareRect);

        Event currentEvent = Event.current;
        Vector3 lightDirection = CellRenderer.GetTerrainLightDirection();
        float maxHandleOffset = squareRect.width * 0.5f - handleRadius - 4f;
        Vector2 handleOffset = new Vector2(lightDirection.x, -lightDirection.y) * maxHandleOffset;
        Rect handleRect = new Rect(
            squareRect.center.x + handleOffset.x - handleRadius,
            squareRect.center.y + handleOffset.y - handleRadius,
            handleRadius * 2f,
            handleRadius * 2f);

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 &&
            (squareRect.Contains(currentEvent.mousePosition) || handleRect.Contains(currentEvent.mousePosition)))
        {
            draggingLightHandle = true;
            UpdateTerrainLightFromMouse(currentEvent.mousePosition, squareRect, maxHandleOffset);
            currentEvent.Use();
        }
        else if (draggingLightHandle && currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
        {
            UpdateTerrainLightFromMouse(currentEvent.mousePosition, squareRect, maxHandleOffset);
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
        {
            draggingLightHandle = false;
        }

        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 0.88f, 0.35f, 0.98f);
        GUI.Box(handleRect, "☼");
        GUI.color = previousColor;
    }

    void DrawLightControlGuides(Rect squareRect)
    {
        Color previousColor = GUI.color;

        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
        GUI.Box(squareRect, "");

        GUI.color = new Color(1f, 1f, 1f, 0.16f);
        GUI.Box(new Rect(squareRect.x, squareRect.center.y - 1f, squareRect.width, 2f), "");
        GUI.Box(new Rect(squareRect.center.x - 1f, squareRect.y, 2f, squareRect.height), "");

        GUI.color = new Color(1f, 1f, 1f, 0.08f);
        float quarterX = squareRect.width * 0.25f;
        float quarterY = squareRect.height * 0.25f;
        GUI.Box(new Rect(squareRect.x + quarterX - 1f, squareRect.y, 2f, squareRect.height), "");
        GUI.Box(new Rect(squareRect.x + quarterX * 3f - 1f, squareRect.y, 2f, squareRect.height), "");
        GUI.Box(new Rect(squareRect.x, squareRect.y + quarterY - 1f, squareRect.width, 2f), "");
        GUI.Box(new Rect(squareRect.x, squareRect.y + quarterY * 3f - 1f, squareRect.width, 2f), "");

        GUI.color = previousColor;
    }

    void UpdateEnvironmentSelectionState()
    {
        if (SimulationCore.EnvirData == null)
        {
            hasHoveredEnvironmentCell = false;
            return;
        }

        Vector2 guiMouse = GetGuiMousePosition();
        if (showUI && IsPointOverInteractiveUI(guiMouse))
        {
            hasHoveredEnvironmentCell = false;
            return;
        }

        int gridX;
        int gridY;
        if (!TryGetEnvironmentCellUnderMouse(out gridX, out gridY))
        {
            hasHoveredEnvironmentCell = false;
            return;
        }

        hasHoveredEnvironmentCell = true;
        hoveredEnvironmentX = gridX;
        hoveredEnvironmentY = gridY;

        if (Input.GetMouseButtonDown(0))
            ToggleLockedEnvironmentCell(gridX, gridY);
    }

    void ToggleLockedEnvironmentCell(int gridX, int gridY)
    {
        if (hasLockedEnvironmentCell && lockedEnvironmentX == gridX && lockedEnvironmentY == gridY)
        {
            hasLockedEnvironmentCell = false;
            lockedEnvironmentX = -1;
            lockedEnvironmentY = -1;
            return;
        }

        hasLockedEnvironmentCell = true;
        lockedEnvironmentX = gridX;
        lockedEnvironmentY = gridY;
    }

    PlayerPanelContext BuildPlayerPanelContext()
    {
        PlayerPanelContext context = new PlayerPanelContext();
        context.HasHoveredEnvironmentCell = hasHoveredEnvironmentCell;
        context.HoveredEnvironmentX = hoveredEnvironmentX;
        context.HoveredEnvironmentY = hoveredEnvironmentY;
        context.HasLockedEnvironmentCell = hasLockedEnvironmentCell;
        context.LockedEnvironmentX = lockedEnvironmentX;
        context.LockedEnvironmentY = lockedEnvironmentY;

        Envir env;
        if (hasLockedEnvironmentCell && TryGetEnvironmentCell(lockedEnvironmentX, lockedEnvironmentY, out env))
        {
            context.HasDisplayEnvironmentCell = true;
            context.DisplayEnvironmentX = lockedEnvironmentX;
            context.DisplayEnvironmentY = lockedEnvironmentY;
            context.DisplayEnvironment = env;
            return context;
        }

        if (hasHoveredEnvironmentCell && TryGetEnvironmentCell(hoveredEnvironmentX, hoveredEnvironmentY, out env))
        {
            context.HasDisplayEnvironmentCell = true;
            context.DisplayEnvironmentX = hoveredEnvironmentX;
            context.DisplayEnvironmentY = hoveredEnvironmentY;
            context.DisplayEnvironment = env;
        }

        return context;
    }

    PlayerPanelTabPage GetActivePlayerPanelTab()
    {
        if (playerPanelTabCount == 0)
            return null;

        activePlayerPanelTabIndex = Mathf.Clamp(activePlayerPanelTabIndex, 0, playerPanelTabCount - 1);
        return playerPanelTabs[activePlayerPanelTabIndex];
    }

    bool TryGetEnvironmentCell(int gridX, int gridY, out Envir env)
    {
        env = null;
        if (SimulationCore.EnvirData == null)
            return false;

        if (gridX < 1 || gridX > SimulationConfig.EnvirSize || gridY < 1 || gridY > SimulationConfig.EnvirSize)
            return false;

        env = SimulationCore.EnvirData[gridX, gridY];
        return env != null;
    }

    bool TryGetEnvironmentCellUnderMouse(out int gridX, out int gridY)
    {
        gridX = -1;
        gridY = -1;

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
            return false;

        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        if (worldPosition.x < 0f || worldPosition.y < 0f)
            return false;

        float ppe = SimulationConfig.PixelPerEnvir;
        gridX = Mathf.FloorToInt(worldPosition.x / ppe) + 1;
        gridY = Mathf.FloorToInt(worldPosition.y / ppe) + 1;

        return gridX >= 1 && gridX <= SimulationConfig.EnvirSize && gridY >= 1 && gridY <= SimulationConfig.EnvirSize;
    }

    Vector2 GetGuiMousePosition()
    {
        return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
    }

    bool IsPointOverInteractiveUI(Vector2 guiPosition)
    {
        if (GetSpeedPanelRect().Contains(guiPosition))
            return true;

        if (GetViewModeButtonColumnRect().Contains(guiPosition))
            return true;

        if (GetLightControlPanelRect().Contains(guiPosition))
            return true;

        if (GetPlayerPanelToggleRect().Contains(guiPosition))
            return true;

        if (playerPanelExpanded && GetPlayerPanelRect().Contains(guiPosition))
            return true;

        return false;
    }

    Rect GetSpeedPanelRect()
    {
        float panelWidth = 250f;
        float panelHeight = 92f;
        float x = Mathf.Max(PanelEdgeMargin, GetPlayerPanelLeftBoundary() - panelWidth - PanelEdgeMargin);
        return new Rect(x, PanelEdgeMargin, panelWidth, panelHeight);
    }

    Rect GetLightControlPanelRect()
    {
        float panelSize = 168f;
        return new Rect(16f, Screen.height - panelSize - 16f, panelSize, panelSize);
    }

    Rect GetViewModeButtonColumnRect()
    {
        float btnWidth = 100f;
        float btnHeight = 28f;
        float spacing = 4f;
        float totalHeight = btnHeight * 5 + spacing * 4f + 8f;
        float x = GetPlayerPanelLeftBoundary() - btnWidth;
        float y = Screen.height - totalHeight - PanelEdgeMargin;
        return new Rect(x, y, btnWidth, totalHeight);
    }

    Rect GetPlayerPanelRect()
    {
        float width = GetPlayerPanelWidth();
        float x = playerPanelExpanded ? Screen.width - width : Screen.width;
        return new Rect(x, 0f, width, Screen.height);
    }

    float GetPlayerPanelLeftBoundary()
    {
        return playerPanelExpanded ? Screen.width - GetPlayerPanelWidth() : Screen.width;
    }

    float GetPlayerPanelWidth()
    {
        float desiredWidth = Mathf.Max(PlayerPanelMinWidth, Screen.width * PlayerPanelWidthRatio);
        return Mathf.Min(desiredWidth, Mathf.Max(PlayerPanelMinWidth, Screen.width - PanelToggleWidth - PanelEdgeMargin));
    }

    Rect GetPlayerPanelToggleRect()
    {
        float x = GetPlayerPanelLeftBoundary() - PanelToggleWidth;
        float y = (Screen.height - PanelToggleHeight) * 0.5f;
        return new Rect(x, y, PanelToggleWidth, PanelToggleHeight);
    }

    void UpdateTerrainLightFromMouse(Vector2 mousePosition, Rect squareRect, float maxHandleOffset)
    {
        Vector2 local = mousePosition - squareRect.center;
        Vector2 normalized = new Vector2(local.x / Mathf.Max(1f, maxHandleOffset), -local.y / Mathf.Max(1f, maxHandleOffset));

        if (normalized.sqrMagnitude > 0.95f * 0.95f)
            normalized = normalized.normalized * 0.95f;

        float radial = Mathf.Clamp01(normalized.magnitude);
        float z = Mathf.Lerp(0.16f, 0.62f, 1f - radial);
        CellRenderer.SetTerrainLightDirection(new Vector3(normalized.x, normalized.y, z));
    }
}
