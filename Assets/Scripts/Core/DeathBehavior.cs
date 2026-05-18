// DeathBehavior.cs - 死亡行为命名空间
// 严格遵循架构：二维委托表直接索引函数实现
// 根据FuncList映射：基因2→{2,4}, 基因3→{3,4}, 基因4→{4}, 基因5→{4}
// 本命名空间处理所有致死判定（行为4）
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Death
{
    public static class Behavior
    {
        // ActionTable[扳机位, 基因id] = 该基因在该扳机处的行为实现函数指针
        private static Action<Cell, Gene>[,] ActionTable
            = new Action<Cell, Gene>[110, SimulationConfig.GeneNum + 1];

        // 本行为的扳机序列独立，从1开始
        private static readonly int[] ActiveTriggers = { 1, 2, 3, 4 };

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

        // 本行为扳机1, 基因2: 温度致死
        // 环境温度超出25-30°C耐受范围时死亡
        private static void Func_1_2(Cell cell, Gene gene)
        {
            Envir env = SimulationCore.EnvirData[cell.px, cell.py];
            int upgradeHash = Gene.GetUpgradeHash(gene.id);
            float minTemp = SimulationCore.GetTempToleranceMinFromUpgradeHash(upgradeHash);
            float maxTemp = SimulationCore.GetTempToleranceMaxFromUpgradeHash(upgradeHash);
            if (env.Temp < minTemp || env.Temp > maxTemp)
                cell.alive = false;
        }

        // 本行为扳机2, 基因3: 光照致死（预留）
        // 当前光照范围0-100均可存活，暂不致死
        // 后续可添加：光照为0时死亡、极端光照伤害等
        private static void Func_2_3(Cell cell, Gene gene)
        {
            // 预留位，当前不产生致死效果
        }

        // 本行为扳机3, 基因4: 基础寿命
        // 每回合5%几率自然死亡
        private static void Func_3_4(Cell cell, Gene gene)
        {
            if (SimulationCore.EnsureThreadRng().NextDouble() < 0.05)
                cell.alive = false;
        }

        // 本行为扳机4, 基因5: 拥挤
        // 周围八连通格内细胞总数超过6个时死亡
        private static void Func_4_5(Cell cell, Gene gene)
        {
            int neighborCount = SimulationCore.GetNeighborCellCount(cell.px, cell.py);
            if (neighborCount > 6)
                cell.alive = false;
        }

        // ============================================================
        // 初始化：注册所有基因实现到ActionTable
        // ============================================================
        public static void Init()
        {
            // 本行为的扳机编号独立，从1开始
            ActionTable[1, 2] = Func_1_2;

            ActionTable[2, 3] = Func_2_3;

            ActionTable[3, 4] = Func_3_4;

            ActionTable[4, 5] = Func_4_5;
        }

        // ============================================================
        // ProcessCell：单细胞处理（供并行调用）
        // ============================================================
        public static void ProcessCell(Cell cell)
        {
            for (int t = 0; t < ActiveTriggers.Length; t++)
            {
                if (!cell.alive) return;
                int triggerId = ActiveTriggers[t];
                ExecuteTrigger(cell, triggerId);
            }
        }

        public static void PreParallel()
        {
            var allCells = SimulationCore.AllCells;
            int count = allCells.Count;

            if (count == 0)
                return;

            Parallel.ForEach(Partitioner.Create(0, count), range =>
            {
                SimulationCore.EnsureThreadRng();
                for (int idx = range.Item1; idx < range.Item2; idx++)
                {
                    Cell cell = allCells[idx];
                    if (!cell.alive) continue;

                    for (int t = 0; t < ActiveTriggers.Length; t++)
                    {
                        if (!cell.alive) break;
                        ExecuteTrigger(cell, ActiveTriggers[t]);
                    }
                }
            });
        }

        public static void Apply()
        {
            // 当前死亡行为只修改自身状态，不需要独立apply阶段
        }

        // ============================================================
        // Pre阶段：遍历所有细胞（单线程版，保留向后兼容）
        // ============================================================
        public static void Pre()
        {
            var allCells = SimulationCore.AllCells;

            // 对每个扳机位依次遍历所有细胞
            for (int t = 0; t < ActiveTriggers.Length; t++)
            {
                int triggerId = ActiveTriggers[t];
                for (int idx = 0; idx < allCells.Count; idx++)
                {
                    Cell cell = allCells[idx];
                    if (!cell.alive) continue;

                    ExecuteTrigger(cell, triggerId);
                }
            }
        }
    }
}
