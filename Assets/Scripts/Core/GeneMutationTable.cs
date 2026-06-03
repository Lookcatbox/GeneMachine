using System;
using System.Collections.Generic;

/// <summary>
/// 基因突变权重二维表 mutationWeights[i, j]：
/// 第 i 个基因（由 hashId 映射的稠密下标）突变为第 j 个基因的权重。
/// 权重只从二维表读取；初始化时用临时默认值填充表项，后续可逐格修改。
/// 抽样时 P(i→j) = mutationWeights[i,j] / Σ_k mutationWeights[i,k]。
/// </summary>
public static class GeneMutationTable
{
    struct GeneVariantEntry
    {
        public int hashId;
        public int baseId;
        public int upgradeHash;
    }

    const int InvalidIndex = -1;

    static GeneVariantEntry[] variants = Array.Empty<GeneVariantEntry>();
    static int[] hashIdToIndexSmall = Array.Empty<int>();
    static Dictionary<int, int> hashIdToIndexLarge = new Dictionary<int, int>();
    static int[,] mutationWeights = new int[0, 0];
    static int[] rowWeightSums = Array.Empty<int>();
    static bool initialized;

    public static int VariantCount => variants != null ? variants.Length : 0;

    public static void Init()
    {
        if (initialized)
            return;

        List<GeneVariantEntry> entries = BuildDefaultVariants();
        BuildIndexMap(entries);
        AllocateWeightTable();
        FillTemporaryDefaultWeights();
        RebuildRowWeightSums();
        initialized = true;
    }

    public static void Rebuild()
    {
        initialized = false;
        Init();
    }

    /// <summary>只读访问二维权重表 mutationWeights[i, j]。</summary>
    public static int GetMutationWeight(int sourceIndex, int targetIndex)
    {
        if (!IsValidIndexPair(sourceIndex, targetIndex))
            return 0;
        return mutationWeights[sourceIndex, targetIndex];
    }

    public static void SetMutationWeight(int sourceIndex, int targetIndex, int weight)
    {
        if (!IsValidIndexPair(sourceIndex, targetIndex))
            return;
        mutationWeights[sourceIndex, targetIndex] = Math.Max(0, weight);
        RebuildRowWeightSum(sourceIndex);
    }

    public static bool SetMutationWeightByHashId(int sourceHashId, int targetHashId, int weight)
    {
        if (!TryGetIndex(sourceHashId, out int sourceIndex))
            return false;
        if (!TryGetIndex(targetHashId, out int targetIndex))
            return false;
        SetMutationWeight(sourceIndex, targetIndex, weight);
        return true;
    }

    /// <summary>hashId → 稠密下标，O(1)。</summary>
    public static bool TryGetIndex(int hashId, out int index)
    {
        if (hashId == 0)
        {
            index = InvalidIndex;
            return false;
        }

        if (hashId > 0 && hashId < hashIdToIndexSmall.Length)
        {
            index = hashIdToIndexSmall[hashId];
            return index >= 0;
        }

        return hashIdToIndexLarge.TryGetValue(hashId, out index);
    }

    public static int GetHashIdByIndex(int index)
    {
        if (index < 0 || index >= variants.Length)
            return 0;
        return variants[index].hashId;
    }

    public static int GetRowWeightSum(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= rowWeightSums.Length)
            return 0;
        return rowWeightSums[sourceIndex];
    }

    /// <summary>
    /// 若 sourceHashId 要突变，按二维表权重加权抽样目标 hashId。
    /// </summary>
    public static bool TryRollMutationTarget(int sourceHashId, Random rng, out int targetHashId)
    {
        targetHashId = 0;
        if (rng == null || !TryGetIndex(sourceHashId, out int sourceIndex))
            return false;

        int total = rowWeightSums[sourceIndex];
        if (total <= 0)
            return false;

        int roll = rng.Next(total);
        int acc = 0;
        int count = variants.Length;
        for (int j = 0; j < count; j++)
        {
            int w = mutationWeights[sourceIndex, j];
            if (w <= 0)
                continue;
            acc += w;
            if (roll < acc)
            {
                targetHashId = variants[j].hashId;
                return true;
            }
        }

        return false;
    }

    static List<GeneVariantEntry> BuildDefaultVariants()
    {
        List<GeneVariantEntry> entries = new List<GeneVariantEntry>();

        for (int baseId = 1; baseId <= 5; baseId++)
            AddVariant(entries, Gene.GetHashId(baseId, 0), baseId, 0);

        int maxLevel = SimulationConfig.ResearchTempUpgradeMaxLevel;
        for (int low = 0; low <= maxLevel; low++)
        {
            for (int high = 0; high <= maxLevel; high++)
            {
                int upgradeHash = SimulationCore.EncodeTempUpgradeHash(low, high);
                int hashId = Gene.GetHashId(2, upgradeHash);
                if (hashId == 2)
                    continue;
                AddVariant(entries, hashId, 2, upgradeHash);
            }
        }

        return entries;
    }

    static void AddVariant(List<GeneVariantEntry> entries, int hashId, int baseId, int upgradeHash)
    {
        if (hashId == 0)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].hashId == hashId)
                return;
        }

        entries.Add(new GeneVariantEntry
        {
            hashId = hashId,
            baseId = baseId,
            upgradeHash = upgradeHash
        });
    }

    static void BuildIndexMap(List<GeneVariantEntry> entries)
    {
        variants = entries.ToArray();
        int count = variants.Length;
        hashIdToIndexSmall = new int[SimulationConfig.GeneNum + 1];
        for (int i = 0; i < hashIdToIndexSmall.Length; i++)
            hashIdToIndexSmall[i] = InvalidIndex;
        hashIdToIndexLarge = new Dictionary<int, int>();

        for (int i = 0; i < count; i++)
        {
            int hashId = variants[i].hashId;
            if (hashId > 0 && hashId <= SimulationConfig.GeneNum)
                hashIdToIndexSmall[hashId] = i;
            else
                hashIdToIndexLarge[hashId] = i;
        }
    }

    static void AllocateWeightTable()
    {
        int count = variants.Length;
        mutationWeights = new int[count, count];
        rowWeightSums = new int[count];
    }

    /// <summary>
    /// 临时默认填表：同系列升降级邻居写 20，其余非自身写 10。
    /// 仅用于初始化；运行时以 mutationWeights 二维表为准。
    /// </summary>
    static void FillTemporaryDefaultWeights()
    {
        int count = variants.Length;
        const int tempSameSeriesWeight = 20;
        const int tempOtherWeight = 10;

        for (int i = 0; i < count; i++)
        {
            for (int j = 0; j < count; j++)
            {
                if (i == j)
                {
                    mutationWeights[i, j] = 0;
                    continue;
                }

                mutationWeights[i, j] = IsSameSeriesUpgradeNeighbor(variants[i], variants[j])
                    ? tempSameSeriesWeight
                    : tempOtherWeight;
            }
        }
    }

    static void RebuildRowWeightSums()
    {
        int count = variants.Length;
        for (int i = 0; i < count; i++)
            RebuildRowWeightSum(i);
    }

    static void RebuildRowWeightSum(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= variants.Length)
            return;

        int sum = 0;
        int count = variants.Length;
        for (int j = 0; j < count; j++)
            sum += mutationWeights[sourceIndex, j];
        rowWeightSums[sourceIndex] = sum;
    }

    /// <summary>
    /// 同系列升降级：相同 baseId，且在升级维度上仅变动 1 级（低温或高温耐受各 ±1）。
    /// 仅用于 FillTemporaryDefaultWeights，不参与运行时权重查询。
    /// </summary>
    static bool IsSameSeriesUpgradeNeighbor(GeneVariantEntry a, GeneVariantEntry b)
    {
        if (a.baseId != b.baseId || a.baseId != 2)
            return false;

        SimulationCore.DecodeTempUpgradeHash(a.upgradeHash, out int aLow, out int aHigh);
        SimulationCore.DecodeTempUpgradeHash(b.upgradeHash, out int bLow, out int bHigh);
        return Math.Abs(aLow - bLow) + Math.Abs(aHigh - bHigh) == 1;
    }

    static bool IsValidIndexPair(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || targetIndex < 0)
            return false;
        int count = variants.Length;
        return sourceIndex < count && targetIndex < count;
    }
}
