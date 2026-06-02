# CodeMenu
代码简介
## 根目录

| 文件 | 简介 |
| --- | --- |
| auto-upload-git.ps1 | 自动初始化 Git、配置用户、提交并推送到远端仓库的脚本。 |

## Tools

| 文件 | 简介 |
| --- | --- |
| Tools/ChemistryReactionEditor/ChemistryReactionEditor.csproj | 化学反应外部编辑器 WPF 项目文件。 |
| Tools/ChemistryReactionEditor/MainWindow.xaml | 化学反应编辑器界面：物质表、反应列表、拖拽排序与反应详情。 |
| Tools/ChemistryReactionEditor/MainWindow.xaml.cs | 化学反应编辑器逻辑：读写 JSON、校验、拖拽排序与保存。 |
| Tools/ChemistryReactionEditor/Models.cs | 化学反应编辑器的数据绑定模型。 |
| Tools/ChemistryReactionEditor/README.md | 化学反应编辑器运行和表达式说明。 |
| Tools/ChemistryReactionEditor/run-editor.bat | Windows 下启动化学反应编辑器的脚本。 |

## StreamingAssets

| 文件 | 简介 |
| --- | --- |
| Assets/StreamingAssets/chemistry-reactions.json | 游戏运行时读取的化学物质与反应配置。 |

## Shaders

| 文件 | 简介 |
| --- | --- |
| Assets/Shaders/HeightNormalLit.shader | 地形底图+法线贴图的轻量光照 shader，支持高度与曲率增强。 |

## Assets/Scripts/Managers

| 文件 | 简介 |
| --- | --- |
| Assets/Scripts/Managers/VideoPlaylistPlayer.cs | 视频播放列表组件，支持按索引播放、前后切换与循环。 |
| Assets/Scripts/Managers/CellRenderer.cs | 细胞与地形渲染管理：背景、叠加层、网格线、视图模式与基因视图筛选渲染。 |
| Assets/Scripts/Managers/CameraController.cs | 正交相机缩放与平移控制，并计算可见网格范围。 |

## Assets/Scripts/Entities

| 文件 | 简介 |
| --- | --- |
| Assets/Scripts/Entities/Species.cs | 玩家与 NPC 细胞的初始基因与优先级配置。 |
| Assets/Scripts/Entities/Gene.cs | 基因结构体、升级哈希映射、baseId 名称/描述目录与全图基因统计。 |
| Assets/Scripts/Entities/Envir.cs | 环境格数据：温度、光照、地形、化学物质量、细胞列表与增删接口。 |
| Assets/Scripts/Entities/Cell.cs | 细胞数据、基因能耗缓存与 baseId 基因查询。 |

## Assets/Scripts/Core

| 文件 | 简介 |
| --- | --- |
| Assets/Scripts/Core/TerrainGenerator.cs | 基于可平铺柏林噪声生成高度与地形分类（河流/湖泊逻辑预留）。 |
| Assets/Scripts/Core/TemperatureBehavior.cs | 温度行为：在耐受区间内给予能量奖励，并支持并行执行。 |
| Assets/Scripts/Core/SimulationRenderSettingsData.cs | 渲染配置的序列化容器，支持与配置同步、哈希与应用。 |
| Assets/Scripts/Core/SimulationCore.cs | 核心模拟线程：世界初始化、行为循环、科研与温度换算。 |
| Assets/Scripts/Core/SimulationConfig.cs | 全局模拟与渲染参数配置中心。 |
| Assets/Scripts/Core/ChemistryConfigData.cs | 化学 JSON 配置的数据结构定义。 |
| Assets/Scripts/Core/ChemistryExpression.cs | 化学条件和动力方程的安全表达式解析与求值。 |
| Assets/Scripts/Core/ChemistrySystem.cs | 从 JSON 加载化学物质/反应，执行环境格非生物化学反应与热力图归一化。 |
| Assets/Scripts/Core/ResearchPlayerPanelTab.cs | 研发界面页签：显示研发点并提供温度耐受升级。 |
| Assets/Scripts/Core/PopulationPlayerPanelTab.cs | 种群页签占位实现，预留后续扩展。 |
| Assets/Scripts/Core/PlayerPanelTabPage.cs | 玩家面板页签基类与上下文数据结构。 |
| Assets/Scripts/Core/MultiplyBehavior.cs | 繁殖行为：并行预计算、按优先级生成子代。 |
| Assets/Scripts/Core/MapCreater.cpp | 空文件，占位或预留原生地图生成实现。 |
| Assets/Scripts/Core/MainManager.cs | Unity 主控脚本：启动模拟、UI 面板、存档/读档窗口（含删除与确认弹窗）。 |
| Assets/Scripts/Core/MainMenuManager.cs | 主菜单：新游戏/读取/设置，读取存档列表与删除存档。 |
| Assets/Scripts/Core/SaveSystem.cs | 存档读写：槽位元数据、二进制世界状态、截图、删除与纹理释放。 |
| Assets/Scripts/Core/LightUpdate.cs | 使用噪声与纬度曲线更新环境光照场。 |
| Assets/Scripts/Core/LightBehavior.cs | 光照行为：根据光照值进行能量获取。 |
| Assets/Scripts/Core/HeatDiffusion.cs | 温度更新：光照增温、散热与扩散。 |
| Assets/Scripts/Core/GenePlayerPanelTab.cs | 基因页签：全图基因列表（名称/描述/细胞数），点击切换基因视图筛选。 |
| Assets/Scripts/Core/EnvironmentPlayerPanelTab.cs | 环境页签：显示悬停/锁定环境格信息、物质列表与浓度热力图切换。 |
| Assets/Scripts/Core/DeathBehavior.cs | 死亡行为：温度、寿命与拥挤规则判定。 |
