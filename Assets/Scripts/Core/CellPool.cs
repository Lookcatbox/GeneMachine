// CellPool.cs - 细胞对象池：复用 Cell 与基因数组，降低繁殖/死亡 churn 的分配与 GC
using System.Collections.Generic;

/// <summary>
/// 模拟层细胞池。Rent/Return 带锁，供模拟线程与主线程（读档、装置）安全复用。
/// </summary>
public static class CellPool
{
    static readonly Stack<Cell> pool = new Stack<Cell>(512);
    static readonly object gate = new object();

    /// <summary>池中可复用实例数（调试用）。</summary>
    public static int PooledCount
    {
        get
        {
            lock (gate)
                return pool.Count;
        }
    }

    /// <summary>取出或新建细胞，并初始化为可_spawn 状态（清空基因槽）。</summary>
    public static Cell Rent(int px, int py, bool isPlayer)
    {
        Cell cell;
        lock (gate)
        {
            cell = pool.Count > 0 ? pool.Pop() : null;
        }

        if (cell == null)
            return new Cell(px, py, isPlayer);

        cell.ResetForSpawn(px, py, isPlayer);
        return cell;
    }

    /// <summary>归还细胞到池（清空基因槽，标记非存活）。</summary>
    public static void Return(Cell cell)
    {
        if (cell == null)
            return;

        cell.PrepareForPool();
        lock (gate)
            pool.Push(cell);
    }

    /// <summary>将列表中所有细胞归还并清空列表。</summary>
    public static void ReturnAllFromList(List<Cell> cells)
    {
        if (cells == null || cells.Count == 0)
            return;

        lock (gate)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                Cell cell = cells[i];
                if (cell == null)
                    continue;
                cell.PrepareForPool();
                pool.Push(cell);
            }
        }
        cells.Clear();
    }

    /// <summary>丢弃池中全部实例（如新游戏重建世界前彻底释放引用）。</summary>
    public static void Clear()
    {
        lock (gate)
            pool.Clear();
    }
}
