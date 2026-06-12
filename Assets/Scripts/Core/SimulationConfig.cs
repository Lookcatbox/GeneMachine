// SimulationConfig.cs - 全局配置
// 规模相关：EnvirSize=2000 → 400 万环境格；多数每步系统为 O(EnvirSize²) 或 O(EnvirSize² × 物质数/反应数)。
using UnityEngine;

/// <summary>模拟规模、行为 ID、环境默认值、装置与研发等全局常量。</summary>
public static class SimulationConfig
{
    public static int CellMaxNum = 20;              // 每个环境格能容纳细胞的最大数
    public static int CellTotalMaxNum = 2000000;    // 全局细胞总数上限；影响 Pre/Apply/死亡清理等 O(N) 行为的上界
    public static int GeneNum = 1000;               // 基因种类数上限
    public static int WorldSeed = 10000000;         // 世界随机种子（地形与初始分布）
    public static int EnvirSize = 2000;             // 环境格边长（EnvirSize × EnvirSize）；扩大此值会平方级增加扩散/反应/光照等每步开销
    public static float PixelPerEnvir = 1.0f;       // 每个环境格对应的世界单位大小

    // 物种基因槽位
    public static int MaxMainGene = 50;             // 主干基因槽位数
    public static int MaxSubGene = 100;             // 自由基因槽位数

    // 初始参数
    public static int InitialEnergy = 200;          // 细胞初始能量
    public static int InitialPlayerCells = 50;      // 初始玩家细胞数
    public static int InitialNPCClusters = 20;      // 初始 NPC 群落数
    public static int InitialNPCPerCluster = 30;    // 每个 NPC 群落细胞数

    // 行为编号 (Behavior ID)
    public static int BehaviorMultiply = 1;         // 繁殖行为 ID
    public static int BehaviorTemperature = 2;      // 温度行为 ID
    public static int BehaviorLight = 3;            // 光照行为 ID
    public static int BehaviorDeath = 4;            // 死亡行为 ID

    // 环境默认值
    public static int DefaultTemp = 27;             // 新建环境格的默认温度（摄氏）
    public static int DefaultLight = 50;            // 新建环境格的默认光照（0–100）
    public static float KelvinOffset = 273.15f;     // 摄氏转开尔文的偏移量
    public static int AltitudeThreshold = 0;        // 海平面高度（低于此为水域）

    // 热扩散与光照简易更新
    public static int LightUpdateValue = 50;        // 简易光照更新时的基准光照值（0–100）
    public static float LightNoiseScale = 0.002f;   // 光照噪声空间缩放
    public static float LightNoiseZStep = 1.0f;     // 光照噪声每回合 z 切片步进
    public static int LightNoiseOctaves = 3;          // 光照噪声叠加层数
    public static float LightNoiseFrequencyMultiplier = 1.8f;  // 光照噪声每层频率递增倍数
    public static float LightNoiseWeightDecay = 0.6f;          // 光照噪声每层权重递减系数
    public static float LightLatitudeMinMultiplier = 0.30f;    // 极地纬度光照倍率下限
    public static float LightLatitudeMaxMultiplier = 1.00f;    // 赤道纬度光照倍率上限
    public static float LightLatitudeAxialTiltDegrees = 23.44f; // 地轴倾角（影响年均日照曲线）
    public static float HeatLightGainAtFull = 14f;  // 100% 光照时每回合温度增量（摄氏）
    public static float HeatLossLand = 0.02f;       // 陆地基准散热比例（效率 100% 时）
    public static float HeatDiffusionStrength = 0.5f; // 热扩散强度（0–1，向邻格匀热比例）
    public static float HeatLossEfficiencyTempMin = -273.15f;  // 散热效率曲线低温端（绝对零度，摄氏）
    public static float HeatLossEfficiencyTempZero = -30f;     // 散热效率曲线 0 效率锚点（摄氏）
    public static float HeatLossEfficiencyTempMax = 35f;       // 散热效率曲线满效率锚点（摄氏）
    public static float HeatLossEfficiencyAtZero = 0.10f;    // 0°C 锚点处的散热效率
    public static float HeatLossEfficiencyAtMax = 1.00f;       // 高温锚点处的散热效率
    public static float HeatLossEfficiencyMidCurvePower = 1.4f; // 0°C 至高温段效率曲线幂次
    public static float HeatLossEfficiencyLowCurvePower = 0.7f; // 0°C 以下效率曲线幂次
    public static float HeatConductionLand = 1.00f;  // 陆地热传导效率倍率
    public static float HeatConductionSand = 0.75f;  // 沙滩热传导效率倍率
    public static float HeatConductionWater = 0.50f; // 水域热传导效率倍率
    public static float ChemicalGasDiffusionStrength = 0.35f;    // 气体物质扩散强度（0–1）
    public static float ChemicalLiquidDiffusionStrength = 0.20f; // 液体物质在水域内的扩散强度（0–1）

    // 调试：温度统计
    public static bool DebugTempStatsEnabled = false; // 是否在控制台输出温度统计
    public static int DebugTempStatsInterval = 100;   // 温度统计输出间隔（模拟步数）
    public static int DebugTempStatsStride = 20;      // 温度统计采样步长（越大越快、越粗）

    // 基础温度耐受（用于研发升级）
    public static int BaseTempToleranceMin = 25;    // 玩家基础最低耐受温度（摄氏）
    public static int BaseTempToleranceMax = 30;    // 玩家基础最高耐受温度（摄氏）
    public static int ResearchTempUpgradeMaxLevel = 5;       // 温度耐受研发最高等级
    public static int ResearchTempUpgradeBaseCost = 10000;   // 温度耐受首次升级研发点消耗
    public static float ResearchTempUpgradeGrowth = 1.5f;    // 温度耐受升级费用增长倍率

    public static int AltitudeBeach = 100;          // 沙滩高度上限（海平面 + 此值以下为沙滩）

    // 视野优化阈值
    public static int GridOptimizeThreshold = 10000; // 可见格子超过此数时启用优化渲染（每格只画最高优先级细胞）
    public static int CellListRenderThreshold = 10000; // 可见格超过此数且多于存活细胞数时，改扫 AllCells 而非逐格扫 EnvirData
    public static int GridDrawThreshold = 2500;      // 可见格子低于此数时绘制网格线
    public static bool ForceOptimizedRenderWhilePanning = true; // 平移地图时强制每格只画最高优先级细胞（中距离缩放）
    public static bool SkipGridLinesWhilePanning = true;          // 平移地图时跳过网格线 GL 绘制（近距离缩放）
    public static int CellRenderMaxBatch = 1023;     // DrawMeshInstanced 单批实例上限（DX11 常量缓冲约 1023）

    // 地形生成
    public static float NoiseBaseScale = 0.00150f;       // 最低频噪声缩放（大陆轮廓）
    public static int NoiseLayerCount = 10;              // 地形噪声叠加层数
    public static float NoiseFrequencyMultiplier = 1.8f; // 地形噪声每层频率递增倍数
    public static float NoiseWeightDecay = 0.7f;         // 地形噪声每层权重递减系数

    // 河流生成
    public static int RiverSourceCount = 300;       // 随机河流源头数量
    public static int RiverMaxLength = 2000;        // 单条河流最大追踪步数
    public static int RiverMinLength = 15;          // 河流最短有效长度（低于则丢弃）
    public static int RiverBaseWidth = 2;           // 河流初始宽度（格）
    public static int RiverWidenPerFlow = 3;        // 每汇入 N 条支流增加 1 格宽度

    // 地形视图颜色
    public static Color TerrainColorOcean = new Color(0.05f, 0.12f, 0.55f, 1f);  // 海洋
    public static Color TerrainColorLand = new Color(0.55f, 0.38f, 0.18f, 1f);   // 陆地
    public static Color TerrainColorBeach = new Color(0.93f, 0.87f, 0.58f, 1f);  // 沙滩
    public static Color TerrainColorRiver = new Color(0.30f, 0.60f, 0.90f, 1f);  // 河流
    public static Color TerrainColorLake = new Color(0.15f, 0.35f, 0.70f, 1f);   // 湖泊

    // 温度视图颜色（渐变色锚点）
    public static Color TempColorBlue = new Color(0.0f, 0.2f, 0.9f, 1f);       // 极冷色
    public static Color TempColorCyan = new Color(0.0f, 0.8f, 0.85f, 1f);      // 冷色
    public static Color TempColorGreen = new Color(0.15f, 0.75f, 0.2f, 1f);    // 凉色
    public static Color TempColorYellow = new Color(0.95f, 0.90f, 0.15f, 1f);  // 暖色
    public static Color TempColorOrange = new Color(0.98f, 0.55f, 0.10f, 1f);  // 热色
    public static Color TempColorRed = new Color(0.9f, 0.1f, 0.0f, 1f);        // 极热色
    public static float TempColorBlueMax = -40f;    // 蓝色段温度上限（摄氏，低于等于全蓝）
    public static float TempColorCyanMax = -20f;    // 青蓝段温度上限（摄氏）
    public static float TempColorGreenMax = 0f;     // 绿段温度上限（摄氏）
    public static float TempColorYellowMax = 15f;   // 黄段温度上限（摄氏）
    public static float TempColorOrangeMin = 25f;   // 橙段温度下限（摄氏）
    public static float TempColorOrangeMax = 30f;   // 橙段温度上限（摄氏）
    public static float TempColorRedMin = 35f;      // 红段温度下限（摄氏，高于等于全红）

    // 光照视图颜色
    public static Color LightColorDark = new Color(0f, 0f, 0f, 1f);     // 无光照色
    public static Color LightColorBright = new Color(1f, 1f, 1f, 1f);   // 满光照色

    // 高度视图颜色（真实地形图风格锚点色）
    public static Color AltitudeColorDeepWater = new Color(0.01f, 0.07f, 0.28f, 1f);       // 深水
    public static Color AltitudeColorShallowWater = new Color(0.10f, 0.42f, 0.82f, 1f);    // 浅水
    public static Color AltitudeColorCoast = new Color(0.97f, 0.88f, 0.56f, 1f);            // 海岸
    public static Color AltitudeColorCoastalPlain = new Color(0.22f, 0.60f, 0.24f, 1f);   // 沿海平原
    public static Color AltitudeColorLowland = new Color(0.46f, 0.78f, 0.20f, 1f);          // 低地
    public static Color AltitudeColorHighland = new Color(0.82f, 0.70f, 0.18f, 1f);         // 高地
    public static Color AltitudeColorUpland = new Color(0.73f, 0.46f, 0.18f, 1f);          // 丘陵
    public static Color AltitudeColorMountain = new Color(0.34f, 0.26f, 0.24f, 1f);        // 山地
    public static Color AltitudeColorSnow = new Color(0.99f, 0.99f, 0.99f, 1f);             // 雪线以上

    // 热力图归一化范围
    public static float TempMin = 15f;              // 温度热力图归一化下限（摄氏）
    public static float TempMax = 35f;              // 温度热力图归一化上限（摄氏）
    public static float LightMin = 0f;              // 光照热力图归一化下限
    public static float LightMax = 100f;            // 光照热力图归一化上限
    public static float AltitudeMin = -20000f;      // 高度视图归一化下限
    public static float AltitudeMax = 20000f;       // 高度视图归一化上限
    public static int AltitudeContourLevels = 50;   // 高度等高线分级数
    public static int AltitudeCoastBandWidth = 24;  // 海岸过渡带宽度（高度单位）
    public static float AltitudeContourDarken = 0.28f; // 等高线边缘加深强度
    public static int OverlayDownsampleFactor = 2;  // 温度/光照叠加层降采样（越大越快）；像素数 ≈ (EnvirSize/factor)²
    public static int ChemicalOverlayDownsampleFactor = 4; // 化学叠加专用降采样（可大于 OverlayDownsampleFactor 以减负）
    public static int OverlayUpdateStepInterval = 1; // 叠加层每隔多少模拟步更新一次（仅步数触发，不用时间节流）
    public static int OverlayBufferPoolSize = 2;   // 叠加层缓冲池大小（至少 2，乒乓写入/显示）
    public static bool DeferOverlayRebuildWhilePanning = true; // 拖动地图时推迟叠加层重建，松手后再刷
    public static int OverlayRebuildDeferFrames = 1;           // 步进触发后延迟 N 帧再重建，错开模拟尖刺
    public static int OverlayRasterMaxParallelism = 2;         // 主线程化学叠加栅格化的最大并行线程数

    // 模拟线程 CPU 争抢控制
    public static SimulationThreadPriorityKind SimulationThreadPriority = SimulationThreadPriorityKind.BelowNormal;
    public static int SimulationParallelReserveCores = 1;      // 模拟 Parallel 时为 Unity 主线程预留的逻辑核数
    public static float OverlayTempEncodeMin = -40f; // 温度叠加编码下限（摄氏，映射到 R 通道）
    public static float OverlayTempEncodeMax = 35f;  // 温度叠加编码上限（摄氏，映射到 R 通道）

    // 装置系统
    public static int DeviceSeedStorageTypeId = 1;           // 种子仓装置类型 ID
    public static int DeviceSeedStorageInitialCount = 1;     // 新游戏初始种子仓数量
    public static int DeviceResearchStationTypeId = 2;       // 科研装置类型 ID
    public static int DeviceResearchStationCraftLimit = 3;   // 科研装置制造次数软限制（遗留字段）
    public static int DeviceResearchStationCraftMax = 3;     // 科研装置可制造上限
    public static int DeviceDefaultCraftMax = 3;             // 可制作装置默认制造上限（0=不限）
    public static int DeviceIconSize = 24;                   // 装置列表图标尺寸（像素）
    public static int DevicePreviewSize = 24;                // 放置预览图尺寸（像素）
    public static float DevicePanelListRatio = 0.33f;        // 装置面板列表区域高度占比
    public static float DevicePreviewAlpha = 0.65f;          // 放置预览透明度
    public static string DeviceTransparentHex = "7F7F7F";    // 装置透明色十六进制（编辑器/占位）
    public static Color32 DeviceTransparentColor = new Color32(127, 127, 127, 255); // 装置透明色
    public static Color DeviceRangeOverlayColor = new Color(0.15f, 0.9f, 0.35f, 0.25f); // 装置范围圈颜色
    public static float GeneViewNonMatchAlpha = 0.5f;        // 基因视图中不匹配细胞透明度（遗留）
    public static float GeneViewDimAlpha = 0.5f;             // 基因视图中未匹配细胞的透明度
    public static int GeneListRefreshStepInterval = 10;      // 基因列表刷新间隔（模拟步数）

    // 研发点与科研装置
    public static int ResearchBaseGainPerStep = 100;         // 每步基础研发点获取量
    public static int ResearchDeviceBaseCost = 10000;        // 科研装置首次制造成本（研发点）
    public static int ResearchDeviceCostMultiplier = 2;      // 科研装置制造成本递增倍率
    public static int ResearchDeviceGainPerCell = 1;           // 科研装置覆盖格内每个细胞提供的研发点

    // 画面设置
    public static readonly int[] TargetFpsCapPresets = { 30, 60, 90, 120, 144, 180 };
    public static int TargetFpsCapInfinity = -1;               // 帧率上限「无限制」时传给 Application.targetFrameRate 的值
    public static int DefaultTargetFpsCap = 60;              // 默认帧率上限
    public static string TargetFpsCapInfinityLabel = "∞";    // 无限制选项显示文案
    public static bool EnforceSoftwareFrameCap = true;       // 帧末主动等待，弥补编辑器内 targetFrameRate 不可靠

    // 存档
    public static int SaveSlotCount = 6;                     // 存档槽位数量
    public static float SaveModalOverlayAlpha = 0.55f;       // 存档确认弹窗背景遮罩透明度
    public static string MainMenuSceneName = "MainMenu";     // 主菜单场景名
    public static string GameSceneName = "A";                // 游戏主场景名

    // 环境场：光照 / 温度
    public static float ClimateLatitudeLightMin = 0.30f;     // 气候光照：极地纬度倍率下限
    public static float ClimateLatitudeLightMax = 1.00f;     // 气候光照：赤道纬度倍率上限
    public static float ClimateBaseLight = 0.12f;            // 气候光照：全局基准分量
    public static float ClimateLatitudeLightWeight = 0.38f;  // 气候光照：纬度曲线权重
    public static float ClimateSunExposureWeight = 0.38f;  // 气候光照：坡向日照权重
    public static float ClimateAltitudeLightWeight = 0.12f;  // 气候光照：海拔对光照权重
    public static float ClimateSunHeightLightWeight = 0.10f; // 气候光照：太阳高度角权重

    public static float ClimateLatitudeTempMin = 17f;        // 气候温度：高纬基准温度（摄氏）
    public static float ClimateLatitudeTempMax = 33.5f;      // 气候温度：低纬基准温度（摄氏）
    public static float ClimateLightToTempWeight = 0.08f;    // 气候温度：光照转温度权重
    public static float ClimateLandAltitudeCooling = 8.5f;    // 气候温度：陆地海拔降温强度
    public static float ClimateWaterAltitudeCooling = 3.5f;   // 气候温度：水域海拔降温强度
    public static float AltitudeTempLapseHeightStep = 100f;  // 海拔递减：每多少高度单位算一步
    public static float AltitudeTempLapsePerStep = 0.65f;    // 海拔递减：每步降低温度（摄氏）
    public static float ClimateMaritimeTempMin = 20f;         // 海洋性气候温度下限（摄氏）
    public static float ClimateMaritimeTempMax = 28f;         // 海洋性气候温度上限（摄氏）
    public static float ClimateMaritimeBlend = 0.60f;         // 海洋性气候混合权重
    public static float ClimateBeachTempMin = 21f;            // 沙滩气候温度下限（摄氏）
    public static float ClimateBeachTempMax = 30f;            // 沙滩气候温度上限（摄氏）
    public static float ClimateBeachBlend = 0.35f;            // 沙滩气候混合权重
    public static float ClimateUnderwaterTempMin = 19f;       // 水下气候温度下限（摄氏）
    public static float ClimateUnderwaterTempMax = 26f;       // 水下气候温度上限（摄氏）
    public static float ClimateUnderwaterBlend = 0.45f;       // 水下气候混合权重
    public static float ClimateOpenWaterDistanceScale = 180f; // 开阔水域距离衰减尺度（格）
    public static float ClimateOpenWaterCoolingStrength = 6.0f; // 远离陆地时的额外降温强度
    public static float ClimateOpenWaterBlendBoost = 0.22f;   // 开阔水域对海洋性混合的增强
    public static float ClimateCoastalLandWarmth = 0.8f;      // 沿海陆地额外增温（摄氏）
    public static int ClimateTemperatureSmoothingPasses = 2;  // 气候温度场平滑迭代次数
    public static float ClimateTemperatureSmoothingStrength = 0.55f; // 气候温度场平滑强度

    // 地形法线光照
    public static int TerrainSlopeSampleRadius = 3;           // 法线计算：坡度采样半径（格）
    public static int TerrainMacroReliefRadius = 24;          // 法线计算：宏观起伏采样半径（格）
    public static float TerrainNormalStrength = 22.0f;        // 地形法线强度
    public static float TerrainLandSlopeNoiseFloor = 0.014f;  // 陆地坡度噪声抑制阈值
    public static float TerrainWaterSlopeNoiseFloor = 0.014f; // 水域坡度噪声抑制阈值
    public static float TerrainLandReliefNoiseFloor = 0.022f; // 陆地起伏噪声抑制阈值
    public static float TerrainWaterReliefNoiseFloor = 0.022f; // 水域起伏噪声抑制阈值
    public static float TerrainCurvatureStrength = 3.2f;      // 地形曲率 shading 强度
    public static float TerrainHeightContrast = 10f;          // 地形高度对比度
    public static float TerrainHighTintStrength = 0.0f;       // 高处色调增强强度
    public static float TerrainLowTintStrength = 0.0f;        // 低处色调增强强度
    public static float TerrainLightAmbient = 0.26f;            // 地形环境光强度
    public static float TerrainLightDiffuse = 15f;              // 地形漫反射强度
    public static Vector3 TerrainLightDirection = new Vector3(-0.91f, -0.33f, 0.24f).normalized; // 地形光照方向

    // 叠加层透明度（温度/光照/化学视图覆盖在地形上方）
    public static float OverlayAlpha = 0.55f;                 // 叠加层整体透明度

    // 化学物质：动力方程指数 min(反应物量/系数)^power
    public static float ChemKineticsPower = 0.75f;            // 反应速率方程幂指数（内置默认配置用）

    // 地形基准浓度（陆地 / 水域，仅开局播种一次）
    public static float ChemBaselineOrganicLand = 2.5f;       // 有机物陆地初始浓度
    public static float ChemBaselineOrganicWater = 0.4f;      // 有机物水域初始浓度
    public static float ChemBaselineCO2Land = 1.2f;           // CO2 陆地初始浓度
    public static float ChemBaselineCO2Water = 2.0f;          // CO2 水域初始浓度
    public static float ChemBaselineH2Land = 0.3f;            // H2 陆地初始浓度
    public static float ChemBaselineH2Water = 0.8f;           // H2 水域初始浓度
    public static float ChemBaselineH2SLand = 0.2f;           // H2S 陆地初始浓度
    public static float ChemBaselineH2SWater = 0.6f;          // H2S 水域初始浓度
    public static float ChemBaselineSulfateLand = 0.5f;      // 硫酸盐陆地初始浓度
    public static float ChemBaselineSulfateWater = 1.5f;      // 硫酸盐水域初始浓度

    // 热力图归一化上限（内置默认配置用）
    public static float ChemOverlayMaxOrganic = 8f;           // 有机物热力图归一化上限
    public static float ChemOverlayMaxCO2 = 6f;               // CO2 热力图归一化上限
    public static float ChemOverlayMaxH2 = 4f;                // H2 热力图归一化上限
    public static float ChemOverlayMaxH2S = 4f;               // H2S 热力图归一化上限
    public static float ChemOverlayMaxSulfate = 5f;           // 硫酸盐热力图归一化上限

    // 物质热力图颜色（内置默认配置用）
    public static Color ChemColorOrganic = new Color(0.35f, 0.55f, 0.22f);  // 有机物
    public static Color ChemColorCO2 = new Color(0.55f, 0.55f, 0.55f);     // CO2
    public static Color ChemColorH2 = new Color(0.20f, 0.75f, 0.85f);      // H2
    public static Color ChemColorH2S = new Color(0.90f, 0.80f, 0.15f);     // H2S
    public static Color ChemColorSulfate = new Color(0.75f, 0.78f, 0.95f); // 硫酸盐

    // 非生物反应速率（内置默认配置用）
    public static float ChemRateR1 = 0.05f;                   // R1 原始氢能合成速率系数
    public static float ChemRateR2 = 0.05f;                   // R2 硫化耦合速率系数
    public static float ChemRateR3 = 0.05f;                   // R3 有机物厌氧分解速率系数
    public static float ChemRateR4 = 0.05f;                   // R4 硫酸盐还原速率系数
    public static float ChemRateR5 = 0.05f;                   // R5 H2S 光致裂解速率系数

    // R1: CO2 + 4H2 -> 有机物
    public static float ChemTempMinR1 = 5f;                   // R1 反应最低温度（摄氏）
    public static float ChemTempMaxR1 = 65f;                  // R1 反应最高温度（摄氏）
    public static int ChemLightMinR1 = 10;                    // R1 反应最低光照

    // R2: H2S + CO2 -> 有机物
    public static float ChemTempMinR2 = 5f;                   // R2 反应最低温度（摄氏）
    public static float ChemTempMaxR2 = 70f;                  // R2 反应最高温度（摄氏）
    public static int ChemLightMinR2 = 15;                    // R2 反应最低光照

    // R3: 有机物 -> CO2 + 2H2
    public static float ChemTempMinR3 = 15f;                  // R3 反应最低温度（摄氏）
    public static float ChemTempMaxR3 = 90f;                  // R3 反应最高温度（摄氏）
    public static int ChemLightMinR3 = 0;                     // R3 反应最低光照

    // R4: 硫酸盐 + 有机物 -> H2S + CO2
    public static float ChemTempMinR4 = 5f;                   // R4 反应最低温度（摄氏）
    public static float ChemTempMaxR4 = 80f;                  // R4 反应最高温度（摄氏）
    public static int ChemLightMinR4 = 0;                     // R4 反应最低光照

    // R5: 2H2S -> 2H2
    public static float ChemTempMinR5 = 10f;                  // R5 反应最低温度（摄氏）
    public static float ChemTempMaxR5 = 75f;                  // R5 反应最高温度（摄氏）
    public static int ChemLightMinR5 = 20;                    // R5 反应最低光照
}

/// <summary>后台模拟线程优先级（相对系统默认线程）。</summary>
public enum SimulationThreadPriorityKind
{
    BelowNormal = 0,
    Normal = 1,
    AboveNormal = 2
}
