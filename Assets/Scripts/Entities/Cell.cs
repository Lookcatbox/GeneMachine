// Cell.cs - 细胞数据类
using System;

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
        this.px = px;
        this.py = py;
        this.isPlayer = isPlayer;
        this.energy = SimulationConfig.InitialEnergy;
        MainGeneList = new Gene[SimulationConfig.MaxMainGene + 1]; // 下标从1开始
        SubGeneList = new Gene[SimulationConfig.MaxSubGene + 1];   // struct数组自动初始化id=0
    }

    public void InvalidateEnergyCostCache() { _energyCostCache = -1; }

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
