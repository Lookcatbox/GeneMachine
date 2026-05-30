// Gene.cs - 基因数据类（struct，值拷贝无GC）

using System.Collections.Generic;

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
}
