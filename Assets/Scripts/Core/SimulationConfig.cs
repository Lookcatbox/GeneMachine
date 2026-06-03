// SimulationConfig.cs - 全局配置
using UnityEngine;

public static class SimulationConfig
{
    public static int CellMaxNum = 20;           // 每个环境格能容纳细胞的最大数
    public static int CellTotalMaxNum = 2000000; // 全局细胞总数最大值
    public static int GeneNum = 1000;            // 基因种类数上限
    public static int WorldSeed = 10000000;         // 世界种子
    public static int EnvirSize = 2000;          // 环境格总大小 EnvirSize x EnvirSize
    public static float PixelPerEnvir = 1.0f;    // 每个环境格对应的世界单位大小

    // 物种基因槽位
    public static int MaxMainGene = 50;           // 主干基因槽位数
    public static int MaxSubGene = 100;           // 自由基因槽位数

    // 初始参数
    public static int InitialEnergy = 200;       // 细胞初始能量
    public static int InitialPlayerCells = 50;   // 初始玩家细胞数
    public static int InitialNPCClusters = 20;   // 初始NPC群落数
    public static int InitialNPCPerCluster = 30; // 每个NPC群落细胞数

    // 行为编号 (Behavior ID)
    public static int BehaviorMultiply = 1;
    public static int BehaviorTemperature = 2;
    public static int BehaviorLight = 3;
    public static int BehaviorDeath = 4;

    // 环境默认值
    public static int DefaultTemp = 27;          // 默认温度
    public static int DefaultLight = 50;         // 默认光照
    public static float KelvinOffset = 273.15f;  // 摄氏度转开尔文偏移
    public static int AltitudeThreshold = 0;            // 海平面高度

    // 热扩散与光照简易更新
    public static int LightUpdateValue = 50;     // 每回合光照固定值(0-100)
    public static float LightNoiseScale = 0.002f; // 光照噪声空间缩放
    public static float LightNoiseZStep = 1.0f;   // 每回合z切片步进
    public static int LightNoiseOctaves = 3;      // 光照噪声层数
    public static float LightNoiseFrequencyMultiplier = 1.8f; // 频率递增倍数
    public static float LightNoiseWeightDecay = 0.6f;         // 权重递减系数
    public static float LightLatitudeMinMultiplier = 0.30f;   // 极地光照倍率
    public static float LightLatitudeMaxMultiplier = 1.00f;   // 赤道光照倍率
    public static float LightLatitudeAxialTiltDegrees = 23.44f; // 地轴倾角（影响年均日照角曲线）
    public static float HeatLightGainAtFull = 14f; // 100%光照时温度增量
    public static float HeatLossLand = 0.02f;    // 陆地温度流失比例（基准，效率=100%时）
    public static float HeatDiffusionStrength = 0.5f; // 热扩散强度
    public static float HeatLossEfficiencyTempMin = -273.15f;   // 绝对零度（摄氏）
    public static float HeatLossEfficiencyTempZero = -30f;        // 低温锚点（摄氏）
    public static float HeatLossEfficiencyTempMax = 35f;        // 高温锚点（摄氏）
    public static float HeatLossEfficiencyAtZero = 0.10f;       // 0°C效率
    public static float HeatLossEfficiencyAtMax = 1.00f;        // 30°C效率
    public static float HeatLossEfficiencyMidCurvePower = 1.4f; // 0-30°C非线性
    public static float HeatLossEfficiencyLowCurvePower = 0.7f; // 0°C以下非线性
    public static float HeatConductionLand = 1.00f;  // 陆地热传导效率
    public static float HeatConductionSand = 0.75f;  // 沙滩热传导效率
    public static float HeatConductionWater = 0.50f; // 水域热传导效率
    public static float ChemicalGasDiffusionStrength = 0.35f; // 气体物质扩散强度
    public static float ChemicalLiquidDiffusionStrength = 0.20f; // 液体/溶质仅在水域中的扩散强度

    // 调试：温度统计
    public static bool DebugTempStatsEnabled = false; // 是否输出温度统计
    public static int DebugTempStatsInterval = 100;   // 间隔多少步输出一次
    public static int DebugTempStatsStride = 20;      // 采样步长，越大越快

    // 基础温度耐受（用于研发升级）
    public static int BaseTempToleranceMin = 25;
    public static int BaseTempToleranceMax = 30;
    public static int ResearchTempUpgradeMaxLevel = 5;
    public static int ResearchTempUpgradeBaseCost = 10000;
    public static float ResearchTempUpgradeGrowth = 1.5f;

    public static int AltitudeBeach = 100; //若非海，且低于沙滩高度(海平面+100)，则地形为沙滩。

    // 视野优化阈值
    public static int GridOptimizeThreshold = 10000; // 超过此数量的可见格子时进入优化渲染

    // 地形生成
        public static float NoiseBaseScale = 0.00150f;       // 最低频底形噪声，决定大陆级轮廓
        public static int NoiseLayerCount = 10;               // 低频到高频共叠加几层
        public static float NoiseFrequencyMultiplier = 1.8f; // 每层频率递增倍数
        public static float NoiseWeightDecay = 0.7f;         // 每层权重递减系数

    // 河流生成
    public static int RiverSourceCount = 300;       // 随机水源数量
    public static int RiverMaxLength = 2000;        // 单条河流最大追踪步数
    public static int RiverMinLength = 15;          // 路径太短不算河流
    public static int RiverBaseWidth = 2;           // 初始河流宽度（格）
    public static int RiverWidenPerFlow = 3;        // 每多几条汇入增加1格宽度

    // 地形视图颜色
    public static Color TerrainColorOcean = new Color(0.05f, 0.12f, 0.55f, 1f);
    public static Color TerrainColorLand = new Color(0.55f, 0.38f, 0.18f, 1f);
    public static Color TerrainColorBeach = new Color(0.93f, 0.87f, 0.58f, 1f);
    public static Color TerrainColorRiver = new Color(0.30f, 0.60f, 0.90f, 1f);
    public static Color TerrainColorLake = new Color(0.15f, 0.35f, 0.70f, 1f);

    // 温度视图颜色
    public static Color TempColorBlue = new Color(0.0f, 0.2f, 0.9f, 1f);
    public static Color TempColorCyan = new Color(0.0f, 0.8f, 0.85f, 1f);
    public static Color TempColorGreen = new Color(0.15f, 0.75f, 0.2f, 1f);
    public static Color TempColorYellow = new Color(0.95f, 0.90f, 0.15f, 1f);
    public static Color TempColorOrange = new Color(0.98f, 0.55f, 0.10f, 1f);
    public static Color TempColorRed = new Color(0.9f, 0.1f, 0.0f, 1f);
    public static float TempColorBlueMax = -40f;
    public static float TempColorCyanMax = -20f;
    public static float TempColorGreenMax = 0f;
    public static float TempColorYellowMax = 15f;
    public static float TempColorOrangeMin = 25f;
    public static float TempColorOrangeMax = 30f;
    public static float TempColorRedMin = 35f;

    // 光照视图颜色
    public static Color LightColorDark = new Color(0f, 0f, 0f, 1f);
    public static Color LightColorBright = new Color(1f, 1f, 1f, 1f);

    // 高度视图颜色（真实地形图风格锚点色）
        public static Color AltitudeColorDeepWater = new Color(0.01f, 0.07f, 0.28f, 1f);
        public static Color AltitudeColorShallowWater = new Color(0.10f, 0.42f, 0.82f, 1f);
        public static Color AltitudeColorCoast = new Color(0.97f, 0.88f, 0.56f, 1f);
        public static Color AltitudeColorCoastalPlain = new Color(0.22f, 0.60f, 0.24f, 1f);
        public static Color AltitudeColorLowland = new Color(0.46f, 0.78f, 0.20f, 1f);
        public static Color AltitudeColorHighland = new Color(0.82f, 0.70f, 0.18f, 1f);
        public static Color AltitudeColorUpland = new Color(0.73f, 0.46f, 0.18f, 1f);
        public static Color AltitudeColorMountain = new Color(0.34f, 0.26f, 0.24f, 1f);
        public static Color AltitudeColorSnow = new Color(0.99f, 0.99f, 0.99f, 1f);

    // 热力图归一化范围
    public static float TempMin = 15f;
    public static float TempMax = 35f;
    public static float LightMin = 0f;
    public static float LightMax = 100f;
    public static float AltitudeMin = -20000f;
    public static float AltitudeMax = 20000f;
    public static int AltitudeContourLevels = 50;
    public static int AltitudeCoastBandWidth = 24;
    public static float AltitudeContourDarken = 0.28f;
    public static int OverlayDownsampleFactor = 2; // 温度/光照叠加层降采样因子
    public static int OverlayUpdateStepInterval = 1; // 叠加层每隔多少回合更新一次
    public static float OverlayUpdateMinIntervalSeconds = 0.01f; // 叠加层最短更新时间间隔
    public static float OverlayTempEncodeMin = -40f; // 热力图编码最低温度（摄氏）
    public static float OverlayTempEncodeMax = 35f;  // 热力图编码最高温度（摄氏）

    // 装置系统
    public static int DeviceSeedStorageTypeId = 1;
    public static int DeviceSeedStorageInitialCount = 1;
    public static int DeviceResearchStationTypeId = 2;
    public static int DeviceResearchStationCraftLimit = 3;
    public static int DeviceResearchStationCraftMax = 3; // 科研装置制造上限
    public static int DeviceDefaultCraftMax = 3; // 可制作装置默认制造上限（0=不限）
    public static int DeviceIconSize = 24;
    public static int DevicePreviewSize = 24;
    public static float DevicePanelListRatio = 0.33f; // 装置列表占比
    public static float DevicePreviewAlpha = 0.65f;
    public static string DeviceTransparentHex = "7F7F7F";
    public static Color32 DeviceTransparentColor = new Color32(127, 127, 127, 255);
    public static Color DeviceRangeOverlayColor = new Color(0.15f, 0.9f, 0.35f, 0.25f);
    public static float GeneViewNonMatchAlpha = 0.5f;
    public static float GeneViewDimAlpha = 0.5f; // 基因视图中未匹配细胞的透明度
    public static int GeneListRefreshStepInterval = 10;

    // 研发点与科研装置
    public static int ResearchBaseGainPerStep = 100;
    public static int ResearchDeviceBaseCost = 10000;
    public static int ResearchDeviceCostMultiplier = 2;
    public static int ResearchDeviceGainPerCell = 1;

    // 存档
    public static int SaveSlotCount = 6;
    public static float SaveModalOverlayAlpha = 0.55f; // 存档确认弹窗背景遮罩透明度
    public static string MainMenuSceneName = "MainMenu";
    public static string GameSceneName = "A";

    // 环境场：光照 / 温度
    public static float ClimateLatitudeLightMin = 0.30f;
    public static float ClimateLatitudeLightMax = 1.00f;
    public static float ClimateBaseLight = 0.12f;
    public static float ClimateLatitudeLightWeight = 0.38f;
    public static float ClimateSunExposureWeight = 0.38f;
    public static float ClimateAltitudeLightWeight = 0.12f;
    public static float ClimateSunHeightLightWeight = 0.10f;

    public static float ClimateLatitudeTempMin = 17f;
    public static float ClimateLatitudeTempMax = 33.5f;
    public static float ClimateLightToTempWeight = 0.08f;
    public static float ClimateLandAltitudeCooling = 8.5f;
    public static float ClimateWaterAltitudeCooling = 3.5f;
    public static float AltitudeTempLapseHeightStep = 100f; // 每多少高度单位
    public static float AltitudeTempLapsePerStep = 0.65f;   // 每步温度降低(摄氏)
    public static float ClimateMaritimeTempMin = 20f;
    public static float ClimateMaritimeTempMax = 28f;
    public static float ClimateMaritimeBlend = 0.60f;
    public static float ClimateBeachTempMin = 21f;
    public static float ClimateBeachTempMax = 30f;
    public static float ClimateBeachBlend = 0.35f;
    public static float ClimateUnderwaterTempMin = 19f;
    public static float ClimateUnderwaterTempMax = 26f;
    public static float ClimateUnderwaterBlend = 0.45f;
    public static float ClimateOpenWaterDistanceScale = 180f;
    public static float ClimateOpenWaterCoolingStrength = 6.0f;
    public static float ClimateOpenWaterBlendBoost = 0.22f;
    public static float ClimateCoastalLandWarmth = 0.8f;
    public static int ClimateTemperatureSmoothingPasses = 2;
    public static float ClimateTemperatureSmoothingStrength = 0.55f;

        // 地形法线光照
        public static int TerrainSlopeSampleRadius = 3;
        public static int TerrainMacroReliefRadius = 24;
        public static float TerrainNormalStrength = 22.0f;
        public static float TerrainLandSlopeNoiseFloor = 0.014f;
        public static float TerrainWaterSlopeNoiseFloor = 0.014f;
        public static float TerrainLandReliefNoiseFloor = 0.022f;
        public static float TerrainWaterReliefNoiseFloor = 0.022f;
        public static float TerrainCurvatureStrength = 3.2f;
        public static float TerrainHeightContrast = 10f;
        public static float TerrainHighTintStrength = 0.0f;
        public static float TerrainLowTintStrength = 0.0f;
        public static float TerrainLightAmbient = 0.26f;
        public static float TerrainLightDiffuse = 15f;
        public static Vector3 TerrainLightDirection = new Vector3(-0.91f, -0.33f, 0.24f).normalized;

    // 叠加层透明度（温度/光照视图覆盖在地形上方）
    public static float OverlayAlpha = 0.55f;

    // 化学物质：动力方程指数 min(反应物量/系数)^power
    public static float ChemKineticsPower = 0.75f;

    // 地形基准浓度（陆地 / 水域）
    public static float ChemBaselineOrganicLand = 2.5f;
    public static float ChemBaselineOrganicWater = 0.4f;
    public static float ChemBaselineCO2Land = 1.2f;
    public static float ChemBaselineCO2Water = 2.0f;
    public static float ChemBaselineH2Land = 0.3f;
    public static float ChemBaselineH2Water = 0.8f;
    public static float ChemBaselineH2SLand = 0.2f;
    public static float ChemBaselineH2SWater = 0.6f;
    public static float ChemBaselineSulfateLand = 0.5f;
    public static float ChemBaselineSulfateWater = 1.5f;

    // 热力图归一化上限
    public static float ChemOverlayMaxOrganic = 8f;
    public static float ChemOverlayMaxCO2 = 6f;
    public static float ChemOverlayMaxH2 = 4f;
    public static float ChemOverlayMaxH2S = 4f;
    public static float ChemOverlayMaxSulfate = 5f;

    // 物质热力图颜色
    public static Color ChemColorOrganic = new Color(0.35f, 0.55f, 0.22f);
    public static Color ChemColorCO2 = new Color(0.55f, 0.55f, 0.55f);
    public static Color ChemColorH2 = new Color(0.20f, 0.75f, 0.85f);
    public static Color ChemColorH2S = new Color(0.90f, 0.80f, 0.15f);
    public static Color ChemColorSulfate = new Color(0.75f, 0.78f, 0.95f);

    // 非生物反应速率
    public static float ChemRateR1 = 0.05f;
    public static float ChemRateR2 = 0.05f;
    public static float ChemRateR3 = 0.05f;
    public static float ChemRateR4 = 0.05f;
    public static float ChemRateR5 = 0.05f;

    // R1: CO2 + 4H2 -> 有机物
    public static float ChemTempMinR1 = 5f;
    public static float ChemTempMaxR1 = 65f;
    public static int ChemLightMinR1 = 10;

    // R2: H2S + CO2 -> 有机物
    public static float ChemTempMinR2 = 5f;
    public static float ChemTempMaxR2 = 70f;
    public static int ChemLightMinR2 = 15;

    // R3: 有机物 -> CO2 + 2H2
    public static float ChemTempMinR3 = 15f;
    public static float ChemTempMaxR3 = 90f;
    public static int ChemLightMinR3 = 0;

    // R4: 硫酸盐 + 有机物 -> H2S + CO2
    public static float ChemTempMinR4 = 5f;
    public static float ChemTempMaxR4 = 80f;
    public static int ChemLightMinR4 = 0;

    // R5: 2H2S -> 2H2
    public static float ChemTempMinR5 = 10f;
    public static float ChemTempMaxR5 = 75f;
    public static int ChemLightMinR5 = 20;
}
