// Envir.cs - 环境格数据类
using System;

[Serializable]
public class Envir
{
    public float Temp;            // 温度
    public int Light;             // 光照强度
    public int CellNum;           // 当前细胞数量
    public int MaxCellNum;        // 最大容纳细胞数

    public int Height; //高度，介于0-1000之间，为整数

    public int Topography; // 地形类型（0=海洋, 1=陆地, 2=沙滩, 3=河流, 4=湖泊）

    public Cell[] CellList;       // 该格中的细胞列表（下标从1开始）

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
