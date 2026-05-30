// TemperatureBehavior.cs - 温度行为命名空间
// 严格遵循架构：二维委托表直接索引函数实现
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Temperature
{
    public static class Behavior
    {
        // ActionTable[扳机位, 基因id] = 该基因在该扳机处的行为实现函数指针
        private static Action<Cell, Gene>[,] ActionTable
            = new Action<Cell, Gene>[110, SimulationConfig.GeneNum + 1];

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

        // 本行为扳机1, 基因2: 基础温度耐受
        // 当环境温度在耐受范围(25-30)内时，给予少量能量(适应奖励)
        // 注意：温度超标致死逻辑在 Death 命名空间中处理
        private static void Func_1_2(Cell cell, Gene gene)
        {
            Envir env = SimulationCore.EnvirData[cell.px, cell.py];
            int upgradeHash = Gene.GetUpgradeHash(gene.id);
            float minTemp = SimulationCore.GetTempToleranceMinFromUpgradeHash(upgradeHash);
            float maxTemp = SimulationCore.GetTempToleranceMaxFromUpgradeHash(upgradeHash);
            if (env.Temp >= minTemp && env.Temp <= maxTemp)
            {
                Interlocked.Add(ref cell.energy, 2);
            }
        }

        // ============================================================
        // 初始化：注册所有基因实现到ActionTable
        // ============================================================
        public static void Init()
        {
            // 本行为的扳机编号独立，从1开始
            ActionTable[1, 2] = Func_1_2;
        }

        // ============================================================
        // ProcessCell：单细胞处理（供并行调用）
        // ============================================================
        public static void ProcessCell(Cell cell)
        {
            ExecuteTrigger(cell, 1);
        }

        public static void PreParallel()
        {
            var allCells = SimulationCore.AllCells;
            int count = allCells.Count;
            int triggerId = 1;

            if (count == 0)
                return;

            Parallel.ForEach(Partitioner.Create(0, count), range =>
            {
                for (int idx = range.Item1; idx < range.Item2; idx++)
                {
                    Cell cell = allCells[idx];
                    if (!cell.alive) continue;
                    ExecuteTrigger(cell, triggerId);
                }
            });
        }

        public static void Apply()
        {
            // 当前温度行为只修改自身状态，不需要独立apply阶段
        }

        // ============================================================
        // Pre阶段：遍历所有细胞（单线程版，保留向后兼容）
        // ============================================================
        public static void Pre()
        {
            var allCells = SimulationCore.AllCells;

            // === 本行为扳机1 ===
            int triggerId = 1;
            for (int idx = 0; idx < allCells.Count; idx++)
            {
                Cell cell = allCells[idx];
                if (!cell.alive) continue;

                ExecuteTrigger(cell, triggerId);
            }
        }
    }
}
