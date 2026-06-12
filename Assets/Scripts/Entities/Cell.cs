// Cell.cs - 细胞数据类
using System;

/// <summary>单个生物个体：位置、能量、基因槽与能量消耗缓存。</summary>
[Serializable]
public class Cell
{
    public int px, py;          // 所处环境格位置
    public int energy;          // 能量
    public int priority = 1;    // 优先级，越大越高
    public bool alive = true;   // 是否存活
    public bool isPlayer;       // 是否为玩家控制的细胞

    public Gene[] MainGeneList; // 主干基因（不会退化，所有玩家细胞共享）
    public Gene[] SubGeneList;  // 自由基因（可变异/退化/掠夺）

    // 能量消耗缓存（基因变化时需调用InvalidateEnergyCostCache）
    private int _energyCostCache = -1;

    public Cell(int px, int py, bool isPlayer = true)
    {
        EnsureGeneArrays();
        ResetForSpawn(px, py, isPlayer);
    }

    /// <summary>从池取出后重置为新生细胞状态（不清空数组，只清槽位）。</summary>
    public void ResetForSpawn(int px, int py, bool isPlayer)
    {
        this.px = px;
        this.py = py;
        this.isPlayer = isPlayer;
        this.energy = SimulationConfig.InitialEnergy;
        this.priority = 1;
        this.alive = true;
        InvalidateEnergyCostCache();
        ClearGeneSlots();
    }

    /// <summary>归还池前：标记死亡并清空基因，避免脏数据残留。</summary>
    public void PrepareForPool()
    {
        alive = false;
        InvalidateEnergyCostCache();
        ClearGeneSlots();
    }

    public void InvalidateEnergyCostCache() { _energyCostCache = -1; }

    /// <summary>清空主干/自由基因槽（下标 0 未使用）。</summary>
    public void ClearGeneSlots()
    {
        EnsureGeneArrays();
        for (int i = 1; i < MainGeneList.Length; i++)
            MainGeneList[i] = default;
        for (int i = 1; i < SubGeneList.Length; i++)
            SubGeneList[i] = default;
    }

    void EnsureGeneArrays()
    {
        int mainLen = SimulationConfig.MaxMainGene + 1;
        int subLen = SimulationConfig.MaxSubGene + 1;
        if (MainGeneList == null || MainGeneList.Length != mainLen)
            MainGeneList = new Gene[mainLen];
        if (SubGeneList == null || SubGeneList.Length != subLen)
            SubGeneList = new Gene[subLen];
    }

    /// <summary>主干+自由基因每回合能量消耗之和（带缓存）。</summary>
    public int GetTotalEnergyCost()
    {
        if (_energyCostCache >= 0) return _energyCostCache;
        int total = 0;
        for (int i = 1; i < MainGeneList.Length; i++)
            if (MainGeneList[i].baseId != 0)
                total += MainGeneList[i].energyCost;
        for (int i = 1; i < SubGeneList.Length; i++)
            if (SubGeneList[i].baseId != 0)
                total += SubGeneList[i].energyCost;
        _energyCostCache = total;
        return total;
    }

    /// <summary>
    /// 检查细胞是否拥有指定 baseId 的基因（扫描主干与自由基因槽）。
    /// </summary>
    public bool HasGeneBaseId(int baseId)
    {
        if (baseId == 0)
            return false;
        if (MainGeneList != null)
        {
            for (int i = 1; i < MainGeneList.Length; i++)
                if (MainGeneList[i].baseId == baseId)
                    return true;
        }
        if (SubGeneList != null)
        {
            for (int i = 1; i < SubGeneList.Length; i++)
                if (SubGeneList[i].baseId == baseId)
                    return true;
        }
        return false;
    }
}
