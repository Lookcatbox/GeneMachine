// Envir.cs - 环境格数据类
using System;

/// <summary>单个地图格：气候、地形、细胞列表与各化学物质存量。</summary>
[Serializable]
public class Envir
{
    public int X;                 // 环境格 x 坐标（用于 ChemistryField SoA 索引）
    public int Y;                 // 环境格 y 坐标
    public float Temp;            // 温度
    public int Light;             // 光照强度
    public int CellNum;           // 当前细胞数量
    public int MaxCellNum;        // 最大容纳细胞数

    public int Height; //高度，介于-10000~10000之间，为整数

    public int Topography; // 地形类型（0=海洋, 1=陆地, 2=沙滩, 3=河流, 4=湖泊）

    public Cell[] CellList;       // 该格中的细胞列表（下标从1开始）

    public float[] ChemAmounts;   // 旧版/兜底存储；运行时主路径使用 ChemistryField SoA

    public Envir() : this(SimulationConfig.CellMaxNum)
    {
    }

    public Envir(int maxCellNum)
    {
        MaxCellNum = maxCellNum;
        CellList = new Cell[maxCellNum + 1];
        CellNum = 0;
        Temp = SimulationConfig.DefaultTemp;
        Light = SimulationConfig.DefaultLight;
        ChemAmounts = ChemistryField.IsAllocated ? null : new float[Math.Max(0, ChemistrySystem.SubstanceCount)];
    }

    public void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>扩展旧版兜底数组；SoA 已分配时无需逐格数组。</summary>
    public void EnsureChemicalCapacity(int count)
    {
        if (ChemistryField.IsAllocated && X > 0 && Y > 0)
            return;

        if (count < 0)
            count = 0;
        if (ChemAmounts == null)
        {
            ChemAmounts = new float[count];
            return;
        }
        if (ChemAmounts.Length >= count)
            return;

        Array.Resize(ref ChemAmounts, count);
    }

    /// <summary>读取物质存量；下标无效返回 0。</summary>
    public float GetChemicalAmount(int substanceIndex)
    {
        if (ChemistryField.IsAllocated && X > 0 && Y > 0)
            return ChemistryField.GetAmount(X, Y, substanceIndex);

        if (ChemAmounts == null || substanceIndex < 0 || substanceIndex >= ChemAmounts.Length)
            return 0f;
        return ChemAmounts[substanceIndex];
    }

    public float GetChemicalAmount(ChemicalSubstance substance)
    {
        return GetChemicalAmount((int)substance);
    }

    /// <summary>写入物质存量（负值钳制为 0）。</summary>
    public void SetChemicalAmount(int substanceIndex, float amount)
    {
        if (ChemistryField.IsAllocated && X > 0 && Y > 0)
        {
            ChemistryField.SetAmount(X, Y, substanceIndex, amount);
            return;
        }

        EnsureChemicalCapacity(substanceIndex + 1);
        if (substanceIndex < 0 || substanceIndex >= ChemAmounts.Length)
            return;
        ChemAmounts[substanceIndex] = amount < 0f ? 0f : amount;
    }

    public void SetChemicalAmount(ChemicalSubstance substance, float amount)
    {
        SetChemicalAmount((int)substance, amount);
    }

    /// <summary>增减物质存量。</summary>
    public void AddChemicalAmount(int substanceIndex, float delta)
    {
        if (ChemistryField.IsAllocated && X > 0 && Y > 0)
        {
            ChemistryField.AddAmount(X, Y, substanceIndex, delta);
            return;
        }

        SetChemicalAmount(substanceIndex, GetChemicalAmount(substanceIndex) + delta);
    }

    public void AddChemicalAmount(ChemicalSubstance substance, float delta)
    {
        AddChemicalAmount((int)substance, delta);
    }

    /// <summary>
    /// 添加细胞到该环境格。返回是否成功
    /// </summary>
    public bool AddCell(Cell cell)
    {
        if (CellNum >= MaxCellNum) return false;
        CellList[++CellNum] = cell;
        return true;
    }

    /// <summary>
    /// 移除指定下标的细胞（用最后一个填补空位）
    /// </summary>
    public void RemoveCell(int index)
    {
        if (index < 1 || index > CellNum) return;
        CellList[index] = CellList[CellNum];
        CellList[CellNum] = null;
        CellNum--;
    }

    /// <summary>
    /// 获取优先级最高的细胞
    /// </summary>
    public Cell GetHighestPriorityCell()
    {
        if (CellNum == 0) return null;
        Cell best = CellList[1];
        for (int i = 2; i <= CellNum; i++)
        {
            if (CellList[i] != null && CellList[i].priority > best.priority)
                best = CellList[i];
        }
        return best;
    }
}
