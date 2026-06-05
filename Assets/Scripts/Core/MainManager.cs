// MainManager.cs - Unity主管理器，负责启动模拟和UI显示
using UnityEngine;
using System.Collections;

/// <summary>游戏场景入口：启动 SimulationCore、绘制玩家面板/存档 UI、同步渲染与装置放置交互。</summary>
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
    private Texture2D rangeOverlayTexture;
    private bool hasSelectedDevice = false;
    private DeviceInstance selectedDevice;

    private bool showSaveWindow = false;
    private bool showSaveConfirm = false;
    private bool showDeleteConfirm = false;
    private bool pausedBeforeSave = false;
    private int pendingSaveSlot = -1;
    private int pendingDeleteSlot = -1;
    private SaveSlotInfo[] cachedSaveSlots;
    private float lastSaveSlotsRefreshTime = -1f;
    private Rect lastSaveWindowRect;
    private Rect lastSaveConfirmRect;
    private Rect lastSaveModalBlockerRect;
    private bool saveScreenshotPending = false;

    private const int SaveConfirmModalId = 91001;
    private const int DeleteConfirmModalId = 91002;
    private GUIStyle saveModalLabelStyle;
    private GUIStyle saveModalShadowStyle;

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
        DeviceSystem.Init();
    }

    void OnValidate()
    {
        InitializePlayerPanelTabs();
        EnsurePlayerPanelToggleTexture();
        ApplyRenderSettings();
    }

    void Start()
    {
        CellRenderer.geneFilterBaseId = 0;
        // 启动独立计算线程
        SimulationCore.SetSpeedMultiplier(simulationSpeed);
        if (SaveSystem.HasPendingLoadSlot)
        {
            int slot = SaveSystem.ConsumePendingLoadSlot();
            bool loaded = SaveSystem.LoadSlotIntoSimulation(slot, this);
            if (loaded)
                SimulationCore.StartCalculationThreadFromLoadedWorld();
            else
            {
                DeviceSystem.ResetRuntimeState();
                SimulationCore.StartCalculationThread();
            }
        }
        else
        {
            DeviceSystem.ResetRuntimeState();
            SimulationCore.StartCalculationThread();
        }
        Debug.Log("基因自动机已启动！计算线程独立运行中...");
    }

    void Update()
    {
        UpdateEnvironmentSelectionState();
        HandleDevicePlacementInput();

        if (!SimulationCore.IsPaused())
            SimulationCore.AddPlaySeconds(Time.unscaledDeltaTime);

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

        if (showSaveWindow && Input.GetKeyDown(KeyCode.Escape))
        {
            if (showSaveConfirm || showDeleteConfirm)
            {
                showSaveConfirm = false;
                showDeleteConfirm = false;
                pendingSaveSlot = -1;
                pendingDeleteSlot = -1;
            }
            else
            {
                CloseSaveWindow();
            }
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

        if (rangeOverlayTexture != null)
        {
            if (Application.isPlaying)
                Destroy(rangeOverlayTexture);
            else
                DestroyImmediate(rangeOverlayTexture);
        }

        SaveSystem.ReleaseSlotTextures(cachedSaveSlots);
        cachedSaveSlots = null;

        SimulationCore.StopCalculation();
        Debug.Log("模拟已停止");
    }

    void InitializePlayerPanelTabs()
    {
        playerPanelTabCount = 0;
        RegisterPlayerPanelTab(new EnvironmentPlayerPanelTab());
        RegisterPlayerPanelTab(new DevicePlayerPanelTab());
        RegisterPlayerPanelTab(new GenePlayerPanelTab());
        RegisterPlayerPanelTab(new ResearchPlayerPanelTab());
        RegisterPlayerPanelTab(new EventsPlayerPanelTab());
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
        DrawPlacedDeviceIcons();
        DrawDeviceRangeOverlays();
        DrawDevicePlacementPreview();
        DrawSaveWindow(style, shadowStyle);
        if (IsSaveModalActive())
            DrawSaveDimOverlay();
        DrawSaveConfirmModal(style, shadowStyle);
        DrawDeleteConfirmModal(style, shadowStyle);
    }

    bool IsSaveModalActive()
    {
        return showSaveConfirm || showDeleteConfirm;
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

        Rect saveRect = new Rect(x + 12, y + 82, panelWidth - 24, 28);
        if (GUI.Button(saveRect, "保存游戏"))
            OpenSaveWindow();
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
        float btnWidth = 108f;
        float btnHeight = 28f;
        float labelHeight = 18f;
        float spacing = 4f;
        float sectionGap = 6f;
        float totalHeight = labelHeight
            + btnHeight * 2 + spacing
            + sectionGap
            + labelHeight
            + btnHeight * 2 + spacing
            + sectionGap
            + btnHeight
            + 8f;
        float panelLeft = GetPlayerPanelLeftBoundary();
        float x = panelLeft - btnWidth;
        float y = Screen.height - totalHeight - PanelEdgeMargin;

        Color origBg = GUI.backgroundColor;
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, btnWidth, labelHeight), "基础视图");
        y += labelHeight + spacing;

        bool baseTerrain = CellRenderer.currentBaseViewMode == CellRenderer.BaseViewMode.Terrain;
        GUI.backgroundColor = baseTerrain ? new Color(0.3f, 0.8f, 1f) : new Color(0.5f, 0.5f, 0.5f);
        if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), "地形视图"))
            CellRenderer.currentBaseViewMode = CellRenderer.BaseViewMode.Terrain;
        y += btnHeight + spacing;

        bool baseAltitude = CellRenderer.currentBaseViewMode == CellRenderer.BaseViewMode.Altitude;
        GUI.backgroundColor = baseAltitude ? new Color(0.3f, 0.8f, 1f) : new Color(0.5f, 0.5f, 0.5f);
        if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), "高度视图"))
            CellRenderer.currentBaseViewMode = CellRenderer.BaseViewMode.Altitude;
        y += btnHeight + sectionGap;

        GUI.backgroundColor = origBg;
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, btnWidth, labelHeight), "叠加视图");
        y += labelHeight + spacing;

        bool showTemp = (CellRenderer.currentOverlayViewMode & CellRenderer.OverlayViewMode.Temperature) != 0;
        GUI.backgroundColor = showTemp ? new Color(0.25f, 0.9f, 0.65f) : new Color(0.45f, 0.45f, 0.45f);
        if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), "温度视图"))
        {
            if (showTemp)
                CellRenderer.currentOverlayViewMode &= ~CellRenderer.OverlayViewMode.Temperature;
            else
                CellRenderer.currentOverlayViewMode |= CellRenderer.OverlayViewMode.Temperature;
        }
        y += btnHeight + spacing;

        bool showLight = (CellRenderer.currentOverlayViewMode & CellRenderer.OverlayViewMode.Light) != 0;
        GUI.backgroundColor = showLight ? new Color(0.25f, 0.9f, 0.65f) : new Color(0.45f, 0.45f, 0.45f);
        if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), "光照视图"))
        {
            if (showLight)
                CellRenderer.currentOverlayViewMode &= ~CellRenderer.OverlayViewMode.Light;
            else
                CellRenderer.currentOverlayViewMode |= CellRenderer.OverlayViewMode.Light;
        }
        y += btnHeight + sectionGap;

        bool normalEnabled = CellRenderer.normalLightingEnabled;
        GUI.backgroundColor = normalEnabled ? new Color(0.25f, 0.9f, 0.65f) : new Color(0.45f, 0.45f, 0.45f);
        if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), "3D"))
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
        if (showSaveWindow || IsSaveModalActive())
        {
            hasHoveredEnvironmentCell = false;
            return;
        }
        if (DeviceSystem.IsPlacing)
        {
            hasHoveredEnvironmentCell = false;
            return;
        }
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
        {
            if (DeviceSystem.TryGetDeviceAt(gridX, gridY, out DeviceInstance instance))
            {
                hasSelectedDevice = true;
                selectedDevice = instance;
                return;
            }

            hasSelectedDevice = false;
            ToggleLockedEnvironmentCell(gridX, gridY);
        }
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
        if (showSaveWindow && lastSaveWindowRect.Contains(guiPosition))
            return true;
        if (IsSaveModalActive() && lastSaveModalBlockerRect.Contains(guiPosition))
            return true;
        if (showSaveConfirm && lastSaveConfirmRect.Contains(guiPosition))
            return true;
        if (showDeleteConfirm && lastSaveConfirmRect.Contains(guiPosition))
            return true;
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
        float panelHeight = 122f;
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
        float btnWidth = 108f;
        float btnHeight = 28f;
        float labelHeight = 18f;
        float spacing = 4f;
        float sectionGap = 6f;
        float totalHeight = labelHeight
            + btnHeight * 2 + spacing
            + sectionGap
            + labelHeight
            + btnHeight * 2 + spacing
            + sectionGap
            + btnHeight
            + 8f;
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

    void HandleDevicePlacementInput()
    {
        if (showSaveWindow || IsSaveModalActive())
            return;
        if (!DeviceSystem.IsPlacing)
            return;

        if (Input.GetMouseButtonDown(1))
        {
            DeviceSystem.CancelPlacement();
            return;
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        Vector2 guiMouse = GetGuiMousePosition();
        if (IsPointOverInteractiveUI(guiMouse))
            return;

        int gridX;
        int gridY;
        if (!TryGetEnvironmentCellUnderMouse(out gridX, out gridY))
            return;

        if (DeviceSystem.TryPlaceDevice(DeviceSystem.PlacingTypeId, gridX, gridY))
            DeviceSystem.CancelPlacement();
    }

    void DrawDevicePlacementPreview()
    {
        if (!DeviceSystem.IsPlacing)
            return;

        Texture2D preview = DeviceSystem.GetPlacingPreviewTexture();
        if (preview == null)
            return;

        Vector2 guiMouse = GetGuiMousePosition();
        if (IsPointOverInteractiveUI(guiMouse))
            return;

        int gridX;
        int gridY;
        if (!TryGetEnvironmentCellUnderMouse(out gridX, out gridY))
            return;

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
            return;

        float ppe = SimulationConfig.PixelPerEnvir;
        float worldX = (gridX - 0.5f) * ppe;
        float worldY = (gridY - 0.5f) * ppe;
        Vector3 screen = worldCamera.WorldToScreenPoint(new Vector3(worldX, worldY, 0f));
        if (screen.z < 0f)
            return;

        float size = SimulationConfig.DevicePreviewSize;
        Rect rect = new Rect(screen.x - size * 0.5f, Screen.height - screen.y - size * 0.5f, size, size);
        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, SimulationConfig.DevicePreviewAlpha);
        GUI.DrawTexture(rect, preview, ScaleMode.StretchToFill);
        GUI.color = prevColor;
    }

    void DrawPlacedDeviceIcons()
    {
        if (SimulationCore.EnvirData == null)
            return;

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
            return;

        var placed = DeviceSystem.GetPlacedDevices();
        if (placed == null || placed.Count == 0)
            return;

        float ppe = SimulationConfig.PixelPerEnvir;
        float size = SimulationConfig.DeviceIconSize;
        for (int i = 0; i < placed.Count; i++)
        {
            DeviceInstance instance = placed[i];
            DeviceType type = DeviceSystem.GetDeviceType(instance.TypeId);
            if (type == null || type.Icon == null)
                continue;

            float worldX = (instance.X - 0.5f) * ppe;
            float worldY = (instance.Y - 0.5f) * ppe;
            Vector3 screen = worldCamera.WorldToScreenPoint(new Vector3(worldX, worldY, 0f));
            if (screen.z < 0f)
                continue;

            Rect rect = new Rect(screen.x - size * 0.5f, Screen.height - screen.y - size * 0.5f, size, size);
            GUI.DrawTexture(rect, type.Icon, ScaleMode.StretchToFill);
        }
    }

    void DrawDeviceRangeOverlays()
    {
        if (showSaveWindow || IsSaveModalActive())
            return;
        if (DeviceSystem.IsPlacing)
        {
            Vector2 guiMouse = GetGuiMousePosition();
            if (!IsPointOverInteractiveUI(guiMouse) && TryGetEnvironmentCellUnderMouse(out int gridX, out int gridY))
            {
                int range = DeviceSystem.GetDeviceRange(DeviceSystem.PlacingTypeId);
                DrawDeviceRangeOverlay(gridX, gridY, range);
            }
        }

        if (hasSelectedDevice)
        {
            int range = DeviceSystem.GetDeviceRange(selectedDevice.TypeId);
            DrawDeviceRangeOverlay(selectedDevice.X, selectedDevice.Y, range);
        }
    }

    void DrawDeviceRangeOverlay(int centerX, int centerY, int range)
    {
        if (range <= 0)
            return;

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
            return;

        EnsureRangeOverlayTexture();
        float ppe = SimulationConfig.PixelPerEnvir;
        int rangeSq = range * range;
        Color prev = GUI.color;
        GUI.color = SimulationConfig.DeviceRangeOverlayColor;

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                int distSq = dx * dx + dy * dy;
                if (distSq > rangeSq)
                    continue;

                int x = centerX + dx;
                int y = centerY + dy;
                if (!SimulationCore.InBounds(x, y))
                    continue;

                Vector3 worldBL = new Vector3((x - 1) * ppe, (y - 1) * ppe, 0f);
                Vector3 worldTR = new Vector3(x * ppe, y * ppe, 0f);
                Vector3 screenBL = worldCamera.WorldToScreenPoint(worldBL);
                Vector3 screenTR = worldCamera.WorldToScreenPoint(worldTR);
                if (screenTR.z < 0f)
                    continue;

                float width = screenTR.x - screenBL.x;
                float height = screenTR.y - screenBL.y;
                if (width <= 0f || height <= 0f)
                    continue;

                Rect rect = new Rect(screenBL.x, Screen.height - screenTR.y, width, height);
                GUI.DrawTexture(rect, rangeOverlayTexture, ScaleMode.StretchToFill);
            }
        }

        GUI.color = prev;
    }

    void EnsureRangeOverlayTexture()
    {
        if (rangeOverlayTexture != null)
            return;

        rangeOverlayTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        rangeOverlayTexture.filterMode = FilterMode.Point;
        rangeOverlayTexture.wrapMode = TextureWrapMode.Clamp;
        rangeOverlayTexture.SetPixel(0, 0, Color.white);
        rangeOverlayTexture.Apply();
    }

    void OpenSaveWindow()
    {
        if (showSaveWindow)
            return;

        showSaveWindow = true;
        pausedBeforeSave = SimulationCore.IsPaused();
        SimulationCore.PauseSimulation();
        RefreshSaveSlots(true);
    }

    void CloseSaveWindow()
    {
        showSaveWindow = false;
        showSaveConfirm = false;
        showDeleteConfirm = false;
        pendingSaveSlot = -1;
        pendingDeleteSlot = -1;
        if (!pausedBeforeSave)
            SimulationCore.ResumeSimulation();
    }

    void RefreshSaveSlots(bool force)
    {
        float now = Time.realtimeSinceStartup;
        if (!force && now - lastSaveSlotsRefreshTime < 0.5f)
            return;

        SaveSystem.ReleaseSlotTextures(cachedSaveSlots);
        cachedSaveSlots = SaveSystem.LoadAllSlotInfos();
        lastSaveSlotsRefreshTime = now;
    }

    void DrawSaveDimOverlay()
    {
        // 仅绘制遮罩，不注册全屏按钮（全屏按钮会抢走 ModalWindow 的点击）
        lastSaveModalBlockerRect = new Rect(0f, 0f, Screen.width, Screen.height);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, SimulationConfig.SaveModalOverlayAlpha);
        GUI.Box(lastSaveModalBlockerRect, GUIContent.none);
        GUI.color = previousColor;
    }

    void DrawSaveConfirmModal(GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        if (!showSaveConfirm)
            return;

        saveModalLabelStyle = labelStyle;
        saveModalShadowStyle = shadowStyle;

        float width = 360f;
        float height = 130f;
        Rect rect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        int prevDepth = GUI.depth;
        GUI.depth = 1000;
        lastSaveConfirmRect = GUI.ModalWindow(SaveConfirmModalId, rect, DrawSaveConfirmWindow, "覆盖存档");
        GUI.depth = prevDepth;
    }

    void DrawSaveConfirmWindow(int windowId)
    {
        GUIStyle labelStyle = saveModalLabelStyle ?? GUI.skin.label;
        GUIStyle shadowStyle = saveModalShadowStyle ?? GUI.skin.label;

        GUILayout.Label("覆盖该存档？", labelStyle);
        GUILayout.Space(12f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("确定", GUILayout.Height(30f)))
        {
            int slot = pendingSaveSlot;
            showSaveConfirm = false;
            pendingSaveSlot = -1;
            PerformSave(slot);
        }
        if (GUILayout.Button("取消", GUILayout.Height(30f)))
        {
            showSaveConfirm = false;
            pendingSaveSlot = -1;
        }
        GUILayout.EndHorizontal();
    }

    void DrawDeleteConfirmModal(GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        if (!showDeleteConfirm)
            return;

        saveModalLabelStyle = labelStyle;
        saveModalShadowStyle = shadowStyle;

        float width = 360f;
        float height = 130f;
        Rect rect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        int prevDepth = GUI.depth;
        GUI.depth = 1000;
        lastSaveConfirmRect = GUI.ModalWindow(DeleteConfirmModalId, rect, DrawDeleteConfirmWindow, "删除存档");
        GUI.depth = prevDepth;
    }

    void DrawDeleteConfirmWindow(int windowId)
    {
        GUIStyle labelStyle = saveModalLabelStyle ?? GUI.skin.label;

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
            RefreshSaveSlots(true);
        }
        if (GUILayout.Button("取消", GUILayout.Height(30f)))
        {
            showDeleteConfirm = false;
            pendingDeleteSlot = -1;
        }
        GUILayout.EndHorizontal();
    }

    void DrawSaveWindow(GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        if (!showSaveWindow)
            return;

        RefreshSaveSlots(false);

        float width = Mathf.Min(760f, Screen.width * 0.85f);
        float height = Mathf.Min(520f, Screen.height * 0.85f);
        Rect windowRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
        lastSaveWindowRect = windowRect;

        GUI.Box(windowRect, "");

        Rect titleRect = new Rect(windowRect.x + 16f, windowRect.y + 10f, windowRect.width - 64f, 24f);
        GUI.Label(new Rect(titleRect.x + 1f, titleRect.y + 1f, titleRect.width, titleRect.height), "存档", shadowStyle);
        GUI.Label(titleRect, "存档", labelStyle);

        Rect closeRect = new Rect(windowRect.xMax - 34f, windowRect.y + 8f, 24f, 24f);
        if (!IsSaveModalActive() && GUI.Button(closeRect, "X"))
        {
            CloseSaveWindow();
            return;
        }

        float padding = 14f;
        float rowHeight = 86f;
        float rowSpacing = 8f;
        float deleteButtonWidth = 56f;
        float y = windowRect.y + 44f;
        int slotCount = Mathf.Max(1, SimulationConfig.SaveSlotCount);
        bool modalActive = IsSaveModalActive();

        for (int i = 0; i < slotCount; i++)
        {
            if (y + rowHeight > windowRect.yMax - padding)
                break;

            Rect rowRect = new Rect(windowRect.x + padding, y, windowRect.width - padding * 2f, rowHeight);
            GUI.Box(rowRect, "");

            SaveSlotInfo info = cachedSaveSlots != null && i < cachedSaveSlots.Length ? cachedSaveSlots[i] : null;
            bool hasData = info != null && info.HasData;
            Rect actionRect = rowRect;
            if (hasData)
                actionRect = new Rect(rowRect.x, rowRect.y, rowRect.width - deleteButtonWidth - 4f, rowRect.height);

            if (hasData)
            {
                float thumbSize = rowHeight - 16f;
                Rect thumbRect = new Rect(rowRect.x + 8f, rowRect.y + 8f, thumbSize, thumbSize);
                if (info.Screenshot != null)
                    GUI.DrawTexture(thumbRect, info.Screenshot, ScaleMode.ScaleToFit);

                float textX = thumbRect.xMax + 12f;
                float textWidth = actionRect.xMax - textX - 8f;
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
                if (!modalActive && GUI.Button(deleteRect, "删除"))
                {
                    pendingDeleteSlot = i;
                    showDeleteConfirm = true;
                }
            }
            else if (!modalActive)
            {
                string emptyText = string.Format("槽位 {0}  ·  空槽位（点击保存）", i + 1);
                Rect emptyRect = new Rect(rowRect.x + 12f, rowRect.y + 8f, rowRect.width - 24f, rowHeight - 16f);
                GUI.Label(new Rect(emptyRect.x + 1f, emptyRect.y + 1f, emptyRect.width, emptyRect.height), emptyText, shadowStyle);
                GUI.Label(emptyRect, emptyText, labelStyle);
            }

            // 点击区域放在最后绘制；不用 GUIStyle.none（在部分版本无法接收点击）
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 1f, 1f, 0.01f);
            if (!modalActive && GUI.Button(actionRect, GUIContent.none))
            {
                if (hasData)
                {
                    pendingSaveSlot = i;
                    showSaveConfirm = true;
                }
                else
                {
                    PerformSave(i);
                }
            }
            GUI.backgroundColor = prevBg;

            y += rowHeight + rowSpacing;
        }
    }

    void PerformSave(int slot)
    {
        if (saveScreenshotPending)
            return;
        StartCoroutine(PerformSaveCoroutine(slot));
    }

    IEnumerator PerformSaveCoroutine(int slot)
    {
        saveScreenshotPending = true;
        try
        {
            bool restoreWindow = showSaveWindow;
            showSaveConfirm = false;
            showDeleteConfirm = false;
            showSaveWindow = false;
            yield return new WaitForEndOfFrame();

            SaveSystem.SaveToSlot(slot, this);
            showSaveWindow = restoreWindow;
            RefreshSaveSlots(true);
        }
        finally
        {
            saveScreenshotPending = false;
        }
    }

    public ViewSaveData CaptureViewState()
    {
        ViewSaveData data = new ViewSaveData();
        Camera cam = Camera.main;
        if (cam != null)
        {
            data.CameraPosition = cam.transform.position;
            data.CameraOrthoSize = cam.orthographicSize;
        }
        data.BaseViewMode = (int)CellRenderer.currentBaseViewMode;
        data.OverlayViewMode = (int)CellRenderer.currentOverlayViewMode;
        data.NormalLightingEnabled = CellRenderer.normalLightingEnabled;
        data.PlayerPanelExpanded = playerPanelExpanded;
        data.ActivePlayerPanelTabIndex = activePlayerPanelTabIndex;
        data.SimulationSpeed = simulationSpeed;
        data.ChemicalOverlayMask = ChemistrySystem.ChemicalOverlayMask;
        return data;
    }

    public void ApplyViewState(ViewSaveData data)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = data.CameraPosition;
            cam.orthographicSize = data.CameraOrthoSize;
        }
        CellRenderer.currentBaseViewMode = (CellRenderer.BaseViewMode)data.BaseViewMode;
        CellRenderer.currentOverlayViewMode = (CellRenderer.OverlayViewMode)data.OverlayViewMode;
        CellRenderer.normalLightingEnabled = data.NormalLightingEnabled;
        playerPanelExpanded = data.PlayerPanelExpanded;
        activePlayerPanelTabIndex = Mathf.Clamp(data.ActivePlayerPanelTabIndex, 0, Mathf.Max(0, playerPanelTabCount - 1));
        simulationSpeed = Mathf.Clamp(data.SimulationSpeed, 1, 10);
        SimulationCore.SetSpeedMultiplier(simulationSpeed);
        ChemistrySystem.SetChemicalOverlayMask(data.ChemicalOverlayMask);
    }
}
