// Species.cs - 物种定义，管理基因初始化
using System;

/// <summary>为玩家/NPC 细胞填充默认基因组合。</summary>
public class Species
{
    /// <summary>
    /// 为玩家细胞初始化主干基因
    /// 基因编号:
    /// 1--基础繁殖 (10%几率繁殖，在自身及周围八格中随机选择一格繁殖)
    /// 2--基础温度耐受 (25-30)
    /// 3--基础光照需求 (0-100)
    /// 4--基础寿命 (每回合5%几率死亡)
    /// 5--拥挤 (周围八连通超过6个个体时死亡)
    /// </summary>
    public static void InitPlayerCell(Cell cell)
    {
        // 主干基因: 位于对应的扳机位置
        cell.MainGeneList[1] = new Gene(1, 2);  // 扳机1: 基础繁殖, 消耗2能量
        cell.MainGeneList[2] = new Gene(2, 1);  // 扳机2: 基础温度耐受, 消耗1能量
        cell.MainGeneList[3] = new Gene(3, 1);  // 扳机3: 基础光照需求, 消耗1能量
        cell.MainGeneList[4] = new Gene(4, 0);  // 扳机4: 基础寿命, 无消耗
        cell.MainGeneList[5] = new Gene(5, 0);  // 扳机5: 拥挤, 无消耗
        cell.priority = 1;
    }

    /// <summary>
    /// 为NPC细胞初始化自由基因（随机生成）
    /// NPC细胞仅拥有自由基因槽
    /// </summary>
    public static void InitNPCCell(Cell cell, Random rng)
    {
        // NPC只有自由基因，随机分配
        // 扳机1位置：随机给繁殖基因
        cell.SubGeneList[1] = new Gene(1, 2);  // 基础繁殖
        // 扳机2位置：温度耐受
        cell.SubGeneList[2] = new Gene(2, 1);
        // 扳机3位置：光照
        cell.SubGeneList[3] = new Gene(3, 1);
        // 扳机4位置：寿命
        cell.SubGeneList[4] = new Gene(4, 0);

        // 随机给一些额外的自由基因
        if (rng.NextDouble() < 0.3)
            cell.SubGeneList[5] = new Gene(5, 0); // 拥挤

        cell.priority = rng.Next(1, 3); // NPC优先级随机1-2
    }
}
