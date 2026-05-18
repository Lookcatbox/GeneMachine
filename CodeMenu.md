# CodeMenu

## 根目录

| 文件 | 简介 |
| --- | --- |
| auto-upload-git.ps1 | 自动初始化 Git、配置用户、提交并推送到远端仓库的脚本。 |

## Shaders

| 文件 | 简介 |
| --- | --- |
| Assets/Shaders/HeightNormalLit.shader | 地形底图+法线贴图的轻量光照 shader，支持高度与曲率增强。 |

## Assets/Scripts/Managers

| 文件 | 简介 |
| --- | --- |
| Assets/Scripts/Managers/VideoPlaylistPlayer.cs | 视频播放列表组件，支持按索引播放、前后切换与循环。 |
| Assets/Scripts/Managers/CellRenderer.cs | 细胞与地形渲染管理：背景、叠加层、网格线与视图模式切换。 |
| Assets/Scripts/Managers/CameraController.cs | 正交相机缩放与平移控制，并计算可见网格范围。 |

## Assets/Scripts/Entities

| 文件 | 简介 |
| --- | --- |
| Assets/Scripts/Entities/Species.cs | 玩家与 NPC 细胞的初始基因与优先级配置。 |
| Assets/Scripts/Entities/Gene.cs | 基因结构体与升级哈希映射逻辑。 |
| Assets/Scripts/Entities/Envir.cs | 环境格数据：温度、光照、地形、细胞列表与增删接口。 |
| Assets/Scripts/Entities/Cell.cs | 细胞数据与基因能耗缓存计算。 |

## Assets/Scripts/Core

| 文件 | 简介 |
| --- | --- |
| Assets/Scripts/Core/TerrainGenerator.cs | 基于可平铺柏林噪声生成高度与地形分类（河流/湖泊逻辑预留）。 |
| Assets/Scripts/Core/TemperatureBehavior.cs | 温度行为：在耐受区间内给予能量奖励，并支持并行执行。 |
| Assets/Scripts/Core/SimulationRenderSettingsData.cs | 渲染配置的序列化容器，支持与配置同步、哈希与应用。 |
| Assets/Scripts/Core/SimulationCore.cs | 核心模拟线程：世界初始化、行为循环、科研与温度换算。 |
| Assets/Scripts/Core/SimulationConfig.cs | 全局模拟与渲染参数配置中心。 |
| Assets/Scripts/Core/ResearchPlayerPanelTab.cs | 研发界面页签：显示研发点并提供温度耐受升级。 |
| Assets/Scripts/Core/PopulationPlayerPanelTab.cs | 种群页签占位实现，预留后续扩展。 |
| Assets/Scripts/Core/PlayerPanelTabPage.cs | 玩家面板页签基类与上下文数据结构。 |
| Assets/Scripts/Core/MultiplyBehavior.cs | 繁殖行为：并行预计算、按优先级生成子代。 |
| Assets/Scripts/Core/MapCreater.cpp | 空文件，占位或预留原生地图生成实现。 |
| Assets/Scripts/Core/MainManager.cs | Unity 主控脚本：启动模拟、UI 面板、视图与光照控制。 |
| Assets/Scripts/Core/LightUpdate.cs | 使用噪声与纬度曲线更新环境光照场。 |
| Assets/Scripts/Core/LightBehavior.cs | 光照行为：根据光照值进行能量获取。 |
| Assets/Scripts/Core/HeatDiffusion.cs | 温度更新：光照增温、散热与扩散。 |
| Assets/Scripts/Core/GenePlayerPanelTab.cs | 基因页签占位实现，预留后续扩展。 |
| Assets/Scripts/Core/EnvironmentPlayerPanelTab.cs | 环境页签：显示鼠标悬停/锁定环境格的详细信息。 |
| Assets/Scripts/Core/DeathBehavior.cs | 死亡行为：温度、寿命与拥挤规则判定。 |
