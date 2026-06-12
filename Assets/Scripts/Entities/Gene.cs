// Gene.cs - 基因数据类（struct，值拷贝无GC）

using System.Collections.Generic;

/// <summary>全图某 baseId 基因在存活细胞中的出现次数（按细胞计一次）。</summary>
public struct GenePresenceEntry
{
    public int baseId;
    public int cellCount;
}

/// <summary>基因实例：baseId 分派行为，hashId 区分升级变体，energyCost 为每回合消耗。</summary>
public struct Gene
{
    public int baseId;       // 基类编号，用于行为函数分派
    public int hashId;       // 唯一身份 id，由 baseId + 升级情况 hash 生成
    public int energyCost;   // 每回合能量消耗

    // 当前基因编号与效果对应（以各行为 ActionTable 注册为准）
    // 0: 空位
    // 1: 繁殖（Multiply.Func_1_1）
    // 2: 温度耐受奖励（Temperature.Func_1_2）
    // 3: 光合作用产能（Light.Func_1_3）
    // 4: 自然寿命致死（Death.Func_3_4）
    // 5: 拥挤致死（Death.Func_4_5）

    private static readonly Dictionary<int, int> UpgradeSparseTable = new Dictionary<int, int>();

    // baseId -> 名称（与运行时行为一致）
    private static readonly Dictionary<int, string> BaseNameTable = new Dictionary<int, string>
    {
        { 1, "基础繁殖" },
        { 2, "基础温度耐受" },
        { 3, "基础光照需求" },
        { 4, "基础寿命" },
        { 5, "拥挤" },
    };

    // baseId -> 描述（摘自 Genelist.txt 前 5 项）
    private static readonly Dictionary<int, string> BaseDescriptionTable = new Dictionary<int, string>
    {
        { 1, "每回合10%几率尝试繁殖，在自身及周围八格中随机选择一个可用格生成子代。" },
        { 2, "基础耐温区间为 25-30，处于区间内时正常生存。" },
        { 3, "可在 0-100 光照区间维持基础代谢。" },
        { 4, "每回合 5% 几率自然死亡。" },
        { 5, "周围八连通个体数超过 6 时死亡。" },
    };

    public int id => hashId;

    public Gene(int baseId, int energyCost = 1, int upgradeHash = 0)
    {
        this.baseId = baseId;
        this.hashId = GetHashId(baseId, upgradeHash);
        this.energyCost = energyCost;

        if (this.hashId != 0 && upgradeHash != 0)
            UpgradeSparseTable[this.hashId] = upgradeHash;
    }

    public static int GetHashId(int baseId, int upgradeHash)
    {
        if (baseId == 0) return 0;
        if (upgradeHash == 0) return baseId;

        unchecked
        {
            int combined = (baseId << 24) | (upgradeHash & 0xFFFFFF);
            return combined == 0 ? -1 : combined;
        }
    }

    public static int GetUpgradeHash(int hashId)
    {
        if (hashId == 0 || hashId <= SimulationConfig.GeneNum)
            return 0;

        return UpgradeSparseTable.TryGetValue(hashId, out int upgradeHash)
            ? upgradeHash
            : 0;
    }

    public static string GetBaseName(int baseId)
    {
        if (BaseNameTable.TryGetValue(baseId, out string name))
            return name;
        return string.Format("基因 #{0}", baseId);
    }

    public static string GetBaseDescription(int baseId)
    {
        if (BaseDescriptionTable.TryGetValue(baseId, out string description))
            return description;
        return "暂无描述。";
    }

    static List<GenePresenceEntry> cachedPresenceList;
    static long cachedPresenceStep = -1;
    static readonly HashSet<int> presenceScratch = new HashSet<int>();

    /// <summary>新游戏/读档后清空基因统计缓存。</summary>
    public static void InvalidatePresenceListCache()
    {
        cachedPresenceList = null;
        cachedPresenceStep = -1;
    }

    /// <summary>
    /// 统计全图细胞中各 baseId 基因的出现次数（按细胞计数，同一细胞含多个相同 baseId 只计一次）。
    /// 结果按 <see cref="SimulationConfig.GeneListRefreshStepInterval"/> 步数缓存，避免 OnGUI 每帧 O(N) 扫描。
    /// </summary>
    public static List<GenePresenceEntry> BuildPresenceList()
    {
        long step = SimulationCore.totalSteps;
        int interval = SimulationConfig.GeneListRefreshStepInterval;
        if (interval < 1)
            interval = 1;

        if (cachedPresenceList != null && cachedPresenceStep >= 0 && step - cachedPresenceStep < interval)
            return cachedPresenceList;

        var counts = new Dictionary<int, int>();
        var cells = SimulationCore.AllCells;
        if (cells == null)
        {
            cachedPresenceList = new List<GenePresenceEntry>();
            cachedPresenceStep = step;
            return cachedPresenceList;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            if (cell == null || !cell.alive)
                continue;

            presenceScratch.Clear();
            CollectBaseIdsFromCell(cell, presenceScratch);
            foreach (int baseId in presenceScratch)
            {
                if (!counts.ContainsKey(baseId))
                    counts[baseId] = 0;
                counts[baseId]++;
            }
        }

        var result = new List<GenePresenceEntry>(counts.Count);
        foreach (var pair in counts)
        {
            result.Add(new GenePresenceEntry { baseId = pair.Key, cellCount = pair.Value });
        }
        result.Sort((a, b) => a.baseId.CompareTo(b.baseId));

        cachedPresenceList = result;
        cachedPresenceStep = step;
        return result;
    }

    private static void CollectBaseIdsFromCell(Cell cell, HashSet<int> seen)
    {
        if (cell.MainGeneList != null)
        {
            for (int i = 1; i < cell.MainGeneList.Length; i++)
            {
                int id = cell.MainGeneList[i].baseId;
                if (id != 0)
                    seen.Add(id);
            }
        }
        if (cell.SubGeneList != null)
        {
            for (int i = 1; i < cell.SubGeneList.Length; i++)
            {
                int id = cell.SubGeneList[i].baseId;
                if (id != 0)
                    seen.Add(id);
            }
        }
    }
}
