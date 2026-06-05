// DeviceSystem.cs - 装置类型注册、背包、放置、科研覆盖与存档状态
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>装置类型静态定义；<see cref="Range"/> 为欧拉距离作用半径（按类型配置，非全局常量）。</summary>
public class DeviceType
{
    public int TypeId;              // 装置类型唯一 id
    public string Name;             // 显示名称
    public string Description;      // 说明文本
    public bool Craftable;          // 是否可用研发点制造
    public int CraftMax;            // 制造上限，0 表示不限
    public int Range;               // 放置后效果半径（欧拉距离）
    public string IconResource;     // Resources 下图标路径
    public string PreviewResource;  // 放置预览纹理路径
    public Texture2D Icon;          // 运行时加载的图标
    public Texture2D Preview;       // 运行时加载的预览图
}

/// <summary>已放置在地图上的装置实例。</summary>
public struct DeviceInstance
{
    public int Id;      // 实例唯一 id
    public int TypeId;  // 对应 DeviceType.TypeId
    public int X;       // 环境格坐标
    public int Y;
}

/// <summary>装置背包、制造、放置与科研装置覆盖格统计。</summary>
public static class DeviceSystem
{
    private const int SeedStorageRange = 10;
    private const int ResearchStationRange = 10;

    private static bool initialized;
    private static int nextDeviceId = 1;
    private static readonly List<DeviceType> typeList = new List<DeviceType>();
    private static readonly Dictionary<int, DeviceType> typeMap = new Dictionary<int, DeviceType>();
    private static readonly Dictionary<int, int> inventory = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> craftCounts = new Dictionary<int, int>();
    private static readonly List<DeviceInstance> devices = new List<DeviceInstance>();
    private static readonly object deviceLock = new object();

    private static readonly List<int> researchCoverageIndices = new List<int>();
    private static readonly HashSet<int> researchCoverageSet = new HashSet<int>();
    private static bool researchCoverageDirty = true;

    private static bool isPlacing;
    private static int placingTypeId;

    public static bool IsPlacing => isPlacing;
    public static int PlacingTypeId => placingTypeId;

    /// <summary>注册装置类型并初始化背包（幂等）。</summary>
    public static void Init()
    {
        if (initialized)
            return;

        initialized = true;
        RegisterSeedStorage();
        RegisterResearchStation();
        ResetRuntimeState();
    }

    public static void ResetRuntimeState()
    {
        lock (deviceLock)
        {
            inventory.Clear();
            craftCounts.Clear();
            devices.Clear();
            nextDeviceId = 1;
            researchCoverageDirty = true;

            if (typeMap.ContainsKey(SimulationConfig.DeviceSeedStorageTypeId))
                inventory[SimulationConfig.DeviceSeedStorageTypeId] = SimulationConfig.DeviceSeedStorageInitialCount;
        }
    }

    public static IReadOnlyList<DeviceType> GetDeviceTypes()
    {
        return typeList;
    }

    public static int GetDeviceCount(int typeId)
    {
        lock (deviceLock)
        {
            return inventory.TryGetValue(typeId, out int count) ? count : 0;
        }
    }

    public static DeviceType GetDeviceType(int typeId)
    {
        typeMap.TryGetValue(typeId, out DeviceType type);
        return type;
    }

    public static int GetDeviceRange(int typeId)
    {
        DeviceType type = GetDeviceType(typeId);
        return type != null ? type.Range : 0;
    }

    public static int GetCraftCount(int typeId)
    {
        lock (deviceLock)
        {
            return craftCounts.TryGetValue(typeId, out int count) ? count : 0;
        }
    }

    public static int GetCraftMax(int typeId)
    {
        DeviceType type = GetDeviceType(typeId);
        return type != null ? type.CraftMax : 0;
    }

    public static bool CanCraftDevice(int typeId)
    {
        if (!typeMap.TryGetValue(typeId, out DeviceType type) || !type.Craftable)
            return false;
        if (type.CraftMax > 0 && GetCraftCount(typeId) >= type.CraftMax)
            return false;
        return GetCraftCost(typeId) > 0 && SimulationCore.GetResearchPoints() >= GetCraftCost(typeId);
    }

    public static int GetCraftCost(int typeId)
    {
        if (!typeMap.TryGetValue(typeId, out DeviceType type) || !type.Craftable)
            return 0;
        if (type.CraftMax > 0 && GetCraftCount(typeId) >= type.CraftMax)
            return 0;
        if (typeId == SimulationConfig.DeviceResearchStationTypeId)
        {
            int crafted = GetCraftCount(typeId);
            double factor = Math.Pow(SimulationConfig.ResearchDeviceCostMultiplier, crafted);
            double cost = SimulationConfig.ResearchDeviceBaseCost * factor;
            return Math.Max(1, (int)Math.Round(cost));
        }
        return 0;
    }

    public static bool TryCraftDevice(int typeId)
    {
        if (!typeMap.TryGetValue(typeId, out DeviceType type) || !type.Craftable)
            return false;
        if (type.CraftMax > 0 && GetCraftCount(typeId) >= type.CraftMax)
            return false;

        int cost = GetCraftCost(typeId);
        if (cost <= 0)
            return false;
        if (!SimulationCore.TrySpendResearchPoints(cost))
            return false;

        lock (deviceLock)
        {
            inventory[typeId] = GetDeviceCount(typeId) + 1;
            craftCounts[typeId] = GetCraftCount(typeId) + 1;
        }
        return true;
    }

    public static Texture2D GetPlacingPreviewTexture()
    {
        DeviceType type = GetDeviceType(placingTypeId);
        return type != null ? type.Preview : null;
    }

    public static void BeginPlacement(int typeId)
    {
        if (!typeMap.ContainsKey(typeId))
            return;
        if (GetDeviceCount(typeId) <= 0)
            return;

        isPlacing = true;
        placingTypeId = typeId;
    }

    public static void CancelPlacement()
    {
        isPlacing = false;
        placingTypeId = 0;
    }

    /// <summary>在地图格放置装置；种子库会立即对范围内格施加效果。</summary>
    public static bool TryPlaceDevice(int typeId, int x, int y)
    {
        if (!typeMap.ContainsKey(typeId))
            return false;
        if (GetDeviceCount(typeId) <= 0)
            return false;
        if (SimulationCore.EnvirData == null)
            return false;

        if (!SimulationCore.InBounds(x, y))
            return false;

        DeviceInstance instance;
        lock (deviceLock)
        {
            instance = new DeviceInstance
            {
                Id = nextDeviceId++,
                TypeId = typeId,
                X = x,
                Y = y
            };
            devices.Add(instance);
            inventory[typeId] = GetDeviceCount(typeId) - 1;
            researchCoverageDirty = true;
        }

        if (typeId == SimulationConfig.DeviceSeedStorageTypeId)
            ApplySeedStorageEffect(x, y);

        return true;
    }

    public static IReadOnlyList<DeviceInstance> GetPlacedDevices()
    {
        lock (deviceLock)
        {
            return devices.ToArray();
        }
    }

    public static bool TryGetDeviceAt(int x, int y, out DeviceInstance instance)
    {
        lock (deviceLock)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].X == x && devices[i].Y == y)
                {
                    instance = devices[i];
                    return true;
                }
            }

            instance = default;
            return false;
        }
    }

    /// <summary>统计所有科研装置覆盖的环境格数量（去重），用于每步研发点加成。</summary>
    public static int CountResearchCoverageCells()
    {
        if (SimulationCore.EnvirData == null)
            return 0;

        lock (deviceLock)
        {
            RebuildResearchCoverageIfNeeded();
            int size = SimulationConfig.EnvirSize;
            int stride = size + 2;
            int total = 0;
            for (int i = 0; i < researchCoverageIndices.Count; i++)
            {
                int packed = researchCoverageIndices[i];
                int x = packed / stride;
                int y = packed % stride;
                if (x < 1 || x > size || y < 1 || y > size)
                    continue;
                Envir env = SimulationCore.EnvirData[x, y];
                if (env != null)
                    total += env.CellNum;
            }
            return total;
        }
    }

    private static void RegisterSeedStorage()
    {
        DeviceType type = new DeviceType
        {
            TypeId = SimulationConfig.DeviceSeedStorageTypeId,
            Name = "种子仓",
            Description = "放置后在半径内的每格增加一个玩家细胞。",
            Craftable = false,
            CraftMax = 0,
            Range = SeedStorageRange,
            IconResource = "Devices/seed_bank_icon",
            PreviewResource = "Devices/seed_bank_preview"
        };

        type.Icon = Resources.Load<Texture2D>(type.IconResource);
        type.Preview = Resources.Load<Texture2D>(type.PreviewResource);

        RegisterDeviceType(type);
    }

    private static void RegisterResearchStation()
    {
        DeviceType type = new DeviceType
        {
            TypeId = SimulationConfig.DeviceResearchStationTypeId,
            Name = "科研装置",
            Description = "覆盖范围内的每个存活细胞提供研发点，重叠范围只计一次。",
            Craftable = true,
            CraftMax = SimulationConfig.DeviceResearchStationCraftMax,
            Range = ResearchStationRange,
            IconResource = "Devices/research_lab_icon",
            PreviewResource = "Devices/research_lab_preview"
        };

        type.Icon = Resources.Load<Texture2D>(type.IconResource);
        type.Preview = Resources.Load<Texture2D>(type.PreviewResource);

        RegisterDeviceType(type);
    }

    private static void RegisterDeviceType(DeviceType type)
    {
        typeList.Add(type);
        typeMap[type.TypeId] = type;
    }

    private static void ApplySeedStorageEffect(int centerX, int centerY)
    {
        int range = Mathf.Max(0, GetDeviceRange(SimulationConfig.DeviceSeedStorageTypeId));
        int rangeSq = range * range;
        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                int distSq = dx * dx + dy * dy;
                if (distSq > rangeSq)
                    continue;
                int nx = centerX + dx;
                int ny = centerY + dy;
                if (!SimulationCore.InBounds(nx, ny))
                    continue;

                Envir env = SimulationCore.EnvirData[nx, ny];
                if (env.CellNum >= env.MaxCellNum)
                    continue;

                Cell cell = new Cell(nx, ny, true);
                Species.InitPlayerCell(cell);
                if (env.AddCell(cell))
                    SimulationCore.AllCells.Add(cell);
            }
        }
    }

    private static void RebuildResearchCoverageIfNeeded()
    {
        if (!researchCoverageDirty)
            return;

        // 仅在装置放置/读档后重建；O(devices × range²)，结果缓存供 CountResearchCoverageCells 查询
        researchCoverageDirty = false;
        researchCoverageIndices.Clear();
        researchCoverageSet.Clear();

        int size = SimulationConfig.EnvirSize;
        int stride = size + 2;

        for (int i = 0; i < devices.Count; i++)
        {
            if (devices[i].TypeId != SimulationConfig.DeviceResearchStationTypeId)
                continue;

            int range = Mathf.Max(0, GetDeviceRange(devices[i].TypeId));
            if (range <= 0)
                continue;

            int rangeSq = range * range;
            int centerX = devices[i].X;
            int centerY = devices[i].Y;
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    int distSq = dx * dx + dy * dy;
                    if (distSq > rangeSq)
                        continue;

                    int nx = centerX + dx;
                    int ny = centerY + dy;
                    if (nx < 1 || nx > size || ny < 1 || ny > size)
                        continue;

                    int packed = nx * stride + ny;
                    if (researchCoverageSet.Add(packed))
                        researchCoverageIndices.Add(packed);
                }
            }
        }
    }

    public static DeviceSystemSaveData CaptureSaveState()
    {
        lock (deviceLock)
        {
            DeviceSystemSaveData data = new DeviceSystemSaveData
            {
                NextDeviceId = nextDeviceId,
                Devices = devices.ToArray(),
                Inventory = BuildInventoryEntries(inventory),
                CraftCounts = BuildInventoryEntries(craftCounts)
            };
            return data;
        }
    }

    public static void ApplySaveState(DeviceSystemSaveData data)
    {
        lock (deviceLock)
        {
            ResetRuntimeState();
            nextDeviceId = Math.Max(1, data.NextDeviceId);
            if (data.Inventory != null)
            {
                for (int i = 0; i < data.Inventory.Length; i++)
                    inventory[data.Inventory[i].TypeId] = data.Inventory[i].Count;
            }
            if (data.CraftCounts != null)
            {
                for (int i = 0; i < data.CraftCounts.Length; i++)
                    craftCounts[data.CraftCounts[i].TypeId] = data.CraftCounts[i].Count;
            }
            if (data.Devices != null)
                devices.AddRange(data.Devices);

            researchCoverageDirty = true;
        }
    }

    private static DeviceInventoryEntry[] BuildInventoryEntries(Dictionary<int, int> map)
    {
        DeviceInventoryEntry[] entries = new DeviceInventoryEntry[map.Count];
        int idx = 0;
        foreach (var kvp in map)
        {
            entries[idx++] = new DeviceInventoryEntry { TypeId = kvp.Key, Count = kvp.Value };
        }
        return entries;
    }
}

public struct DeviceSystemSaveData
{
    public int NextDeviceId;
    public DeviceInstance[] Devices;
    public DeviceInventoryEntry[] Inventory;
    public DeviceInventoryEntry[] CraftCounts;
}

public struct DeviceInventoryEntry
{
    public int TypeId;
    public int Count;
}
