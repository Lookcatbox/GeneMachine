# 修改日志

## 2026-05-18 代码文件简介清单

### 修改文件

#### CodeMenu.md
- 新增全项目代码文件简介清单。

## 2026-05-14 热扩散与光照更新

### 修改文件

#### Core/LightUpdate.cs
- 新增每回合光照更新：全图固定光照值（当前为50）。

#### Core/HeatDiffusion.cs
- 新增温度更新与热扩散：光照升温、地形散热、8邻域拉普拉斯扩散。

#### Core/SimulationCore.cs
- 每回合改为调用新的光照与热扩散更新，不再刷新旧气候场（旧温度生成保留初始化）。

#### Core/SimulationConfig.cs
- 新增光照固定值、升温/散热比例、扩散系数等参数。

#### Entities/Envir.cs
- `Temp` 类型调整为 `float`，用于连续温度演化。

## 2026-05-14 温度流失效率调整

### 修改文件

#### Core/SimulationConfig.cs
- 调整地形温度流失比例（陆地20%、沙地15%、水域10%）。
- 新增温度流失效率区间（0°C=0%，30°C=100%）。

#### Core/HeatDiffusion.cs
- 温度流失改为按效率系数缩放，温度越高流失越接近配置的最大比例。

## 2026-05-15 热传导效率接入

### 修改文件

#### Core/SimulationConfig.cs
- 新增地形热传导效率（陆地100%、沙滩75%、水域50%）。

#### Core/HeatDiffusion.cs
- 光照升温与温度流失按热传导效率缩放，陆地逻辑作为基准。
- 移除水域/沙滩独立流失比例，统一使用陆地基准流失再乘热传导效率。

#### Core/SimulationConfig.cs
- 删除 `HeatLossWater` / `HeatLossSand`，仅保留 `HeatLossLand` 作为基准。

## 2026-05-15 温度视图随回合刷新

### 修改文件

#### Managers/CellRenderer.cs
- 温度/光照视图在每回合步数变化时刷新叠加层，确保热量变化能实时显示。

## 2026-05-15 温度改为开尔文内部运算

### 修改文件

#### Core/SimulationConfig.cs
- 新增开尔文偏移常量，配置仍以摄氏度为基准。

#### Core/SimulationCore.cs
- 初始化完成后统一将环境温度转换为开尔文。
- 新增摄氏度/开尔文转换工具，并将温度耐受计算改为开尔文。

#### Core/HeatDiffusion.cs
- 温度流失效率阈值改为按开尔文参与计算。

#### Core/TemperatureBehavior.cs / Core/DeathBehavior.cs
- 温度判定改为使用开尔文阈值。

#### Core/EnvironmentPlayerPanelTab.cs
- 环境格信息显示改为摄氏度。

#### Managers/CellRenderer.cs
- 温度视图渲染改为摄氏度显示。

## 2026-05-15 热量参数回合尺度调整

### 修改文件

#### Core/SimulationConfig.cs
- 按“一回合=3天、开尔文直接流失”调整：`HeatLightGainAtFull=10`，`HeatLossLand=0.02`。

## 2026-05-15 光照增温与绝对零度阈值

### 修改文件

#### Core/SimulationConfig.cs
- `HeatLossEfficiencyTempMin` 调整为绝对零度（-273.15°C）。
- `HeatLightGainAtFull` 提升到 18 以提高赤道温度。

## 2026-05-15 温度过热与视图性能优化

### 修改文件

#### Core/SimulationConfig.cs
- 降低 `HeatLightGainAtFull` 到 14 并提高 `HeatDiffusionStrength` 到 0.35 以降温、平滑温度断层。
- 新增 `OverlayDownsampleFactor` 用于温度/光照叠加层降采样。

#### Managers/CellRenderer.cs
- 叠加层使用降采样纹理与复用像素缓冲，减少每回合更新成本。

## 2026-05-15 温度流失比例允许超过100%

### 修改文件

#### Core/HeatDiffusion.cs
- 温度流失效率不再上限封顶，可超过100%。

## 2026-05-15 修正光照纬度曲线与温度下限

### 修改文件

#### Core/LightUpdate.cs
- 恢复使用原始余弦曲线进行纬度光照修正（赤道100%、极地30%）。

#### Core/SimulationConfig.cs
- 移除纬度幂曲线参数，避免偏离原始余弦分布。

#### Core/HeatDiffusion.cs
- 温度流失后夹紧到绝对零度，避免出现异常大负温。

## 2026-05-15 扩大温带光照曲线

### 修改文件

#### Core/SimulationConfig.cs
- 新增 `LightLatitudeCurvePower`，用于控制纬度光照曲线宽度。

#### Core/LightUpdate.cs
- 纬度修正加入幂曲线，降低陡峭度以扩大温带范围。

## 2026-05-15 诊断温度异常

### 修改文件

#### Core/SimulationConfig.cs
- 新增温度统计调试开关与采样配置（DebugTempStatsEnabled/Interval/Stride）。

#### Core/SimulationCore.cs
- 每隔配置步数输出温度采样的最小/最大值与坐标（[DEBUG-TEMP]）。

#### Core/HeatDiffusion.cs
- 移除绝对零度夹紧，便于复现异常负温用于诊断。

## 2026-05-15 纬度日照角曲线更接近真实

### 修改文件

#### Core/SimulationConfig.cs
- 新增地轴倾角参数 `LightLatitudeAxialTiltDegrees`。

#### Core/LightUpdate.cs
- 纬度光照修正改为基于年均日照角（考虑地轴倾角）的近似曲线。

## 2026-05-15 热辐射非线性效率曲线

### 修改文件

#### Core/SimulationConfig.cs
- 新增 0°C/30°C 锚点效率与曲线幂参数（0°C=10%，30°C=100%）。

#### Core/HeatDiffusion.cs
- 温度流失效率改为分段非线性曲线，0°C以下逐步逼近0%。

## 2026-05-15 热力图三色过度

### 修改文件

#### Core/SimulationConfig.cs
- 新增温度视图中间色与中点位置（蓝-黄-红）。

#### Managers/CellRenderer.cs
- 温度热力图改为蓝-黄-红分段插值。

## 2026-05-15 热力图六色渐变

### 修改文件

#### Core/SimulationConfig.cs
- 新增蓝/青/绿/黄/橙/红六色与温度阈值配置。

#### Managers/CellRenderer.cs
- 温度热力图改为六色分段渐变（含 25-30 橙色带与 30-35 过渡）。

#### Core/SimulationRenderSettingsData.cs
- 温度视图颜色改为六色配置并纳入渲染设置哈希。

## 2026-05-14 光照更新改为三维噪声切片

### 修改文件

#### Core/LightUpdate.cs
- 光照更新改为三维噪声切片（z每回合递增），范围映射到0-100光照。

#### Core/SimulationConfig.cs
- 新增光照噪声参数（缩放、z步进、层数、频率倍数、权重衰减）。

## 2026-05-14 光照纬度修正

### 修改文件

#### Core/LightUpdate.cs
- 基于纬度加入光照修正：赤道100%、两极30%，按余弦曲线分布。

#### Core/SimulationConfig.cs
- 新增光照纬度倍率参数（极地/赤道）。

## 2026-05-14 修复空细胞列表并行遍历异常

### 修改文件

#### Core/MultiplyBehavior.cs
- 当 `AllCells.Count == 0` 时直接返回，避免 `Partitioner.Create(0, 0)` 抛出 `ArgumentOutOfRangeException`。

#### Core/TemperatureBehavior.cs / Core/LightBehavior.cs / Core/DeathBehavior.cs
- 同样增加空列表保护，避免并行分区创建异常。

#### Core/SimulationCore.cs
- 能量消耗并行段增加 `count > 0` 保护，避免 `Partitioner.Create(0, 0)` 异常导致计算线程终止。

## 2026-05-14 安装 mattpocock/skills 全量 skills

### 修改内容

#### 用户级全局 skills 目录
- 从 `https://github.com/mattpocock/skills` 下载并安装了 24 个非 `deprecated` 的 skills 到 `C:\Users\12454\.claude\skills`。
- 安装范围包含工程类、日常协作类、杂项类、个人类和 in-progress 类 skills；`deprecated` 类未安装。

## 2026-05-14 创建项目级指令文件

### 修改文件

#### .github/copilot-instructions.md
- 新增项目级指令，固定四条要求：指出代码错误、记录所有修改到日志、尽量不动基础架构、Unity 操作步骤分步说明。

#### Assets/Scripts/logs.md
- 补充本次修改记录，便于后续追踪这次指令配置。

## 2026-04-24 玩家操作面板与环境页

### 修改文件

#### Core/MainManager.cs
- 新增右侧玩家操作面板，支持展开/折叠两种状态；展开时固定占据窗口右侧三分之一高度全屏，折叠时仅保留右侧中央的竖向梯形切换按钮。
- 视图切换按钮改为锚定在玩家操作面板左边界，面板展开/折叠时会跟随移动。
- 新增 100 槽位的标签页数组，并接入环境、基因、研发、种群 4 个标签页。
- 新增鼠标悬停环境格读取、左键锁定/解锁环境格逻辑；环境页显示优先读取锁定格，否则读取当前悬停格。
- 为避免 UI 区域误选地图环境格，新增基于 IMGUI 布局矩形的点击屏蔽判断。

#### Core/PlayerPanelTabPage.cs
- 新增玩家操作面板标签页基类与上下文结构，供后续继续扩展新的页签。

#### Core/EnvironmentPlayerPanelTab.cs
- 新增环境标签页，实现环境格属性展示。
- 环境页当前显示：坐标、地形类型、高度、温度、光照、当前细胞数、最大容量、CellList 长度、玩家/NPC 细胞数、最高优先级，以及锁定状态提示。

#### Core/GenePlayerPanelTab.cs
- 新增基因标签页独立代码文件，作为后续扩展入口。

#### Core/ResearchPlayerPanelTab.cs
- 新增研发标签页独立代码文件，作为后续扩展入口。

#### Core/PopulationPlayerPanelTab.cs
- 新增种群标签页独立代码文件，作为后续扩展入口。

## 2026-04-23 增加 3D 法线光照开关

### 修改文件

#### Managers/CellRenderer.cs
- 新增 `normalLightingEnabled` 开关，允许在运行时切换地图背景的法线光照效果。
- 背景材质参数现在会在 3D 开关变化时即时刷新，无需重建地图纹理。

#### Core/MainManager.cs
- 在右下角视图按钮下方新增 `3D` 按钮，用于打开或关闭法线贴图带来的立体光照效果。

#### Shaders/HeightNormalLit.shader
- 新增 `_LightingEnabled` 参数，使 shader 可以在“普通平面着色”和“法线光照着色”之间切换。

## 2026-04-23 基于高度图生成法线贴图

### 修改文件

#### Core/SimulationConfig.cs
- 新增地形法线光照参数：`TerrainNormalStrength`、`TerrainLightAmbient`、`TerrainLightDiffuse`、`TerrainLightDirection`。

#### Managers/CellRenderer.cs
- 背景渲染新增 `bgNormalTexture`，根据 `Envir.Height` 计算每个格子的法线方向。
- 法线采样使用环绕邻格，和当前地图左右/上下连续的世界边界保持一致。
- 背景材质从普通 `Sprites/Default` 切换为支持法线光照的自定义 shader，并在首次渲染时自动生成法线贴图。

#### Shaders/HeightNormalLit.shader
- 新增专用背景 shader：同时采样底图和法线贴图，使用固定方向光做轻量漫反射，让地形起伏更立体。

## 2026-04-22 高度视图细化与地形参数调优

### 修改文件

#### Core/MainManager.cs
- 右下角视图切换按钮正式扩展为 4 个，加入 `高度视图` 按钮。

#### Managers/CellRenderer.cs
- 高度视图底图改为按量化后的高度分层绘制，并在分层边界处压暗形成清晰分层线。
- 新增 `GetAltitudeQuantizedLevel`、`GetAltitudeContourColor`、`EvaluateAltitudeGradient`、`EvaluateAltitudeGradientSegment`，统一高度分层与着色逻辑。
- 高度视图颜色带按分层中心采样，保证颜色带与分层边界对齐。
- 视图切换时每次都会重绘背景，温度/光照叠加层不再覆盖高度视图。

#### Core/SimulationConfig.cs
- 世界种子调整为 `11111`。
- 海平面与沙滩阈值下调为 `AltitudeThreshold = 450`、`AltitudeBeach = 475`。
- 地形噪声参数继续调优为 `NoiseBaseScale = 0.00090`、`NoiseLayerCount = 10`、`NoiseFrequencyMultiplier = 1.8`、`NoiseWeightDecay = 0.7`。
- 高度视图补充并收敛为一组真实地形锚点色，`AltitudeContourLevels` 提高到 `50`，并提高 `AltitudeContourDarken` 以增强分层边界。

#### Core/TerrainGenerator.cs
- 高度生成正式从单套 fBm 迁移为“低频主形状 + 高频细节叠加”的多层噪声结构。
- 增加 `SampleTiledPerlin`，使高度图左右边界与上下边界环绕连续。

#### Future.md
- 将当前目标收敛为“增加高度视图，并由 `SimulationConfig` 中的颜色配置统一控制”。

## 2026-04-22 地图边界改为环绕连续

### 修改文件

#### Core/TerrainGenerator.cs
- 重写 `GeneratePerlinHeight` 的单层噪声采样方式，使每层噪声都按地图宽高做周期采样
- 左边界与右边界现在会在高度图上连续衔接，上边界与下边界也会连续衔接
- 保留原有“低频主形状 + 高频细节叠加”结构，只改变每层的采样方式，不改海陆阈值和地形分类逻辑

## 2026-04-22 提高高度视图颜色对比度

### 修改文件

#### Core/SimulationConfig.cs
- 调整高度视图锚点色，使整体高差映射更强烈：
  - 深水区进一步压暗，浅水区更亮更蓝
  - 海岸带更亮，低地和平原更饱和
  - 高地、丘陵、山地之间的暖色跨度加大
  - 山顶接近纯白，拉大与中高海拔的亮度落差
- `AltitudeContourDarken` 提高到更明显的边界强度，使分层线更清晰

## 2026-04-22 高度生成改为低频主形状叠加高频细节

### 修改文件

#### Core/SimulationConfig.cs
- **删除**: `NoiseScale`, `NoiseOctaves`, `NoisePersistence`, `NoiseLacunarity`
- **新增**: `NoiseBaseScale`, `NoiseLayerCount`, `NoiseFrequencyMultiplier`, `NoiseWeightDecay`
- 新噪声参数语义调整为：最低频层决定大陆级轮廓，后续层频率逐层提高、权重逐层减小，仅用于补充地形细节

#### Core/TerrainGenerator.cs
- **重写** `GeneratePerlinHeight`
- 高度图不再直接使用单套 fBm 参数采样，而是改为：
  - 先从最低频噪声层开始建立大尺度地形骨架
  - 再按“频率递增、影响递减”的方式连续叠加多层高频噪声
  - 每层使用独立偏移，减少不同层细节重复对齐
- 最终仍归一化到 `0-1000`，不改动后续 `AltitudeThreshold` / `AltitudeBeach` 的地形分类逻辑

## 2026-04-13 地形生成 + 视图切换

### 新增文件
- **Core/TerrainGenerator.cs** — 地形生成器（静态类）
  - `GeneratePerlinHeight`: 6八度柏林噪声生成 1000×1000 高度图（0-1000）
  - `HydraulicErosion`: 粒子水力侵蚀（70000次迭代），自然雕刻河谷地形
  - `FlowAccumulation`: D8方向流量累积算法，流量 ≥ 阈值的陆地格标记为河流
  - `Generate`: 整合上述三步，根据 AltitudeThreshold/AltitudeBeach 分类地形（海洋/沙滩/陆地/河流），写入 Envir.Height 和 Envir.Topography

### 修改文件

#### Core/SimulationConfig.cs
- 添加 `using UnityEngine;`
- 新增地形生成参数：NoiseScale, NoiseOctaves, NoisePersistence, NoiseLacunarity, ErosionIterations, ErosionMaxLifetime, RiverFlowThreshold
- 新增地形视图颜色常量：TerrainColorOcean(深蓝), TerrainColorLand(棕色), TerrainColorBeach(淡黄), TerrainColorRiver(浅蓝)
- 新增温度视图颜色常量：TempColorCold(蓝), TempColorHot(红)
- 新增光照视图颜色常量：LightColorDark(黑), LightColorBright(白)
- 新增热力图归一化范围：TempMin/TempMax, LightMin/LightMax

#### Core/SimulationCore.cs — InitWorld()
- 在 Envir 创建循环之后调用 `TerrainGenerator.Generate()`
- 玩家出生点：从地图中心螺旋搜索最近陆地
- 玩家细胞生成：跳过海洋格（Topography==0）
- NPC 群落生成：随机选点时重试直到找到陆地，个体也跳过海洋格

#### Managers/CellRenderer.cs
- 新增 `ViewMode` 枚举（Terrain/Temperature/Light）及 `currentViewMode` 静态字段
- 新增背景渲染资源：1000×1000 Texture2D（Point过滤）、Material、覆盖全世界的 Mesh（z=1，在细胞z=0之后）
- `CreateBackground()`: 创建背景纹理/材质/网格
- `UpdateBackgroundTexture()`: 根据当前视图模式填充像素（地形=按Topography着色, 温度=蓝红插值, 光照=黑白插值）
- `LateUpdate()` 开头：检测视图模式变更时重建纹理，每帧 DrawMesh 渲染背景

#### Core/MainManager.cs — OnGUI()
- 添加 `DrawViewModeUI()` 调用
- `DrawViewModeUI()`: 屏幕右下角绘制3个按钮（地形视图/温度视图/光照视图），选中按钮高亮显示，点击切换 CellRenderer.currentViewMode

### 未修改的文件
- Entities/Cell.cs, Gene.cs, Species.cs
- 所有 Behavior 文件
- CameraController.cs

---

## 2026-04-13 水系算法重写（水源寻径 + 湖泊）

### 修改文件

#### Core/TerrainGenerator.cs — 完全重写水系部分
- **删除**: `HydraulicErosion`(粒子侵蚀)、`SampleHeight`、`CalcGradient`、`FlowAccumulation`(D8流量累积)
- **新增**: `GenerateRiversAndLakes` — 水源寻径河流 + 湖泊生成
  - 随机选取 300 个陆地水源点（高于海拔阈值+20）
  - 每个水源沿最陡下降方向追踪路径，直到：到达海洋 / 汇入已有河流 / 汇入已有湖泊 → 正常终止
  - 若所有邻域都更高（极低点）→ BFS 填充湖泊，水位=极低点高度+汇入源数×LakeRisePerSource
  - 路径写入 flowCount，后续汇入同一湖泊的水源会抬升该湖泊水位并重新 BFS 扩展湖面
- **新增**: `FillLake` — BFS 洪水填充湖泊区域（高度≤水位的连通区标记为湖泊）
- **新增**: `PaintRiver` — 根据 flowCount 按半径涂河流宽度（汇入越多越粗）
- `Generate` 方法更新：调用新的 `GenerateRiversAndLakes`，Topography 增加 4=湖泊判定

#### Core/SimulationConfig.cs
- **删除**: ErosionIterations, ErosionMaxLifetime, RiverFlowThreshold
- **新增**: RiverSourceCount=300, RiverMaxLength=2000, RiverMinLength=15, RiverWidenPerFlow=3
- **新增**: LakeRisePerSource=8f
- **新增**: TerrainColorLake(深蓝偏暗)

#### Entities/Envir.cs
- Topography 注释更新：增加 4=湖泊

#### Managers/CellRenderer.cs
- UpdateBackgroundTexture: Topography switch 新增 case 4 → TerrainColorLake

---

## 2026-04-13 湖泊溢出算法修复 + 河流初始宽度

### 修改文件

#### Core/TerrainGenerator.cs — 湖泊溢出机制完全重写
- **删除**: 旧的 `FillLake`（BFS 洪水填充，导致大量陆地被淹没）
- **重写** `GenerateRiversAndLakes`:
  - 新增 `RiverSource` 结构体（x, y, flow, fromLake）
  - 改为队列驱动：初始 300 个随机水源入队，湖泊溢出产生的新水源也入队继续追踪
  - 极低点处理：找溢出口（未访问邻域中最低点），创建湖泊后从溢出口生成新水源（继承流量）
  - 汇入已有湖泊时：从该湖泊溢出口生成新水源继续向下
  - visited 数组改为代数标记（visitedGen + currentGen），避免每个水源分配 bool 数组
- **新增**: `FillLakeBasin` — 仅填充高度 < 溢出口高度的连通区域（面积受地形约束，不会无限扩展）

#### Core/SimulationConfig.cs
- **删除**: LakeRisePerSource
- **新增**: RiverBaseWidth=2（初始河流宽度 2 格）
- 河流宽度公式改为: `RiverBaseWidth + flowCount / RiverWidenPerFlow`

---

## 2026-04-13 地图扩大 + 河流禁用 + 沙滩阈值调整

### 修改文件

#### Core/SimulationConfig.cs
- `EnvirSize`: 1000 → **2000**
- `AltitudeBeach`: 550 → **525**（海平面 500 + 25）

#### Core/TerrainGenerator.cs
- `GenerateRiversAndLakes` 调用已注释掉（`// TODO: 河流生成暂时禁用`）

---

## 2026-04-13 温度/光照视图改为叠加层

### 修改文件

#### Managers/CellRenderer.cs
- 地形底图始终渲染（不再根据视图模式切换底图内容）
- `UpdateBackgroundTexture()` 简化：只绘制地形颜色
- **新增** 叠加层系统：
  - `overlayTexture`: Bilinear 过滤的 Texture2D（虚化效果）
  - `overlayMesh`: z=0.5（在地形 z=1 和细胞 z=0 之间）
  - `CreateOverlay()`: 创建叠加层资源
  - `UpdateOverlayTexture()`: 根据温度/光照模式填充半透明像素（alpha=OverlayAlpha）
  - `LateUpdate()`: 温度/光照模式时额外渲染叠加层

#### Core/SimulationConfig.cs
- **新增**: `OverlayAlpha = 0.55f`（叠加层透明度）

---

## 2026-04-19 完善基因设计表

### 修改文件

#### Genelist.txt
- 将原始占位式基因清单重写为完整策划文档，扩展到 **45 个基因**。
- 保留并整理 1-10 号基础/原始代谢基因，补全 9 号“原始光合作用”和 10 号“硫化作用”。
- 新增 11-45 号基因，覆盖：
  - 原始厌氧代谢路线（发酵、摄食、甲烷生成、铁循环、固氮、反硝化）
  - 环境耐受路线（抗 H2S、抗酸、抗紫外、热/冷休克、生物膜加厚）
  - 向产氧光合作用过渡的前置基因（光系统前体、水裂解前体、膜褶雏形、色素协同）
  - 终盘基因节点（光合作用、活性氧清除 I/II、细胞色素电子传递链、氧化呼吸）
  - 若干可选强力取舍基因（高亲和氧捕获、快速复制酶、资源掠夺酶、休眠复苏开关、群落分工信号、钙壳沉积）
- 为每个基因补全了：效果、繁殖消耗、开启消耗、联动、冲突/代价。
- 新增 5 条推荐演化路线：原始厌氧摄食型、硫循环厌氧型、原始自养光能型、产氧光合型、高氧呼吸型。
- 在文末追加“当前项目与策划的差异提醒”，明确指出：
  - 当前 `Gene` 结构只有单一 `energyCost` 字段，尚未拆分繁殖消耗/开启消耗。
  - 当前 `Species` 仅初始化了 1-5 号基因。
  - 当前行为系统仍只有繁殖、温度、光照、死亡四类基础逻辑，高阶化学代谢尚未落地。

---

## 2026-04-19 修正 10 号基因“硫化作用”定义

### 修改文件

#### Genelist.txt
- 将 10 号基因从错误的“分解有机物并继续排 H2S 的异养/还原型定义”修正为“以 H2S 为电子供体、固定 CO2 合成有机物的化能自养型定义”。
- 修正后的效果为：每回合消耗 `1 单位 H2S + 1 单位 CO2`，生成 `1 单位有机物`，并排出 `1 单位硫沉积前体`。
- 联动说明同步调整为：
  - 与 14、23 构成硫化化能自养路线。
  - 与 7 共同构成“上游产 H2S、下游吃 H2S”的硫循环生态。
- 将推荐路线 B 名称从“硫循环厌氧型”调整为“硫循环化能型”，避免与新定义冲突。
- 将 39 号“氧化呼吸”的冲突描述中移除 10 号基因，因为修正后的 10 号基因不再代表严格厌氧路线。

---

## 2026-04-19 收敛化学物质命名

### 修改文件

#### Genelist.txt
- 按“只合并功能相近物质，不简化关键物质名”的原则调整了化学名词。
- 保留原名不变的关键物质：`CO2`、`H2O`、`H2`、`O2`、`CH4`、`H2S`、`N2`、`Fe2+/Fe3+`。
- 合并了两组前台更适合统一显示的物质：
  - `可用氮` = `NH3`、`NO3` 等可直接利用的含氮物。
  - `酸化物` = `SO2`、`H2SO4` 等会提升酸化压力的物质。
- 将文中所有“某某前体”表述改为更直白的名称：
  - `硫沉积前体` → `固硫物`
  - `有机物前体` → `少量有机物`
  - `光系统前体` → `原始光系统`
  - `水裂解前体` → `水裂解雏形`
- 与氮循环相关的基因名称和描述同步简化：
  - `氨同化` → `快速氮同化`
  - `硝酸盐同化` → `稳定氮同化`
  - `反硝化` → `脱氮呼吸`

---

## 2026-04-21 增加高度视图

### 修改文件

#### Core/SimulationConfig.cs
- 新增高度视图颜色常量：`AltitudeColorLow`、`AltitudeColorMid`、`AltitudeColorHigh`。
- 新增高度归一化范围：`AltitudeMin = 0`、`AltitudeMax = 1000`。

#### Managers/CellRenderer.cs
- `ViewMode` 枚举新增 `Altitude` 模式。
- `UpdateBackgroundTexture()` 新增高度着色逻辑：
  - 按 `Envir.Height` 在 0-1000 范围归一化。
  - 使用低海拔/中海拔/高海拔三段颜色插值绘制高度底图。
- 视图切换时不再只在首次初始化时更新底图，而是每次切换都重绘背景，以支持地形视图和高度视图之间切换。
- 温度/光照叠加层渲染条件收紧为仅在这两种模式下启用，高度视图不使用叠加层。

#### Core/MainManager.cs
- 右下角视图切换按钮从 3 个扩展为 4 个。
- 新增按钮：`高度视图`。

---

## 2026-04-21 提高高度视图对比度

### 修改文件

#### Core/SimulationConfig.cs
- 调整高度视图三段颜色常量，使高度分层更明显：
  - `AltitudeColorLow` 改为更深的蓝色
  - `AltitudeColorMid` 改为更亮的黄绿色/土黄色过渡
  - `AltitudeColorHigh` 改为更接近纯白的高山颜色
- 本次仅修改颜色配置，不改动高度视图渲染逻辑。

---

## 2026-04-21 高度视图改为等高分层图风格

### 修改文件

#### Core/SimulationConfig.cs
- 将高度视图颜色从 3 段渐变改为 6 段离散色带：深低地、浅低地、平原、高原、山地、高峰。
- 新增 `AltitudeContourLevels = 6`，用于控制高度分层数量。

#### Managers/CellRenderer.cs
- 将高度视图着色逻辑从连续渐变改为按高度区间分层取色。
- 高度视图现在表现为离散色带，更接近等高分层图/地形分层图风格。

---

## 2026-04-21 高度分层扩展到12段

### 修改文件

#### Core/SimulationConfig.cs
- 将高度视图色带从 6 段扩展到 12 段，提供更细的高程分层。
- `AltitudeContourLevels` 从 `6` 调整为 `12`。
- 新增 `AltitudeColorLevel7` 到 `AltitudeColorLevel12`，并重配全部 12 段颜色，使其从深蓝低地逐步过渡到白色高峰。

#### Managers/CellRenderer.cs
- `switch(level)` 的高度着色分支从 6 档扩展到 12 档。
- 现在高度图的分层更细，更接近密集色带的等高分层图效果。

---

## 2026-04-21 高度图改为20段真实地形图风格

### 修改文件

#### Core/SimulationConfig.cs
- 将高度视图配置从 12 个硬编码分层色改为 8 个真实地形图锚点色：深水、浅水、海岸平原、低地、高地、山前坡、山地、雪顶。
- `AltitudeContourLevels` 从 `12` 调整为 `20`。

#### Managers/CellRenderer.cs
- 高度图渲染从“12 档 switch 取固定色”改为“20 段量化 + 分段插值”。
- 新增 `GetAltitudeContourColor(float altitudeT)`，先把高度量化为 20 段，再按真实地形颜色锚点插值取色。
- 视觉效果从单纯分层色带调整为更接近真实地形图的蓝-绿-黄-棕-灰-白序列。

---

## 2026-04-21 强化海岸带与伪等高线

### 修改文件

#### Core/SimulationConfig.cs
- 新增 `AltitudeColorCoast`，用于单独强化海平面附近的海岸带显示。
- 新增 `AltitudeCoastBandWidth = 24`，控制海岸带宽度。
- 新增 `AltitudeContourDarken = 0.18f`，控制伪等高线压暗程度。

#### Managers/CellRenderer.cs
- 新增 `GetAltitudeQuantizedLevel(int height)`，统一高度分层计算。
- 将 `GetAltitudeContourColor` 改为直接基于高度值取色。
- 海平面以下全部改为分级蓝色显示，并限制在水下蓝色序列中，不再混入陆地颜色。
- 海平面以上单独增加海岸带高亮，再进入陆地真实地形色带。
- 在高度分层边界处比较上下左右相邻格分层编号，并对边界像素做压暗处理，形成伪等高线效果。
