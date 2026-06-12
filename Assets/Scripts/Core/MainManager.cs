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

    void Reset()
    {
        InitializePlayerPanelTabs();
        EnsurePlayerPanelToggleTexture();
        SyncRenderSettingsFromSimulationConfig(true);
        ApplyRenderSettings();
    }

    void Awake()
    {
        DisplaySettings.LoadAndApply();
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
        Color fill = GeneMachineGuiTheme.Panel;
        Color outline = GeneMachineGuiTheme.BorderHot;

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

        GUIStyle style = GeneMachineGuiTheme.BuildLabelStyle(16);
        GUIStyle shadowStyle = GeneMachineGuiTheme.BuildShadowStyle(style);

        DrawSimulationCommandBar(style, shadowStyle);

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

    void DrawSimulationCommandBar(GUIStyle style, GUIStyle shadowStyle)
    {
        float rightBoundary = Mathf.Min(GetPlayerPanelLeftBoundary() - PanelEdgeMargin, Screen.width - PanelEdgeMargin);
        float width = Mathf.Clamp(rightBoundary - PanelEdgeMargin, 560f, 900f);
        Rect barRect = new Rect(PanelEdgeMargin, PanelEdgeMargin, width, 112f);

        GeneMachineGuiTheme.DrawPanel(barRect);
        GeneMachineGuiTheme.DrawGrid(barRect, 32f);

        GUIStyle brandStyle = GeneMachineGuiTheme.BuildTitleStyle(24, TextAnchor.UpperLeft);
        GUIStyle brandShadow = GeneMachineGuiTheme.BuildShadowStyle(brandStyle);
        GUIStyle monoStyle = GeneMachineGuiTheme.BuildLabelStyle(12);
        monoStyle.fontStyle = FontStyle.Bold;
        monoStyle.normal.textColor = GeneMachineGuiTheme.MutedText;
        GUIStyle monoShadow = GeneMachineGuiTheme.BuildShadowStyle(monoStyle);

        GeneMachineGuiTheme.DrawGeneMark(new Rect(barRect.x + 14f, barRect.y + 16f, 46f, 46f), 0.90f);

        Rect brandRect = new Rect(barRect.x + 68f, barRect.y + 12f, 138f, 34f);
        GeneMachineGuiTheme.DrawText(brandRect, "GeneMachine", brandStyle, brandShadow);

        bool paused = SimulationCore.IsPaused();
        string stateText = paused ? "PAUSED" : "RUNNING";
        Color stateColor = paused ? GeneMachineGuiTheme.Amber : GeneMachineGuiTheme.GeneGreen;
        Rect stateRect = new Rect(barRect.x + 70f, barRect.y + 52f, 136f, 18f);
        GeneMachineGuiTheme.DrawStatusDot(new Vector2(stateRect.x + 7f, stateRect.y + 9f), stateColor);
        GeneMachineGuiTheme.DrawText(new Rect(stateRect.x + 22f, stateRect.y, stateRect.width - 22f, stateRect.height), stateText, monoStyle, monoShadow);

        Rect hintRect = new Rect(barRect.x + 16f, barRect.yMax - 28f, barRect.width - 32f, 18f);
        GeneMachineGuiTheme.DrawText(hintRect, "SPACE 暂停/继续   WASD 移动   滚轮 缩放   左键锁定环境格   F1 隐藏UI", monoStyle, monoShadow);

        float chipX = barRect.x + 214f;
        float chipY = barRect.y + 18f;
        float gap = 8f;
        float chipWidth = Mathf.Max(104f, (barRect.xMax - chipX - 18f - gap * 3f) / 4f);
        GeneMachineGuiTheme.DrawMetricChip(new Rect(chipX, chipY, chipWidth, 52f), "STEPS / SEC", SimulationCore.stepsPerSecond.ToString(), GeneMachineGuiTheme.Cyan, style, shadowStyle);
        chipX += chipWidth + gap;
        GeneMachineGuiTheme.DrawMetricChip(new Rect(chipX, chipY, chipWidth, 52f), "FPS", (1f / Mathf.Max(Time.deltaTime, 0.0001f)).ToString("F0"), GeneMachineGuiTheme.GeneGreen, style, shadowStyle);
        chipX += chipWidth + gap;
        GeneMachineGuiTheme.DrawMetricChip(new Rect(chipX, chipY, chipWidth, 52f), "CELLS", SimulationCore.aliveCellCount.ToString(), GeneMachineGuiTheme.Amber, style, shadowStyle);
        chipX += chipWidth + gap;
        GeneMachineGuiTheme.DrawMetricChip(new Rect(chipX, chipY, chipWidth, 52f), "STEP", SimulationCore.totalSteps.ToString(), GeneMachineGuiTheme.Cyan, style, shadowStyle);
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

        GeneMachineGuiTheme.DrawPanel(new Rect(x, y, panelWidth, panelHeight));

        GeneMachineGuiTheme.DrawText(new Rect(x + 12, y + 10, panelWidth - 20, 22), "游戏速度", style, shadowStyle);

        int currentSpeed = SimulationCore.speedMultiplier;
        float sliderValue = GUI.HorizontalSlider(new Rect(x + 12, y + 42, panelWidth - 24, 20), currentSpeed, 1f, 10f);
        int newSpeed = Mathf.RoundToInt(sliderValue);
        if (newSpeed != currentSpeed)
        {
            SimulationCore.SetSpeedMultiplier(newSpeed);
        }

        string speedText = string.Format("{0}x  ({1:F1}秒/步)", SimulationCore.speedMultiplier, 1f / SimulationCore.speedMultiplier);
        GeneMachineGuiTheme.DrawText(new Rect(x + 12, y + 59, panelWidth - 20, 22), speedText, style, shadowStyle);

        Rect saveRect = new Rect(x + 12, y + 82, panelWidth - 24, 28);
        if (GeneMachineGuiTheme.DrawButton(saveRect, "保存游戏", false))
            OpenSaveWindow();
    }

    void DrawPlayerOperationPanel(GUIStyle style, GUIStyle shadowStyle)
    {
        DrawPlayerPanelToggle();

        if (!playerPanelExpanded)
            return;

        Rect panelRect = GetPlayerPanelRect();
        GeneMachineGuiTheme.DrawPanel(panelRect);
        GeneMachineGuiTheme.DrawGrid(panelRect, 28f);

        Rect headerRect = new Rect(panelRect.x + 18f, panelRect.y + 16f, panelRect.width - 36f, 58f);
        GeneMachineGuiTheme.DrawSectionHeader(headerRect, "SIMULATION INSPECTOR", "Gene Console", style, shadowStyle);

        float railWidth = 112f;
        Rect railRect = new Rect(panelRect.x + 16f, headerRect.yMax + 14f, railWidth, panelRect.height - headerRect.yMax - 30f);
        GeneMachineGuiTheme.DrawInset(railRect);
        DrawPlayerPanelTabs(railRect, style, shadowStyle);

        Rect contentRect = new Rect(railRect.xMax + 12f, railRect.y, panelRect.xMax - railRect.xMax - 28f, railRect.height);
        GeneMachineGuiTheme.DrawInset(contentRect);

        PlayerPanelTabPage activeTab = GetActivePlayerPanelTab();
        if (activeTab != null)
            activeTab.Draw(contentRect, style, shadowStyle, BuildPlayerPanelContext());
    }

    void DrawPlayerPanelToggle()
    {
        EnsurePlayerPanelToggleTexture();
        Rect toggleRect = GetPlayerPanelToggleRect();
        GUIStyle toggleStyle = GeneMachineGuiTheme.BuildTitleStyle(22, TextAnchor.MiddleCenter);
        toggleStyle.fontSize = 22;
        toggleStyle.fontStyle = FontStyle.Bold;
        toggleStyle.normal.textColor = GeneMachineGuiTheme.Text;
        GUIStyle toggleShadow = GeneMachineGuiTheme.BuildShadowStyle(toggleStyle);

        string label = playerPanelExpanded ? ">" : "<";
        GUI.DrawTexture(toggleRect, playerPanelToggleTexture, ScaleMode.StretchToFill);
        GeneMachineGuiTheme.DrawText(toggleRect, label, toggleStyle, toggleShadow);
        if (GeneMachineGuiTheme.DrawTransparentClick(toggleRect))
            playerPanelExpanded = !playerPanelExpanded;
    }

    void DrawPlayerPanelTabs(Rect tabStripRect, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        if (playerPanelTabCount == 0)
            return;

        float spacing = 8f;
        float tabHeight = 54f;
        float y = tabStripRect.y + 10f;

        for (int i = 0; i < playerPanelTabCount; i++)
        {
            if (y + tabHeight > tabStripRect.yMax - 10f)
                break;

            bool selected = i == activePlayerPanelTabIndex;
            Rect tabRect = new Rect(tabStripRect.x + 8f, y, tabStripRect.width - 16f, tabHeight);
            string index = string.Format("{0:00}", i + 1);
            if (GeneMachineGuiTheme.DrawNavTab(tabRect, index, playerPanelTabs[i].Title, selected, labelStyle, shadowStyle))
                activePlayerPanelTabIndex = i;

            y += tabHeight + spacing;
        }
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
        Color origColor = GUI.color;
        GUIStyle modeLabel = GeneMachineGuiTheme.BuildLabelStyle(14);
        GUIStyle modeShadow = GeneMachineGuiTheme.BuildShadowStyle(modeLabel);
        Rect columnRect = GetViewModeButtonColumnRect();
        Rect panelRect = new Rect(columnRect.x - 10f, columnRect.y - 10f, columnRect.width + 20f, columnRect.height + 20f);
        GeneMachineGuiTheme.DrawPanel(panelRect);
        GeneMachineGuiTheme.DrawText(new Rect(x, y, btnWidth, labelHeight), "基础视图", modeLabel, modeShadow);
        y += labelHeight + spacing;

        bool baseTerrain = CellRenderer.currentBaseViewMode == CellRenderer.BaseViewMode.Terrain;
        if (GeneMachineGuiTheme.DrawButton(new Rect(x, y, btnWidth, btnHeight), "地形视图", baseTerrain))
            CellRenderer.currentBaseViewMode = CellRenderer.BaseViewMode.Terrain;
        y += btnHeight + spacing;

        bool baseAltitude = CellRenderer.currentBaseViewMode == CellRenderer.BaseViewMode.Altitude;
        if (GeneMachineGuiTheme.DrawButton(new Rect(x, y, btnWidth, btnHeight), "高度视图", baseAltitude))
            CellRenderer.currentBaseViewMode = CellRenderer.BaseViewMode.Altitude;
        y += btnHeight + sectionGap;

        GUI.backgroundColor = origBg;
        GUI.color = origColor;
        GeneMachineGuiTheme.DrawText(new Rect(x, y, btnWidth, labelHeight), "叠加视图", modeLabel, modeShadow);
        y += labelHeight + spacing;

        bool showTemp = (CellRenderer.currentOverlayViewMode & CellRenderer.OverlayViewMode.Temperature) != 0;
        if (GeneMachineGuiTheme.DrawButton(new Rect(x, y, btnWidth, btnHeight), "温度视图", showTemp))
        {
            if (showTemp)
                CellRenderer.currentOverlayViewMode &= ~CellRenderer.OverlayViewMode.Temperature;
            else
                CellRenderer.currentOverlayViewMode |= CellRenderer.OverlayViewMode.Temperature;
        }
        y += btnHeight + spacing;

        bool showLight = (CellRenderer.currentOverlayViewMode & CellRenderer.OverlayViewMode.Light) != 0;
        if (GeneMachineGuiTheme.DrawButton(new Rect(x, y, btnWidth, btnHeight), "光照视图", showLight))
        {
            if (showLight)
                CellRenderer.currentOverlayViewMode &= ~CellRenderer.OverlayViewMode.Light;
            else
                CellRenderer.currentOverlayViewMode |= CellRenderer.OverlayViewMode.Light;
        }
        y += btnHeight + sectionGap;

        bool normalEnabled = CellRenderer.normalLightingEnabled;
        if (GeneMachineGuiTheme.DrawButton(new Rect(x, y, btnWidth, btnHeight), "3D", normalEnabled))
        {
            CellRenderer.normalLightingEnabled = !CellRenderer.normalLightingEnabled;
        }

        GUI.backgroundColor = origBg;
        GUI.color = origColor;
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

        GeneMachineGuiTheme.DrawPanel(panelRect);
        GeneMachineGuiTheme.DrawText(new Rect(x + 10, y + 6, panelSize - 22, 20), "太阳方向", style, shadowStyle);

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

        GeneMachineGuiTheme.DrawBox(handleRect, new Color(GeneMachineGuiTheme.Amber.r, GeneMachineGuiTheme.Amber.g, GeneMachineGuiTheme.Amber.b, 0.20f), GeneMachineGuiTheme.Amber);
        GUIStyle handleStyle = GeneMachineGuiTheme.BuildTitleStyle(16, TextAnchor.MiddleCenter);
        handleStyle.normal.textColor = GeneMachineGuiTheme.Amber;
        GUIStyle handleShadow = GeneMachineGuiTheme.BuildShadowStyle(handleStyle);
        GeneMachineGuiTheme.DrawText(handleRect, "☼", handleStyle, handleShadow);
    }

    void DrawLightControlGuides(Rect squareRect)
    {
        GeneMachineGuiTheme.DrawBox(squareRect, GeneMachineGuiTheme.Inset, GeneMachineGuiTheme.Border);

        GeneMachineGuiTheme.DrawBox(new Rect(squareRect.x, squareRect.center.y - 1f, squareRect.width, 2f), new Color(1f, 1f, 1f, 0.16f), Color.clear);
        GeneMachineGuiTheme.DrawBox(new Rect(squareRect.center.x - 1f, squareRect.y, 2f, squareRect.height), new Color(1f, 1f, 1f, 0.16f), Color.clear);

        float quarterX = squareRect.width * 0.25f;
        float quarterY = squareRect.height * 0.25f;
        Color guideColor = new Color(1f, 1f, 1f, 0.08f);
        GeneMachineGuiTheme.DrawBox(new Rect(squareRect.x + quarterX - 1f, squareRect.y, 2f, squareRect.height), guideColor, Color.clear);
        GeneMachineGuiTheme.DrawBox(new Rect(squareRect.x + quarterX * 3f - 1f, squareRect.y, 2f, squareRect.height), guideColor, Color.clear);
        GeneMachineGuiTheme.DrawBox(new Rect(squareRect.x, squareRect.y + quarterY - 1f, squareRect.width, 2f), guideColor, Color.clear);
        GeneMachineGuiTheme.DrawBox(new Rect(squareRect.x, squareRect.y + quarterY * 3f - 1f, squareRect.width, 2f), guideColor, Color.clear);
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
        return new Rect(x, PanelEdgeMargin + 128f, panelWidth, panelHeight);
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
        lastSaveModalBlockerRect = new Rect(0f, 0f, Screen.width, Screen.height);
        GeneMachineGuiTheme.DrawBox(lastSaveModalBlockerRect, new Color(0f, 0f, 0f, SimulationConfig.SaveModalOverlayAlpha), Color.clear);
    }

    void DrawSaveConfirmModal(GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        if (!showSaveConfirm)
            return;

        float width = 360f;
        float height = 148f;
        Rect rect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        lastSaveConfirmRect = rect;
        GeneMachineGuiTheme.DrawModalShell(rect, "覆盖存档", labelStyle, shadowStyle);
        GeneMachineGuiTheme.DrawText(new Rect(rect.x + 18f, rect.y + 58f, rect.width - 36f, 24f), "覆盖该存档？", labelStyle, shadowStyle);

        Rect confirmRect = new Rect(rect.x + 18f, rect.yMax - 44f, (rect.width - 48f) * 0.5f, 28f);
        Rect cancelRect = new Rect(confirmRect.xMax + 12f, confirmRect.y, confirmRect.width, confirmRect.height);
        if (GeneMachineGuiTheme.DrawButton(confirmRect, "确定", true))
        {
            int slot = pendingSaveSlot;
            showSaveConfirm = false;
            pendingSaveSlot = -1;
            PerformSave(slot);
        }
        if (GeneMachineGuiTheme.DrawButton(cancelRect, "取消", false))
        {
            showSaveConfirm = false;
            pendingSaveSlot = -1;
        }
    }

    void DrawDeleteConfirmModal(GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        if (!showDeleteConfirm)
            return;

        float width = 360f;
        float height = 148f;
        Rect rect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        lastSaveConfirmRect = rect;
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
            RefreshSaveSlots(true);
        }
        if (GeneMachineGuiTheme.DrawButton(cancelRect, "取消", false))
        {
            showDeleteConfirm = false;
            pendingDeleteSlot = -1;
        }
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

        GeneMachineGuiTheme.DrawPanel(windowRect);
        GeneMachineGuiTheme.DrawGrid(windowRect, 28f);

        Rect titleRect = new Rect(windowRect.x + 16f, windowRect.y + 10f, windowRect.width - 64f, 24f);
        GeneMachineGuiTheme.DrawText(titleRect, "存档", labelStyle, shadowStyle);

        Rect closeRect = new Rect(windowRect.xMax - 34f, windowRect.y + 8f, 24f, 24f);
        if (!IsSaveModalActive() && GeneMachineGuiTheme.DrawCloseButton(closeRect))
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
            SaveSlotInfo info = cachedSaveSlots != null && i < cachedSaveSlots.Length ? cachedSaveSlots[i] : null;
            bool hasData = info != null && info.HasData;
            GeneMachineGuiTheme.DrawCard(rowRect, hasData);
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
                GeneMachineGuiTheme.DrawText(slotLabelRect, slotLabel, labelStyle, shadowStyle);
                GeneMachineGuiTheme.DrawText(timeRect, timeText, labelStyle, shadowStyle);
                GeneMachineGuiTheme.DrawText(playRect, playText, labelStyle, shadowStyle);

                Rect deleteRect = new Rect(rowRect.xMax - deleteButtonWidth - 6f, rowRect.y + (rowHeight - 28f) * 0.5f, deleteButtonWidth, 28f);
                if (!modalActive && GeneMachineGuiTheme.DrawButton(deleteRect, "删除", false))
                {
                    pendingDeleteSlot = i;
                    showDeleteConfirm = true;
                }
            }
            else if (!modalActive)
            {
                string emptyText = string.Format("槽位 {0}  ·  空槽位（点击保存）", i + 1);
                Rect emptyRect = new Rect(rowRect.x + 12f, rowRect.y + 8f, rowRect.width - 24f, rowHeight - 16f);
                GeneMachineGuiTheme.DrawText(emptyRect, emptyText, labelStyle, shadowStyle);
            }

            // 点击区域放在最后绘制；不用 GUIStyle.none（在部分版本无法接收点击）
            if (!modalActive && GeneMachineGuiTheme.DrawTransparentClick(actionRect))
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
