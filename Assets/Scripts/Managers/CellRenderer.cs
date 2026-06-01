// CellRenderer.cs - 细胞渲染器，分批DrawMeshInstanced
using UnityEngine;
using System.Collections.Generic;

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

    // 渲染批次
    private const int MAX_BATCH = 1023;
    private List<Matrix4x4> playerMatrices = new List<Matrix4x4>();
    private List<Matrix4x4> npcMatrices = new List<Matrix4x4>();
    private List<Matrix4x4> playerDimMatrices = new List<Matrix4x4>();
    private List<Matrix4x4> npcDimMatrices = new List<Matrix4x4>();

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

    // 叠加层（温度/光照热力图）
    private Texture2D overlayTexture;
    private Material overlayMaterial;
    private Mesh overlayMesh;
    private bool overlayDirty = true;
    private long lastOverlayStep = -1;
    private float lastOverlayUpdateTime = 0f;
    private Color32[] overlayPixels;
    private int overlayTextureSize;
    private int overlayDownsampleFactor = 1;

    // 化学物质叠加层（CPU 预计算颜色）
    private Texture2D chemicalOverlayTexture;
    private Material chemicalOverlayMaterial;
    private Color32[] chemicalOverlayPixels;
    private bool chemicalOverlayDirty = true;
    private int lastChemicalOverlayMask = 0;
    private long lastChemicalOverlayRevision = -1;
    private float lastChemicalOverlayUpdateTime = 0f;

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
        CreateOverlay();
        CreateChemicalOverlay();
    }

    public static void InvalidateChemicalOverlay()
    {
        if (instance != null)
            instance.chemicalOverlayDirty = true;
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
        Shader shader = Shader.Find("Sprites/Default");

        playerMaterial = new Material(shader);
        playerMaterial.mainTexture = circleTexture;
        playerMaterial.color = PlayerColor;

        npcMaterial = new Material(shader);
        npcMaterial.mainTexture = circleTexture;
        npcMaterial.color = NPCColor;

        Color playerDim = PlayerColor;
        playerDim.a = SimulationConfig.GeneViewDimAlpha;
        playerDimMaterial = new Material(shader);
        playerDimMaterial.mainTexture = circleTexture;
        playerDimMaterial.color = playerDim;

        Color npcDim = NPCColor;
        npcDim.a = SimulationConfig.GeneViewDimAlpha;
        npcDimMaterial = new Material(shader);
        npcDimMaterial.mainTexture = circleTexture;
        npcDimMaterial.color = npcDim;
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

    void ApplyOverlayMaterialSettings()
    {
        if (overlayMaterial == null) return;
        bool showTemp = (currentOverlayViewMode & OverlayViewMode.Temperature) != 0;
        bool showLight = (currentOverlayViewMode & OverlayViewMode.Light) != 0;
        if (overlayMaterial.HasProperty("_ShowTemp"))
            overlayMaterial.SetFloat("_ShowTemp", showTemp ? 1f : 0f);
        if (overlayMaterial.HasProperty("_ShowLight"))
            overlayMaterial.SetFloat("_ShowLight", showLight ? 1f : 0f);

        if (overlayMaterial.HasProperty("_TempBlue"))
            overlayMaterial.SetColor("_TempBlue", SimulationConfig.TempColorBlue);
        if (overlayMaterial.HasProperty("_TempCyan"))
            overlayMaterial.SetColor("_TempCyan", SimulationConfig.TempColorCyan);
        if (overlayMaterial.HasProperty("_TempGreen"))
            overlayMaterial.SetColor("_TempGreen", SimulationConfig.TempColorGreen);
        if (overlayMaterial.HasProperty("_TempYellow"))
            overlayMaterial.SetColor("_TempYellow", SimulationConfig.TempColorYellow);
        if (overlayMaterial.HasProperty("_TempOrange"))
            overlayMaterial.SetColor("_TempOrange", SimulationConfig.TempColorOrange);
        if (overlayMaterial.HasProperty("_TempRed"))
            overlayMaterial.SetColor("_TempRed", SimulationConfig.TempColorRed);

        if (overlayMaterial.HasProperty("_TempBlueMax"))
            overlayMaterial.SetFloat("_TempBlueMax", SimulationConfig.TempColorBlueMax);
        if (overlayMaterial.HasProperty("_TempCyanMax"))
            overlayMaterial.SetFloat("_TempCyanMax", SimulationConfig.TempColorCyanMax);
        if (overlayMaterial.HasProperty("_TempGreenMax"))
            overlayMaterial.SetFloat("_TempGreenMax", SimulationConfig.TempColorGreenMax);
        if (overlayMaterial.HasProperty("_TempYellowMax"))
            overlayMaterial.SetFloat("_TempYellowMax", SimulationConfig.TempColorYellowMax);
        if (overlayMaterial.HasProperty("_TempOrangeMin"))
            overlayMaterial.SetFloat("_TempOrangeMin", SimulationConfig.TempColorOrangeMin);
        if (overlayMaterial.HasProperty("_TempOrangeMax"))
            overlayMaterial.SetFloat("_TempOrangeMax", SimulationConfig.TempColorOrangeMax);
        if (overlayMaterial.HasProperty("_TempRedMin"))
            overlayMaterial.SetFloat("_TempRedMin", SimulationConfig.TempColorRedMin);

        if (overlayMaterial.HasProperty("_TempEncodeMin"))
            overlayMaterial.SetFloat("_TempEncodeMin", SimulationConfig.OverlayTempEncodeMin);
        if (overlayMaterial.HasProperty("_TempEncodeMax"))
            overlayMaterial.SetFloat("_TempEncodeMax", SimulationConfig.OverlayTempEncodeMax);

        if (overlayMaterial.HasProperty("_LightDark"))
            overlayMaterial.SetColor("_LightDark", SimulationConfig.LightColorDark);
        if (overlayMaterial.HasProperty("_LightBright"))
            overlayMaterial.SetColor("_LightBright", SimulationConfig.LightColorBright);
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
        instance.overlayDirty = true;
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

    void CreateOverlay()
    {
        int size = SimulationConfig.EnvirSize;
        overlayDownsampleFactor = Mathf.Max(1, SimulationConfig.OverlayDownsampleFactor);
        overlayTextureSize = Mathf.Max(1, size / overlayDownsampleFactor);
        overlayTexture = new Texture2D(overlayTextureSize, overlayTextureSize, TextureFormat.RGBA32, false);
        overlayTexture.filterMode = FilterMode.Bilinear; // 虚化效果
        overlayTexture.wrapMode = TextureWrapMode.Clamp;
        overlayPixels = new Color32[overlayTextureSize * overlayTextureSize];

        Shader shader = Shader.Find("Custom/OverlayColorMap");
        if (shader == null)
        {
            Debug.LogWarning("OverlayColorMap shader not found, falling back to Sprites/Default.");
            shader = Shader.Find("Sprites/Default");
        }
        overlayMaterial = new Material(shader);
        overlayMaterial.mainTexture = overlayTexture;
        overlayMaterial.color = Color.white;
        ApplyOverlayMaterialSettings();

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

    void CreateChemicalOverlay()
    {
        int size = SimulationConfig.EnvirSize;
        int factor = Mathf.Max(1, overlayDownsampleFactor);
        int texSize = Mathf.Max(1, size / factor);
        chemicalOverlayTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        chemicalOverlayTexture.filterMode = FilterMode.Bilinear;
        chemicalOverlayTexture.wrapMode = TextureWrapMode.Clamp;
        chemicalOverlayPixels = new Color32[texSize * texSize];

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        chemicalOverlayMaterial = new Material(shader);
        chemicalOverlayMaterial.mainTexture = chemicalOverlayTexture;
        chemicalOverlayMaterial.color = Color.white;
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

    void UpdateOverlayTexture()
    {
        if (SimulationCore.EnvirData == null) return;
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
                    float tempC = SimulationCore.KelvinToCelsius(env.Temp);
                    float t01 = Mathf.InverseLerp(tempEncodeMin, tempEncodeMax, tempC);
                    r = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t01) * 255f), 0, 255);
                }
                if (showLight)
                {
                    float l01 = Mathf.InverseLerp(lightMin, lightMax, env.Light);
                    g = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(l01) * 255f), 0, 255);
                }
                overlayPixels[y * texSize + x] = new Color32(r, g, 0, overlayAlpha);
            }
        }

        overlayTexture.SetPixels32(overlayPixels);
        overlayTexture.Apply();
        overlayDirty = false;
    }

    void UpdateChemicalOverlayTexture()
    {
        if (SimulationCore.EnvirData == null)
            return;

        int mask = ChemistrySystem.ChemicalOverlayMask;
        if (mask == 0)
            return;

        int size = SimulationConfig.EnvirSize;
        int factor = Mathf.Max(1, overlayDownsampleFactor);
        int texSize = Mathf.Max(1, overlayTextureSize);
        byte overlayAlpha = (byte)Mathf.Clamp(Mathf.RoundToInt(SimulationConfig.OverlayAlpha * 255f), 0, 255);

        for (int y = 0; y < texSize; y++)
        {
            int envY = 1 + y * factor + factor / 2;
            if (envY > size) envY = size;
            for (int x = 0; x < texSize; x++)
            {
                int envX = 1 + x * factor + factor / 2;
                if (envX > size) envX = size;
                Envir env = SimulationCore.EnvirData[envX, envY];

                Color rgb = Color.black;
                float weightSum = 0f;
                float maxIntensity = 0f;
                ChemicalSubstance[] substances = ChemistrySystem.DefaultOrder;
                for (int i = 0; i < substances.Length; i++)
                {
                    ChemicalSubstance substance = substances[i];
                    int bit = 1 << (int)substance;
                    if ((mask & bit) == 0)
                        continue;

                    float norm = ChemistrySystem.NormalizeOverlayAmount(substance, env.GetChemicalAmount(substance));
                    if (norm <= 0f)
                        continue;

                    Color substanceColor = ChemistrySystem.GetSubstanceColor(substance);
                    rgb += substanceColor * norm;
                    weightSum += norm;
                    if (norm > maxIntensity)
                        maxIntensity = norm;
                }

                Color32 pixel;
                if (weightSum > 0f)
                {
                    rgb /= weightSum;
                    byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(maxIntensity * overlayAlpha), 0, 255);
                    pixel = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(rgb.r * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(rgb.g * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(rgb.b * 255f), 0, 255),
                        alpha);
                }
                else
                {
                    pixel = new Color32(0, 0, 0, 0);
                }

                chemicalOverlayPixels[y * texSize + x] = pixel;
            }
        }

        chemicalOverlayTexture.SetPixels32(chemicalOverlayPixels);
        chemicalOverlayTexture.Apply();
        chemicalOverlayDirty = false;
        lastChemicalOverlayMask = mask;
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

        // 温度/光照模式：渲染半透明叠加层
        bool showTempLight = (currentOverlayViewMode & (OverlayViewMode.Temperature | OverlayViewMode.Light)) != 0;
        if (showTempLight)
        {
            if (currentOverlayViewMode != lastOverlayViewMode)
            {
                overlayDirty = true;
                lastOverlayViewMode = currentOverlayViewMode;
            }
            ApplyOverlayMaterialSettings();
            long currentStep = SimulationCore.GetResearchStepCounter();
            int stepInterval = Mathf.Max(1, SimulationConfig.OverlayUpdateStepInterval);
            float minInterval = Mathf.Max(0f, SimulationConfig.OverlayUpdateMinIntervalSeconds);
            if (currentStep - lastOverlayStep >= stepInterval)
            {
                overlayDirty = true;
                lastOverlayStep = currentStep;
            }

            float now = Time.realtimeSinceStartup;
            if (overlayDirty && (minInterval <= 0f || now - lastOverlayUpdateTime >= minInterval))
            {
                UpdateOverlayTexture();
                lastOverlayUpdateTime = now;
            }
            if (overlayMesh != null && overlayMaterial != null)
                Graphics.DrawMesh(overlayMesh, Matrix4x4.identity, overlayMaterial, 0);
        }

        // 化学物质叠加层：可与温度/光照叠加显示
        bool showChemical = (currentOverlayViewMode & OverlayViewMode.Chemical) != 0;
        if (showChemical)
        {
            int currentMask = ChemistrySystem.ChemicalOverlayMask;
            if (currentMask != lastChemicalOverlayMask)
                chemicalOverlayDirty = true;

            long currentRevision = ChemistrySystem.GetChemicalRevision();
            float minInterval = Mathf.Max(0f, SimulationConfig.OverlayUpdateMinIntervalSeconds);
            if (currentRevision != lastChemicalOverlayRevision)
            {
                chemicalOverlayDirty = true;
                lastChemicalOverlayRevision = currentRevision;
            }

            float now = Time.realtimeSinceStartup;
            if (chemicalOverlayDirty && (minInterval <= 0f || now - lastChemicalOverlayUpdateTime >= minInterval))
            {
                UpdateChemicalOverlayTexture();
                lastChemicalOverlayUpdateTime = now;
            }

            if (overlayMesh != null && chemicalOverlayMaterial != null && currentMask != 0)
                Graphics.DrawMesh(overlayMesh, Matrix4x4.identity, chemicalOverlayMaterial, 0);
        }
        else
        {
            lastChemicalOverlayMask = 0;
            lastChemicalOverlayRevision = -1;
        }

        if (!showTempLight && !showChemical)
        {
            lastOverlayViewMode = OverlayViewMode.None;
        }

        int minX, maxX, minY, maxY;
        cameraController.GetVisibleGridRange(out minX, out maxX, out minY, out maxY);

        int visibleGrids = (maxX - minX + 1) * (maxY - minY + 1);
        bool optimizedMode = visibleGrids > SimulationConfig.GridOptimizeThreshold;

        RenderCells(minX, maxX, minY, maxY, optimizedMode);

        // 缓存网格线范围供OnRenderObject使用
        drawGrid = visibleGrids < 2500;
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
    /// 渲染可见区域内的细胞
    /// </summary>
    void RenderCells(int minX, int maxX, int minY, int maxY, bool optimizedMode)
    {
        float ppe = SimulationConfig.PixelPerEnvir;
        playerMatrices.Clear();
        npcMatrices.Clear();
        playerDimMatrices.Clear();
        npcDimMatrices.Clear();

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Envir env = SimulationCore.GetEnvir(x, y);
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

        // 先渲染半透明细胞，再渲染高亮细胞
        FlushBatches(playerDimMatrices, playerDimMaterial);
        FlushBatches(npcDimMatrices, npcDimMaterial);
        FlushBatches(playerMatrices, playerMaterial);
        FlushBatches(npcMatrices, npcMaterial);
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

    void FlushBatches(List<Matrix4x4> matricesList, Material mat)
    {
        if (matricesList.Count == 0) return;

        for (int i = 0; i < matricesList.Count; i++)
        {
            Graphics.DrawMesh(quadMesh, matricesList[i], mat, 0);
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
