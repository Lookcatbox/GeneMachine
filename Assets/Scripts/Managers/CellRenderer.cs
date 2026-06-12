// CellRenderer.cs - 细胞渲染器，分批DrawMeshInstanced
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主线程渲染：地形/海拔底图、温度/光照/化学 overlay（GPU 合成）、细胞 Instancing 与视锥裁剪。
/// </summary>
public class CellRenderer : MonoBehaviour
{
    public static Vector3 terrainLightDirection = SimulationConfig.TerrainLightDirection;
    private static CellRenderer instance;

    private Mesh quadMesh;
    private Material playerMaterial;  // 玩家细胞材质(绿)
    private Material npcMaterial;     // NPC细胞材质(棕)
    private Material playerDimMaterial; // 基因视图中未匹配的玩家细胞
    private Material npcDimMaterial;    // 基因视图中未匹配的 NPC 细胞
    private Texture2D circleTexture;
    private CameraController cameraController;

    // 颜色定义
    private static readonly Color PlayerColor = new Color(0.2f, 0.8f, 0.3f, 1f);
    private static readonly Color NPCColor = new Color(0.8f, 0.5f, 0.2f, 1f);
    private static readonly Color GridLineColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

    // 渲染批次：List 收集矩阵，batchBuffer 供 DrawMeshInstanced 复用
    private List<Matrix4x4> playerMatrices = new List<Matrix4x4>();
    private List<Matrix4x4> npcMatrices = new List<Matrix4x4>();
    private List<Matrix4x4> playerDimMatrices = new List<Matrix4x4>();
    private List<Matrix4x4> npcDimMatrices = new List<Matrix4x4>();
    private Matrix4x4[] batchBuffer;
    private int lastRenderedMatrixCount;
    // 远距离视野：按格去重，每格只保留最高优先级细胞
    private readonly Dictionary<long, Cell> viewportBestCells = new Dictionary<long, Cell>();

    // 网格线
    private Material lineMaterial;

    // 缓存网格线数据
    private int cachedMinX, cachedMaxX, cachedMinY, cachedMaxY;
    private bool drawGrid = false;

    // 视图模式
    public enum BaseViewMode { Terrain = 0, Altitude = 1 }
    [System.Flags]
    public enum OverlayViewMode { None = 0, Temperature = 1, Light = 2, Chemical = 4 }
    public static BaseViewMode currentBaseViewMode = BaseViewMode.Terrain;
    public static OverlayViewMode currentOverlayViewMode = OverlayViewMode.None;
    public static bool normalLightingEnabled = true;
    public static int geneFilterBaseId = 0; // 0=无筛选，>0=基因视图筛选的 baseId
    private BaseViewMode lastBaseViewMode = (BaseViewMode)(-1);
    private OverlayViewMode lastOverlayViewMode = OverlayViewMode.None;
    private bool lastNormalLightingEnabled = true;
    private Vector3 lastTerrainLightDirection = SimulationConfig.TerrainLightDirection;

    // 背景渲染
    private Texture2D bgTexture;
    private Texture2D bgNormalTexture;
    private Material bgMaterial;
    private Mesh bgMesh;
    private bool bgNormalDirty = true;

    // 叠加层：步进写入缓冲池，每帧只显示池中最新快照
    private OverlayBufferPool overlayBufferPool;
    private Material compositeBlitMaterial;
    private Material compositeDisplayMaterial;
    private Mesh overlayMesh;
    private long lastOverlayRenderedStep = -1;
    private long pendingOverlayStep = -1;
    private bool overlayRebuildInProgress;
    private int overlayTextureSize;
    private int chemicalTextureSize;
    private int overlayDownsampleFactor = 1;
    private int chemicalDownsampleFactor = 1;

    void Start()
    {
        instance = this;
        cameraController = FindObjectOfType<CameraController>();
        terrainLightDirection = terrainLightDirection.sqrMagnitude > 0.0001f
            ? terrainLightDirection.normalized
            : SimulationConfig.TerrainLightDirection;
        lastTerrainLightDirection = terrainLightDirection;

        CreateCircleTexture();
        CreateQuadMesh();
        CreateMaterials();
        CreateLineMaterial();
        CreateBackground();
        CreateOverlaySources();
        CreateCompositeOverlay();
    }

    void OnDestroy()
    {
        overlayBufferPool?.Dispose();
        overlayBufferPool = null;
    }

    public static void InvalidateChemicalOverlay()
    {
        ChemicalOverlayRasterizer.NotifyMaskChanged();
    }

    void CreateCircleTexture()
    {
        int size = 64;
        circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        circleTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        float center = size / 2f;
        float radius = size / 2f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= radius - 1f)
                    pixels[y * size + x] = Color.white;
                else if (dist <= radius)
                    pixels[y * size + x] = new Color(1, 1, 1, radius - dist);
                else
                    pixels[y * size + x] = Color.clear;
            }
        }

        circleTexture.SetPixels(pixels);
        circleTexture.Apply();
    }

    void CreateQuadMesh()
    {
        quadMesh = new Mesh();
        quadMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(0.5f, 0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0)
        };
        quadMesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        quadMesh.RecalculateNormals();
    }

    void CreateMaterials()
    {
        Shader shader = Shader.Find("Custom/CellCircleInstanced");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        playerMaterial = CreateCellMaterial(shader, PlayerColor);
        npcMaterial = CreateCellMaterial(shader, NPCColor);

        Color playerDim = PlayerColor;
        playerDim.a = SimulationConfig.GeneViewDimAlpha;
        playerDimMaterial = CreateCellMaterial(shader, playerDim);

        Color npcDim = NPCColor;
        npcDim.a = SimulationConfig.GeneViewDimAlpha;
        npcDimMaterial = CreateCellMaterial(shader, npcDim);

        int maxBatch = SimulationConfig.CellRenderMaxBatch;
        batchBuffer = new Matrix4x4[Mathf.Max(1, maxBatch)];
    }

    /// <summary>创建启用 GPU Instancing 的细胞材质。</summary>
    Material CreateCellMaterial(Shader shader, Color color)
    {
        var mat = new Material(shader);
        mat.mainTexture = circleTexture;
        mat.color = color;
        mat.enableInstancing = true;
        return mat;
    }

    void CreateLineMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    void CreateBackground()
    {
        int size = SimulationConfig.EnvirSize;
        bgTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        bgTexture.filterMode = FilterMode.Point;
        bgTexture.wrapMode = TextureWrapMode.Clamp;

        bgNormalTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        bgNormalTexture.filterMode = FilterMode.Bilinear;
        bgNormalTexture.wrapMode = TextureWrapMode.Clamp;

        Shader shader = Shader.Find("Custom/HeightNormalLit");
        if (shader == null)
        {
            Debug.LogWarning("HeightNormalLit shader not found, falling back to Sprites/Default.");
            shader = Shader.Find("Sprites/Default");
        }
        bgMaterial = new Material(shader);
        bgMaterial.mainTexture = bgTexture;
        bgMaterial.color = Color.white;
        ApplyBackgroundLightingSettings();

        float worldSize = size * SimulationConfig.PixelPerEnvir;
        bgMesh = new Mesh();
        bgMesh.vertices = new Vector3[]
        {
            new Vector3(0, 0, 1),
            new Vector3(worldSize, 0, 1),
            new Vector3(worldSize, worldSize, 1),
            new Vector3(0, worldSize, 1)
        };
        bgMesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        bgMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        bgMesh.RecalculateNormals();
    }

    void ApplyBackgroundLightingSettings()
    {
        if (bgMaterial == null) return;
        if (bgMaterial.HasProperty("_MainTex"))
            bgMaterial.SetTexture("_MainTex", bgTexture);
        if (bgMaterial.HasProperty("_NormalTex"))
            bgMaterial.SetTexture("_NormalTex", bgNormalTexture);
        if (bgMaterial.HasProperty("_LightDir"))
            bgMaterial.SetVector("_LightDir", terrainLightDirection);
        if (bgMaterial.HasProperty("_AmbientStrength"))
            bgMaterial.SetFloat("_AmbientStrength", SimulationConfig.TerrainLightAmbient);
        if (bgMaterial.HasProperty("_DiffuseStrength"))
            bgMaterial.SetFloat("_DiffuseStrength", SimulationConfig.TerrainLightDiffuse);
        if (bgMaterial.HasProperty("_NormalStrength"))
            bgMaterial.SetFloat("_NormalStrength", 1f);
        if (bgMaterial.HasProperty("_CurvatureStrength"))
            bgMaterial.SetFloat("_CurvatureStrength", SimulationConfig.TerrainCurvatureStrength);
        if (bgMaterial.HasProperty("_HeightContrast"))
            bgMaterial.SetFloat("_HeightContrast", SimulationConfig.TerrainHeightContrast);
        if (bgMaterial.HasProperty("_HighTintStrength"))
            bgMaterial.SetFloat("_HighTintStrength", SimulationConfig.TerrainHighTintStrength);
        if (bgMaterial.HasProperty("_LowTintStrength"))
            bgMaterial.SetFloat("_LowTintStrength", SimulationConfig.TerrainLowTintStrength);
        if (bgMaterial.HasProperty("_LightingEnabled"))
            bgMaterial.SetFloat("_LightingEnabled", normalLightingEnabled ? 1f : 0f);
    }

    void ApplyCompositeBlitSettings()
    {
        if (compositeBlitMaterial == null)
            return;

        bool showTemp = (currentOverlayViewMode & OverlayViewMode.Temperature) != 0;
        bool showLight = (currentOverlayViewMode & OverlayViewMode.Light) != 0;
        bool showChem = (currentOverlayViewMode & OverlayViewMode.Chemical) != 0
            && ChemistrySystem.ChemicalOverlayMask != 0;

        if (compositeBlitMaterial.HasProperty("_ShowTemp"))
            compositeBlitMaterial.SetFloat("_ShowTemp", showTemp ? 1f : 0f);
        if (compositeBlitMaterial.HasProperty("_ShowLight"))
            compositeBlitMaterial.SetFloat("_ShowLight", showLight ? 1f : 0f);
        if (compositeBlitMaterial.HasProperty("_ShowChem"))
            compositeBlitMaterial.SetFloat("_ShowChem", showChem ? 1f : 0f);

        if (compositeBlitMaterial.HasProperty("_TempBlue"))
            compositeBlitMaterial.SetColor("_TempBlue", SimulationConfig.TempColorBlue);
        if (compositeBlitMaterial.HasProperty("_TempCyan"))
            compositeBlitMaterial.SetColor("_TempCyan", SimulationConfig.TempColorCyan);
        if (compositeBlitMaterial.HasProperty("_TempGreen"))
            compositeBlitMaterial.SetColor("_TempGreen", SimulationConfig.TempColorGreen);
        if (compositeBlitMaterial.HasProperty("_TempYellow"))
            compositeBlitMaterial.SetColor("_TempYellow", SimulationConfig.TempColorYellow);
        if (compositeBlitMaterial.HasProperty("_TempOrange"))
            compositeBlitMaterial.SetColor("_TempOrange", SimulationConfig.TempColorOrange);
        if (compositeBlitMaterial.HasProperty("_TempRed"))
            compositeBlitMaterial.SetColor("_TempRed", SimulationConfig.TempColorRed);

        if (compositeBlitMaterial.HasProperty("_TempBlueMax"))
            compositeBlitMaterial.SetFloat("_TempBlueMax", SimulationConfig.TempColorBlueMax);
        if (compositeBlitMaterial.HasProperty("_TempCyanMax"))
            compositeBlitMaterial.SetFloat("_TempCyanMax", SimulationConfig.TempColorCyanMax);
        if (compositeBlitMaterial.HasProperty("_TempGreenMax"))
            compositeBlitMaterial.SetFloat("_TempGreenMax", SimulationConfig.TempColorGreenMax);
        if (compositeBlitMaterial.HasProperty("_TempYellowMax"))
            compositeBlitMaterial.SetFloat("_TempYellowMax", SimulationConfig.TempColorYellowMax);
        if (compositeBlitMaterial.HasProperty("_TempOrangeMin"))
            compositeBlitMaterial.SetFloat("_TempOrangeMin", SimulationConfig.TempColorOrangeMin);
        if (compositeBlitMaterial.HasProperty("_TempOrangeMax"))
            compositeBlitMaterial.SetFloat("_TempOrangeMax", SimulationConfig.TempColorOrangeMax);
        if (compositeBlitMaterial.HasProperty("_TempRedMin"))
            compositeBlitMaterial.SetFloat("_TempRedMin", SimulationConfig.TempColorRedMin);

        if (compositeBlitMaterial.HasProperty("_TempEncodeMin"))
            compositeBlitMaterial.SetFloat("_TempEncodeMin", SimulationConfig.OverlayTempEncodeMin);
        if (compositeBlitMaterial.HasProperty("_TempEncodeMax"))
            compositeBlitMaterial.SetFloat("_TempEncodeMax", SimulationConfig.OverlayTempEncodeMax);

        if (compositeBlitMaterial.HasProperty("_LightDark"))
            compositeBlitMaterial.SetColor("_LightDark", SimulationConfig.LightColorDark);
        if (compositeBlitMaterial.HasProperty("_LightBright"))
            compositeBlitMaterial.SetColor("_LightBright", SimulationConfig.LightColorBright);
    }

    void ApplyOverlayMaterialSettings()
    {
        ApplyCompositeBlitSettings();
    }

    public static Vector3 GetTerrainLightDirection()
    {
        return terrainLightDirection;
    }

    public static void SetTerrainLightDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction = direction.normalized;
        if (direction.z < 0.05f)
            direction = new Vector3(direction.x, direction.y, 0.05f).normalized;

        terrainLightDirection = direction;

        if (instance != null)
        {
            instance.ApplyBackgroundLightingSettings();
            instance.lastTerrainLightDirection = terrainLightDirection;
        }
    }

    public static void RefreshRenderingSettings()
    {
        if (instance == null)
            return;

        instance.ApplyBackgroundLightingSettings();
        instance.bgNormalDirty = true;
        instance.lastOverlayRenderedStep = -1;
        instance.lastBaseViewMode = (BaseViewMode)(-1);
        instance.lastOverlayViewMode = OverlayViewMode.None;
        instance.lastTerrainLightDirection = terrainLightDirection;
        instance.ApplyOverlayMaterialSettings();
    }

    int WrapEnvirIndex(int value, int size)
    {
        if (value < 1) return value + size;
        if (value > size) return value - size;
        return value;
    }

    float ApplySignalDeadZone(float value, float deadZone)
    {
        float magnitude = Mathf.Abs(value);
        if (magnitude <= deadZone)
            return 0f;

        float filteredMagnitude = (magnitude - deadZone) / Mathf.Max(0.0001f, 1f - deadZone);
        return Mathf.Sign(value) * filteredMagnitude;
    }

    void CreateOverlaySources()
    {
        int size = SimulationConfig.EnvirSize;
        overlayDownsampleFactor = Mathf.Max(1, SimulationConfig.OverlayDownsampleFactor);
        overlayTextureSize = Mathf.Max(1, size / overlayDownsampleFactor);
        chemicalDownsampleFactor = Mathf.Max(1, SimulationConfig.ChemicalOverlayDownsampleFactor);
        chemicalTextureSize = Mathf.Max(1, size / chemicalDownsampleFactor);

        overlayBufferPool = new OverlayBufferPool();
        overlayBufferPool.Initialize(
            SimulationConfig.OverlayBufferPoolSize,
            overlayTextureSize,
            chemicalTextureSize);

        float worldSize = size * SimulationConfig.PixelPerEnvir;
        overlayMesh = new Mesh();
        overlayMesh.vertices = new Vector3[]
        {
            new Vector3(0, 0, 0.5f),
            new Vector3(worldSize, 0, 0.5f),
            new Vector3(worldSize, worldSize, 0.5f),
            new Vector3(0, worldSize, 0.5f)
        };
        overlayMesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        overlayMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        overlayMesh.RecalculateNormals();
    }

    void CreateCompositeOverlay()
    {
        Shader blitShader = Shader.Find("Custom/OverlayComposite");
        if (blitShader == null)
        {
            Debug.LogWarning("OverlayComposite shader not found, falling back to OverlayColorMap.");
            blitShader = Shader.Find("Custom/OverlayColorMap");
        }
        compositeBlitMaterial = new Material(blitShader);

        Shader displayShader = Shader.Find("Sprites/Default");
        if (displayShader == null)
            displayShader = Shader.Find("Unlit/Transparent");
        compositeDisplayMaterial = new Material(displayShader);
        compositeDisplayMaterial.color = Color.white;

        ApplyCompositeBlitSettings();
    }

    void UpdateBackgroundTexture()
    {
        if (SimulationCore.EnvirData == null) return;
        int size = SimulationConfig.EnvirSize;
        Color32[] pixels = new Color32[size * size];

        for (int y = 1; y <= size; y++)
        {
            for (int x = 1; x <= size; x++)
            {
                Color c;
                Envir env = SimulationCore.EnvirData[x, y];

                if (currentBaseViewMode == BaseViewMode.Altitude)
                {
                    c = GetAltitudeContourColor(env.Height);

                    int currentLevel = GetAltitudeQuantizedLevel(env.Height);
                    bool isContourEdge = false;

                    if (x > 1 && GetAltitudeQuantizedLevel(SimulationCore.EnvirData[x - 1, y].Height) != currentLevel) isContourEdge = true;
                    else if (x < size && GetAltitudeQuantizedLevel(SimulationCore.EnvirData[x + 1, y].Height) != currentLevel) isContourEdge = true;
                    else if (y > 1 && GetAltitudeQuantizedLevel(SimulationCore.EnvirData[x, y - 1].Height) != currentLevel) isContourEdge = true;
                    else if (y < size && GetAltitudeQuantizedLevel(SimulationCore.EnvirData[x, y + 1].Height) != currentLevel) isContourEdge = true;

                    if (isContourEdge)
                        c = Color.Lerp(c, Color.black, SimulationConfig.AltitudeContourDarken);
                }
                else
                {
                    switch (env.Topography)
                    {
                        case 0: c = SimulationConfig.TerrainColorOcean; break;
                        case 2: c = SimulationConfig.TerrainColorBeach; break;
                        case 3: c = SimulationConfig.TerrainColorRiver; break;
                        case 4: c = SimulationConfig.TerrainColorLake; break;
                        default: c = SimulationConfig.TerrainColorLand; break;
                    }
                }

                pixels[(y - 1) * size + (x - 1)] = c;
            }
        }

        bgTexture.SetPixels32(pixels);
        bgTexture.Apply();
    }

    void UpdateBackgroundNormalTexture()
    {
        // 主线程瓶颈之一：O(EnvirSize² × macroRadius) 全分辨率 CPU 法线；仅在 bgNormalDirty 时重算
        if (SimulationCore.EnvirData == null || bgNormalTexture == null) return;

        int size = SimulationConfig.EnvirSize;
        Color32[] pixels = new Color32[size * size];
        float invAltitudeRange = 1f / Mathf.Max(1f, SimulationConfig.AltitudeMax - SimulationConfig.AltitudeMin);
        float normalStrength = SimulationConfig.TerrainNormalStrength;
        int slopeRadius = Mathf.Max(1, SimulationConfig.TerrainSlopeSampleRadius);
        int macroRadius = Mathf.Max(slopeRadius + 1, SimulationConfig.TerrainMacroReliefRadius);
        float seaLevel = SimulationConfig.AltitudeThreshold;

        for (int y = 1; y <= size; y++)
        {
            int yDown = WrapEnvirIndex(y - slopeRadius, size);
            int yUp = WrapEnvirIndex(y + slopeRadius, size);
            int yFarDown = WrapEnvirIndex(y - macroRadius, size);
            int yFarUp = WrapEnvirIndex(y + macroRadius, size);

            for (int x = 1; x <= size; x++)
            {
                int xLeft = WrapEnvirIndex(x - slopeRadius, size);
                int xRight = WrapEnvirIndex(x + slopeRadius, size);
                int xFarLeft = WrapEnvirIndex(x - macroRadius, size);
                int xFarRight = WrapEnvirIndex(x + macroRadius, size);

                Envir centerEnv = SimulationCore.EnvirData[x, y];
                float centerHeight = centerEnv.Height;
                bool isWater = centerEnv.Topography == 0 || centerEnv.Topography == 3 || centerEnv.Topography == 4;
                float slopeNoiseFloor = isWater
                    ? SimulationConfig.TerrainWaterSlopeNoiseFloor
                    : SimulationConfig.TerrainLandSlopeNoiseFloor;
                float reliefNoiseFloor = isWater
                    ? SimulationConfig.TerrainWaterReliefNoiseFloor
                    : SimulationConfig.TerrainLandReliefNoiseFloor;

                float leftHeight = SimulationCore.EnvirData[xLeft, y].Height;
                float rightHeight = SimulationCore.EnvirData[xRight, y].Height;
                float downHeight = SimulationCore.EnvirData[x, yDown].Height;
                float upHeight = SimulationCore.EnvirData[x, yUp].Height;
                float farLeftHeight = SimulationCore.EnvirData[xFarLeft, y].Height;
                float farRightHeight = SimulationCore.EnvirData[xFarRight, y].Height;
                float farDownHeight = SimulationCore.EnvirData[x, yFarDown].Height;
                float farUpHeight = SimulationCore.EnvirData[x, yFarUp].Height;

                float gradientX = ((rightHeight - leftHeight) / (2f * slopeRadius)) * invAltitudeRange * normalStrength;
                float gradientY = ((upHeight - downHeight) / (2f * slopeRadius)) * invAltitudeRange * normalStrength;
                gradientX = ApplySignalDeadZone(gradientX, slopeNoiseFloor);
                gradientY = ApplySignalDeadZone(gradientY, slopeNoiseFloor);
                Vector3 normal = new Vector3(-gradientX, -gradientY, 1f).normalized;

                float nearAverage = (leftHeight + rightHeight + downHeight + upHeight) * 0.25f;
                float farAverage = (farLeftHeight + farRightHeight + farDownHeight + farUpHeight) * 0.25f;
                float relief = ((centerHeight - nearAverage) * 0.35f + (centerHeight - farAverage) * 0.65f) * invAltitudeRange * 10f;
                relief = ApplySignalDeadZone(relief, reliefNoiseFloor);
                relief = Mathf.Clamp(relief, -1f, 1f);

                float signedHeight;
                if (centerHeight >= seaLevel)
                    signedHeight = Mathf.InverseLerp(seaLevel, SimulationConfig.AltitudeMax, centerHeight);
                else
                    signedHeight = -Mathf.InverseLerp(seaLevel, SimulationConfig.AltitudeMin, centerHeight);
                signedHeight = Mathf.Sign(signedHeight) * Mathf.Pow(Mathf.Abs(signedHeight), 0.22f);

                pixels[(y - 1) * size + (x - 1)] = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    signedHeight * 0.5f + 0.5f,
                    relief * 0.5f + 0.5f);
            }
        }

        bgNormalTexture.SetPixels32(pixels);
        bgNormalTexture.Apply();
        ApplyBackgroundLightingSettings();
        bgNormalDirty = false;
    }

    int GetAltitudeQuantizedLevel(int height)
    {
        float altitudeT = Mathf.InverseLerp(SimulationConfig.AltitudeMin, SimulationConfig.AltitudeMax, height);
        int quantizedLevel = Mathf.Clamp(
            Mathf.FloorToInt(altitudeT * SimulationConfig.AltitudeContourLevels),
            0,
            SimulationConfig.AltitudeContourLevels - 1);

        return quantizedLevel;
    }

    Color GetAltitudeContourColor(int height)
    {
        int quantizedLevel = GetAltitudeQuantizedLevel(height);
        int levelCount = Mathf.Max(2, SimulationConfig.AltitudeContourLevels);
        float bandMinT = quantizedLevel / (float)levelCount;
        float bandMaxT = (quantizedLevel + 1) / (float)levelCount;
        float bandCenterT = (bandMinT + bandMaxT) * 0.5f;

        return EvaluateAltitudeGradient(bandCenterT);
    }

    Color EvaluateAltitudeGradient(float altitudeT)
    {
        float seaLevelT = Mathf.InverseLerp(
            SimulationConfig.AltitudeMin,
            SimulationConfig.AltitudeMax,
            SimulationConfig.AltitudeThreshold);
        float coastT = Mathf.InverseLerp(
            SimulationConfig.AltitudeMin,
            SimulationConfig.AltitudeMax,
            SimulationConfig.AltitudeThreshold + SimulationConfig.AltitudeCoastBandWidth);

        float coastalPlainT = Mathf.Lerp(coastT, 1f, 0.12f);
        float lowlandT = Mathf.Lerp(coastT, 1f, 0.28f);
        float highlandT = Mathf.Lerp(coastT, 1f, 0.46f);
        float uplandT = Mathf.Lerp(coastT, 1f, 0.66f);
        float mountainT = Mathf.Lerp(coastT, 1f, 0.84f);

        if (altitudeT <= seaLevelT)
            return EvaluateAltitudeGradientSegment(altitudeT, 0f, seaLevelT,
                SimulationConfig.AltitudeColorDeepWater,
                SimulationConfig.AltitudeColorShallowWater);
        if (altitudeT <= coastT)
            return EvaluateAltitudeGradientSegment(altitudeT, seaLevelT, coastT,
                SimulationConfig.AltitudeColorShallowWater,
                SimulationConfig.AltitudeColorCoast);
        if (altitudeT <= coastalPlainT)
            return EvaluateAltitudeGradientSegment(altitudeT, coastT, coastalPlainT,
                SimulationConfig.AltitudeColorCoast,
                SimulationConfig.AltitudeColorCoastalPlain);
        if (altitudeT <= lowlandT)
            return EvaluateAltitudeGradientSegment(altitudeT, coastalPlainT, lowlandT,
                SimulationConfig.AltitudeColorCoastalPlain,
                SimulationConfig.AltitudeColorLowland);
        if (altitudeT <= highlandT)
            return EvaluateAltitudeGradientSegment(altitudeT, lowlandT, highlandT,
                SimulationConfig.AltitudeColorLowland,
                SimulationConfig.AltitudeColorHighland);
        if (altitudeT <= uplandT)
            return EvaluateAltitudeGradientSegment(altitudeT, highlandT, uplandT,
                SimulationConfig.AltitudeColorHighland,
                SimulationConfig.AltitudeColorUpland);
        if (altitudeT <= mountainT)
            return EvaluateAltitudeGradientSegment(altitudeT, uplandT, mountainT,
                SimulationConfig.AltitudeColorUpland,
                SimulationConfig.AltitudeColorMountain);

        return EvaluateAltitudeGradientSegment(altitudeT, mountainT, 1f,
            SimulationConfig.AltitudeColorMountain,
            SimulationConfig.AltitudeColorSnow);
    }

    Color EvaluateAltitudeGradientSegment(float altitudeT, float startT, float endT, Color startColor, Color endColor)
    {
        if (Mathf.Approximately(startT, endT))
            return endColor;

        float localT = Mathf.InverseLerp(startT, endT, altitudeT);
        return Color.Lerp(startColor, endColor, localT);
    }

    /// <summary>将温度/光照编码写入缓冲池槽的 CPU 像素。</summary>
    void WriteTempLightOverlayPixels(Color32[] pixels)
    {
        if (SimulationCore.EnvirData == null || pixels == null)
            return;

        int size = SimulationConfig.EnvirSize;
        int factor = Mathf.Max(1, overlayDownsampleFactor);
        int texSize = Mathf.Max(1, overlayTextureSize);
        byte overlayAlpha = (byte)Mathf.Clamp(Mathf.RoundToInt(SimulationConfig.OverlayAlpha * 255f), 0, 255);
        float tempEncodeMin = SimulationConfig.OverlayTempEncodeMin;
        float tempEncodeMax = SimulationConfig.OverlayTempEncodeMax;
        float lightMin = SimulationConfig.LightMin;
        float lightMax = SimulationConfig.LightMax;
        bool showTemp = (currentOverlayViewMode & OverlayViewMode.Temperature) != 0;
        bool showLight = (currentOverlayViewMode & OverlayViewMode.Light) != 0;

        for (int y = 0; y < texSize; y++)
        {
            int envY = 1 + y * factor + factor / 2;
            if (envY > size) envY = size;
            for (int x = 0; x < texSize; x++)
            {
                int envX = 1 + x * factor + factor / 2;
                if (envX > size) envX = size;
                Envir env = SimulationCore.EnvirData[envX, envY];
                byte r = 0;
                byte g = 0;
                if (showTemp)
                {
                    float tempK = TemperatureField.IsAllocated
                        ? TemperatureField.Get(envX, envY)
                        : env.Temp;
                    float tempC = SimulationCore.KelvinToCelsius(tempK);
                    float t01 = Mathf.InverseLerp(tempEncodeMin, tempEncodeMax, tempC);
                    r = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t01) * 255f), 0, 255);
                }
                if (showLight)
                {
                    float l01 = Mathf.InverseLerp(lightMin, lightMax, env.Light);
                    g = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(l01) * 255f), 0, 255);
                }
                pixels[y * texSize + x] = new Color32(r, g, 0, overlayAlpha);
            }
        }
    }

    void WriteChemicalOverlayPixels(OverlayBufferPool.Slot slot, bool showChem)
    {
        if (slot == null)
            return;

        if (!showChem || SimulationCore.EnvirData == null)
        {
            ClearChemicalPixels(slot.ChemicalPixels);
            return;
        }

        ChemicalOverlayRasterizer.Configure(slot.ChemicalPixels, chemicalTextureSize, chemicalDownsampleFactor);
        ChemicalOverlayRasterizer.Rebuild(SimulationCore.EnvirData);
    }

    static void ClearChemicalPixels(Color32[] pixels)
    {
        if (pixels == null)
            return;
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);
    }

    void UploadOverlaySlotTextures(OverlayBufferPool.Slot slot)
    {
        if (slot == null)
            return;

        slot.TempLightTexture.SetPixels32(slot.TempLightPixels);
        slot.TempLightTexture.Apply(false);
        slot.ChemicalTexture.SetPixels32(slot.ChemicalPixels);
        slot.ChemicalTexture.Apply(false);
    }

    void CompositeOverlayToSlot(OverlayBufferPool.Slot slot)
    {
        if (compositeBlitMaterial == null || slot == null || slot.CompositeTexture == null)
            return;

        compositeBlitMaterial.SetTexture("_TempLightTex", slot.TempLightTexture);
        compositeBlitMaterial.SetTexture("_ChemTex", slot.ChemicalTexture);
        ApplyCompositeBlitSettings();

        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(Texture2D.whiteTexture, slot.CompositeTexture, compositeBlitMaterial);
        RenderTexture.active = previous;
    }

    /// <summary>模拟步推进时：写入缓冲池后备槽并发布为显示槽。</summary>
    void RebuildOverlayBufferForStep(long currentStep)
    {
        if (overlayBufferPool == null)
            return;

        OverlayBufferPool.Slot slot = overlayBufferPool.AcquireWriteSlot();
        if (slot == null)
            return;

        bool showTemp = (currentOverlayViewMode & OverlayViewMode.Temperature) != 0;
        bool showLight = (currentOverlayViewMode & OverlayViewMode.Light) != 0;
        bool showChem = (currentOverlayViewMode & OverlayViewMode.Chemical) != 0
            && ChemistrySystem.ChemicalOverlayMask != 0;

        if (showTemp || showLight)
            WriteTempLightOverlayPixels(slot.TempLightPixels);
        else
        {
            for (int i = 0; i < slot.TempLightPixels.Length; i++)
                slot.TempLightPixels[i] = new Color32(0, 0, 0, 0);
        }

        WriteChemicalOverlayPixels(slot, showChem);
        UploadOverlaySlotTextures(slot);
        CompositeOverlayToSlot(slot);

        overlayBufferPool.CommitWrite(
            slot,
            currentStep,
            currentOverlayViewMode,
            ChemistrySystem.ChemicalOverlayMask);
        lastOverlayRenderedStep = currentStep;
        lastOverlayViewMode = currentOverlayViewMode;
    }

    bool ShouldRebuildOverlayForStep(long currentStep)
    {
        int stepInterval = Mathf.Max(1, SimulationConfig.OverlayUpdateStepInterval);
        return !overlayRebuildInProgress
            && currentStep > lastOverlayRenderedStep
            && currentStep - lastOverlayRenderedStep >= stepInterval;
    }

    bool ShouldDeferOverlayRebuild(bool isPanning)
    {
        return SimulationConfig.DeferOverlayRebuildWhilePanning && isPanning;
    }

    void TryScheduleOverlayRebuild(long step)
    {
        if (step < 0 || overlayRebuildInProgress)
            return;

        StartCoroutine(RebuildOverlayCoroutine(step));
    }

    IEnumerator RebuildOverlayCoroutine(long step)
    {
        overlayRebuildInProgress = true;

        int deferFrames = Mathf.Max(0, SimulationConfig.OverlayRebuildDeferFrames);
        for (int i = 0; i < deferFrames; i++)
            yield return null;

        while (ShouldDeferOverlayRebuild(cameraController != null && cameraController.IsPanning))
            yield return null;

        RebuildOverlayBufferForStep(step);
        overlayRebuildInProgress = false;
        pendingOverlayStep = -1;
    }

    void UpdateOverlayForCurrentStep(bool isPanning)
    {
        long currentStep = SimulationCore.totalSteps;
        int stepInterval = Mathf.Max(1, SimulationConfig.OverlayUpdateStepInterval);
        bool stepDue = !overlayRebuildInProgress
            && currentStep > lastOverlayRenderedStep
            && currentStep - lastOverlayRenderedStep >= stepInterval;

        if (stepDue)
            pendingOverlayStep = System.Math.Max(pendingOverlayStep, currentStep);

        if (pendingOverlayStep < 0 || overlayRebuildInProgress)
            return;

        if (ShouldDeferOverlayRebuild(isPanning))
            return;

        TryScheduleOverlayRebuild(pendingOverlayStep);
    }

    void LateUpdate()
    {
        if (SimulationCore.EnvirData == null || cameraController == null) return;

        if (bgNormalDirty)
            UpdateBackgroundNormalTexture();

        if (normalLightingEnabled != lastNormalLightingEnabled ||
            (terrainLightDirection - lastTerrainLightDirection).sqrMagnitude > 0.000001f)
        {
            ApplyBackgroundLightingSettings();
            lastNormalLightingEnabled = normalLightingEnabled;
            lastTerrainLightDirection = terrainLightDirection;
        }

        if (currentBaseViewMode != lastBaseViewMode)
        {
            UpdateBackgroundTexture();
            lastBaseViewMode = currentBaseViewMode;
        }

        // 始终渲染地形底图
        if (bgMesh != null && bgMaterial != null)
            Graphics.DrawMesh(bgMesh, Matrix4x4.identity, bgMaterial, 0);

        // 叠加层：仅在模拟步前进时写入缓冲池；每帧只绘制池中显示槽（不扫 EnvirData）
        bool showTemp = (currentOverlayViewMode & OverlayViewMode.Temperature) != 0;
        bool showLight = (currentOverlayViewMode & OverlayViewMode.Light) != 0;
        bool showChem = (currentOverlayViewMode & OverlayViewMode.Chemical) != 0
            && ChemistrySystem.ChemicalOverlayMask != 0;
        bool showAnyOverlay = showTemp || showLight || showChem;

        bool isPanning = cameraController.IsPanning;
        if (showAnyOverlay)
        {
            UpdateOverlayForCurrentStep(isPanning);

            if (overlayBufferPool != null && overlayBufferPool.HasDisplayBuffer
                && overlayMesh != null && compositeDisplayMaterial != null)
            {
                compositeDisplayMaterial.mainTexture = overlayBufferPool.DisplayComposite;
                Graphics.DrawMesh(overlayMesh, Matrix4x4.identity, compositeDisplayMaterial, 0);
            }
        }
        else
        {
            lastOverlayViewMode = OverlayViewMode.None;
        }

        int minX, maxX, minY, maxY;
        cameraController.GetVisibleGridRange(out minX, out maxX, out minY, out maxY);

        int visibleGrids = (maxX - minX + 1) * (maxY - minY + 1);
        // 可见格过多时只画每格最高优先级细胞，避免 O(visibleGrids × CellNum) 的 DrawMesh 调用
        bool optimizedMode = visibleGrids > SimulationConfig.GridOptimizeThreshold;
        if (cameraController.IsPanning && SimulationConfig.ForceOptimizedRenderWhilePanning)
            optimizedMode = true;

        RenderCells(minX, maxX, minY, maxY, visibleGrids, optimizedMode);

        // 缓存网格线范围供 OnRenderObject 使用；平移时跳过 GL 线段以减轻主线程负担
        drawGrid = visibleGrids < SimulationConfig.GridDrawThreshold;
        if (cameraController.IsPanning && SimulationConfig.SkipGridLinesWhilePanning)
            drawGrid = false;
        cachedMinX = minX; cachedMaxX = maxX;
        cachedMinY = minY; cachedMaxY = maxY;
    }

    void OnRenderObject()
    {
        if (drawGrid && lineMaterial != null)
        {
            DrawGridLines(cachedMinX, cachedMaxX, cachedMinY, cachedMaxY);
        }
    }

    /// <summary>
    /// 渲染可见区域内的细胞。DrawMeshInstanced 每批最多 MAX_BATCH(1023) 个实例。
    /// </summary>
    void RenderCells(int minX, int maxX, int minY, int maxY, int visibleGrids, bool optimizedMode)
    {
        float ppe = SimulationConfig.PixelPerEnvir;
        int listCapacity = Mathf.Max(256, lastRenderedMatrixCount);
        EnsureMatrixListCapacity(listCapacity);
        playerMatrices.Clear();
        npcMatrices.Clear();
        playerDimMatrices.Clear();
        npcDimMatrices.Clear();

        // 远距离优化模式：可见格常为百万级空格子，逐格扫 EnvirData 极慢；改扫 AllCells 并视口裁剪
        int cellListThreshold = SimulationConfig.CellListRenderThreshold;
        bool useCellListPath = optimizedMode && visibleGrids > cellListThreshold;

        if (useCellListPath)
        {
            RenderCellsFromCellList(minX, maxX, minY, maxY, ppe);
        }
        else
        {
            Envir[,] envirData = SimulationCore.EnvirData;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Envir env = envirData[x, y];
                    if (env == null || env.CellNum == 0) continue;

                    if (optimizedMode)
                    {
                        Cell best = env.GetHighestPriorityCell();
                        if (best == null) continue;

                        float worldX = (x - 0.5f) * ppe;
                        float worldY = (y - 0.5f) * ppe;
                        float scale = ppe * 0.8f;

                        Matrix4x4 m = Matrix4x4.TRS(
                            new Vector3(worldX, worldY, 0),
                            Quaternion.identity,
                            new Vector3(scale, scale, 1));

                        AddCellRenderMatrix(best, m);
                    }
                    else
                    {
                        RenderCellsInGrid(env, x, y, ppe);
                    }
                }
            }
        }

        // 先渲染半透明细胞，再渲染高亮细胞
        FlushBatches(playerDimMatrices, playerDimMaterial);
        FlushBatches(npcDimMatrices, npcDimMaterial);
        FlushBatches(playerMatrices, playerMaterial);
        FlushBatches(npcMatrices, npcMaterial);

        lastRenderedMatrixCount = playerMatrices.Count + npcMatrices.Count
            + playerDimMatrices.Count + npcDimMatrices.Count;
    }

    void EnsureMatrixListCapacity(int capacity)
    {
        if (playerMatrices.Capacity < capacity) playerMatrices.Capacity = capacity;
        if (npcMatrices.Capacity < capacity) npcMatrices.Capacity = capacity;
        if (playerDimMatrices.Capacity < capacity) playerDimMatrices.Capacity = capacity;
        if (npcDimMatrices.Capacity < capacity) npcDimMatrices.Capacity = capacity;
    }

    /// <summary>远距离优化路径：只遍历存活细胞，跳过大量空环境格。</summary>
    void RenderCellsFromCellList(int minX, int maxX, int minY, int maxY, float ppe)
    {
        viewportBestCells.Clear();
        List<Cell> allCells = SimulationCore.AllCells;
        if (allCells == null)
            return;

        for (int i = 0; i < allCells.Count; i++)
        {
            Cell cell = allCells[i];
            if (cell == null || !cell.alive)
                continue;
            if (cell.px < minX || cell.px > maxX || cell.py < minY || cell.py > maxY)
                continue;

            long key = ((long)cell.px << 21) | (uint)cell.py;
            if (!viewportBestCells.TryGetValue(key, out Cell best) || cell.priority > best.priority)
                viewportBestCells[key] = cell;
        }

        float scale = ppe * 0.8f;
        foreach (Cell cell in viewportBestCells.Values)
        {
            float worldX = (cell.px - 0.5f) * ppe;
            float worldY = (cell.py - 0.5f) * ppe;
            Matrix4x4 m = Matrix4x4.TRS(
                new Vector3(worldX, worldY, 0),
                Quaternion.identity,
                new Vector3(scale, scale, 1));
            AddCellRenderMatrix(cell, m);
        }
    }

    /// <summary>
    /// 按基因视图筛选状态，将细胞矩阵加入对应批次。
    /// </summary>
    void AddCellRenderMatrix(Cell cell, Matrix4x4 matrix)
    {
        bool dim = geneFilterBaseId > 0 && !cell.HasGeneBaseId(geneFilterBaseId);
        if (cell.isPlayer)
        {
            if (dim) playerDimMatrices.Add(matrix);
            else playerMatrices.Add(matrix);
        }
        else
        {
            if (dim) npcDimMatrices.Add(matrix);
            else npcMatrices.Add(matrix);
        }
    }

    /// <summary>
    /// 在单个环境格内排列并渲染所有细胞
    /// 细胞表现为圆形，优先级越大圆形越大，等比缩放使其恰好塞满方格
    /// </summary>
    void RenderCellsInGrid(Envir env, int gx, int gy, float ppe)
    {
        int n = env.CellNum;
        if (n == 0) return;

        int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
        int rows = Mathf.CeilToInt((float)n / cols);

        float cellWidth = ppe / cols;
        float cellHeight = ppe / rows;
        float baseSize = Mathf.Min(cellWidth, cellHeight);

        int maxPriority = 1;
        for (int i = 1; i <= n; i++)
        {
            if (env.CellList[i] != null && env.CellList[i].priority > maxPriority)
                maxPriority = env.CellList[i].priority;
        }

        float originX = (gx - 1) * ppe;
        float originY = (gy - 1) * ppe;

        int idx = 0;
        for (int i = 1; i <= n; i++)
        {
            Cell cell = env.CellList[i];
            if (cell == null) continue;

            int col = idx % cols;
            int row = idx / cols;

            float cx = originX + (col + 0.5f) * cellWidth;
            float cy = originY + (row + 0.5f) * cellHeight;

            float priorityRatio = (float)cell.priority / maxPriority;
            float scale = baseSize * (0.4f + 0.55f * priorityRatio);

            Matrix4x4 m = Matrix4x4.TRS(
                new Vector3(cx, cy, 0),
                Quaternion.identity,
                new Vector3(scale, scale, 1));

            AddCellRenderMatrix(cell, m);

            idx++;
        }
    }

    /// <summary>按 CellRenderMaxBatch 分批调用 DrawMeshInstanced，将 Draw Call 从 N 降至 ceil(N/1023)。</summary>
    void FlushBatches(List<Matrix4x4> matricesList, Material mat)
    {
        int total = matricesList.Count;
        if (total == 0 || mat == null) return;

        int maxBatch = SimulationConfig.CellRenderMaxBatch;
        if (batchBuffer == null || batchBuffer.Length < maxBatch)
            batchBuffer = new Matrix4x4[Mathf.Max(1, maxBatch)];

        for (int offset = 0; offset < total; offset += maxBatch)
        {
            int count = Mathf.Min(maxBatch, total - offset);
            for (int i = 0; i < count; i++)
                batchBuffer[i] = matricesList[offset + i];
            Graphics.DrawMeshInstanced(quadMesh, 0, mat, batchBuffer, count);
        }
    }

    /// <summary>
    /// 绘制环境格网格线
    /// </summary>
    void DrawGridLines(int minX, int maxX, int minY, int maxY)
    {
        float ppe = SimulationConfig.PixelPerEnvir;

        GL.PushMatrix();
        lineMaterial.SetPass(0);
        GL.Begin(GL.LINES);
        GL.Color(GridLineColor);

        // 竖线
        for (int x = minX; x <= maxX + 1; x++)
        {
            float wx = (x - 1) * ppe;
            GL.Vertex3(wx, (minY - 1) * ppe, 0);
            GL.Vertex3(wx, maxY * ppe, 0);
        }

        // 横线
        for (int y = minY; y <= maxY + 1; y++)
        {
            float wy = (y - 1) * ppe;
            GL.Vertex3((minX - 1) * ppe, wy, 0);
            GL.Vertex3(maxX * ppe, wy, 0);
        }

        GL.End();
        GL.PopMatrix();
    }
}
