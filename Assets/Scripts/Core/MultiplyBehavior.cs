// MultiplyBehavior.cs - 繁殖行为命名空间
// 严格遵循架构：二维委托表直接索引函数实现
// 支持多线程并行Pre阶段（线程本地缓冲 + ConcurrentBag合并）
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Multiply
{
    public struct MultiplyCommand
    {
        public Cell parent;
        public int targetX, targetY;
        public int priority;
    }

    /// <summary>繁殖行为：扳机表驱动 Pre（并行收集）与 Apply（排序后生成子代）。</summary>
    public static class Behavior
    {
        // ActionTable[扳机位, 基因id] = 该基因在该扳机处的行为实现函数指针
        private static Action<Cell, Gene>[,] ActionTable
            = new Action<Cell, Gene>[110, SimulationConfig.GeneNum + 1];

        // 主缓冲区（Apply阶段使用）
        private static List<MultiplyCommand> Buffer = new List<MultiplyCommand>();

        // 线程本地存储：繁殖缓冲（避免锁） + 候选位置数组（避免per-cell的new List）
        [ThreadStatic] private static List<MultiplyCommand> tls_buffer;
        [ThreadStatic] private static int[] tls_candidates;

        private static void ExecuteTrigger(Cell cell, int triggerId)
        {
            for (int i = 1; i < cell.MainGeneList.Length; i++)
            {
                Gene gene = cell.MainGeneList[i];
                if (gene.baseId == 0) continue;
                Action<Cell, Gene> action = ActionTable[triggerId, gene.baseId];
                if (action != null) action(cell, gene);
            }

            for (int i = 1; i < cell.SubGeneList.Length; i++)
            {
                Gene gene = cell.SubGeneList[i];
                if (gene.baseId == 0) continue;
                Action<Cell, Gene> action = ActionTable[triggerId, gene.baseId];
                if (action != null) action(cell, gene);
            }
        }

        // ============================================================
        // Func_i_j: 扳机i处，基因j的行为实现
        // ============================================================

        // 扳机1, 基因1: 基础繁殖
        // 10%几率繁殖，在自身及周围八格中随机选择一格能繁殖的环境
        private static void Func_1_1(Cell cell, Gene gene)
        {
            var rng = SimulationCore.EnsureThreadRng();
            if (rng.NextDouble() >= 0.10) return;

            var env = SimulationCore.EnvirData;
            if (tls_candidates == null) tls_candidates = new int[9];
            int cc = 0;

            if (env[cell.px, cell.py].CellNum < env[cell.px, cell.py].MaxCellNum)
                tls_candidates[cc++] = -1;

            for (int d = 0; d < 8; d++)
            {
                int nx = cell.px + SimulationCore.DX[d];
                int ny = cell.py + SimulationCore.DY[d];
                if (SimulationCore.InBounds(nx, ny) &&
                    env[nx, ny].CellNum < env[nx, ny].MaxCellNum)
                    tls_candidates[cc++] = d;
            }

            if (cc == 0) return;

            int chosen = tls_candidates[rng.Next(cc)];
            int tx = chosen == -1 ? cell.px : cell.px + SimulationCore.DX[chosen];
            int ty = chosen == -1 ? cell.py : cell.py + SimulationCore.DY[chosen];

            tls_buffer.Add(new MultiplyCommand
            {
                parent = cell,
                targetX = tx,
                targetY = ty,
                priority = cell.priority
            });
        }

        // ============================================================
        // 初始化：注册所有基因实现到ActionTable
        // ============================================================
        public static void Init()
        {
            // 本行为的扳机编号独立，从1开始
            ActionTable[1, 1] = Func_1_1;
        }

        // ============================================================
        // PreParallel：并行版Pre，线程本地缓冲 + ConcurrentBag合并
        // O(N × genesPerCell)；ConcurrentBag.ToArray 合并有少量分配，但避免全局锁
        // ============================================================
        public static void PreParallel()
        {
            var allCells = SimulationCore.AllCells;
            int count = allCells.Count;
            var collectedBuffers = new ConcurrentBag<MultiplyCommand[]>();

            if (count == 0)
            {
                Buffer.Clear();
                return;
            }

            Parallel.ForEach(Partitioner.Create(0, count), range =>
            {
                // 惰性初始化线程本地RNG和缓冲
                SimulationCore.EnsureThreadRng();
                if (tls_buffer == null) tls_buffer = new List<MultiplyCommand>();
                else tls_buffer.Clear();

                int triggerId = 1;
                for (int idx = range.Item1; idx < range.Item2; idx++)
                {
                    Cell cell = allCells[idx];
                    if (!cell.alive) continue;

                    ExecuteTrigger(cell, triggerId);
                }

                if (tls_buffer.Count > 0)
                    collectedBuffers.Add(tls_buffer.ToArray());
            });

            Buffer.Clear();
            foreach (var arr in collectedBuffers)
                Buffer.AddRange(arr);
        }

        // ============================================================
        // Pre：单线程版（保留向后兼容）
        // ============================================================
        public static void Pre()
        {
            SimulationCore.EnsureThreadRng();
            tls_buffer = new List<MultiplyCommand>();
            var allCells = SimulationCore.AllCells;

            int triggerId = 1;
            for (int idx = 0; idx < allCells.Count; idx++)
            {
                Cell cell = allCells[idx];
                if (!cell.alive) continue;

                ExecuteTrigger(cell, triggerId);
            }

            Buffer.Clear();
            Buffer.AddRange(tls_buffer);
        }

        // ============================================================
        // Apply阶段：按优先级排序缓冲区，依次执行繁殖
        // Buffer.Sort 为 O(M log M)，M=本步繁殖命令数；通常 M << N
        // ============================================================
        public static void Apply()
        {
            Buffer.Sort((a, b) => b.priority.CompareTo(a.priority));

            var envirData = SimulationCore.EnvirData;
            var allCells = SimulationCore.AllCells;

            for (int i = 0; i < Buffer.Count; i++)
            {
                var cmd = Buffer[i];
                Envir targetEnvir = envirData[cmd.targetX, cmd.targetY];
                if (targetEnvir.CellNum >= targetEnvir.MaxCellNum) continue;

                Cell child = CellPool.Rent(cmd.targetX, cmd.targetY, cmd.parent.isPlayer);
                // struct Gene 直接值拷贝，无需Clone
                for (int g = 1; g < cmd.parent.MainGeneList.Length; g++)
                    child.MainGeneList[g] = cmd.parent.MainGeneList[g];
                for (int g = 1; g < cmd.parent.SubGeneList.Length; g++)
                    child.SubGeneList[g] = cmd.parent.SubGeneList[g];
                child.priority = cmd.parent.priority;
                child.InvalidateEnergyCostCache();

                if (targetEnvir.AddCell(child))
                    allCells.Add(child);
                else
                    CellPool.Return(child);
            }
            Buffer.Clear();
        }
    }
}
