// ChemistryField.cs - 按物质连续存储的环境化学存量（SoA）
using UnityEngine;

/// <summary>
/// 化学存量的 SoA 存储：每种物质一段连续 float[]，下标为 y * stride + x。
/// 相比 Envir 内部 float[]，扩散/叠加层可按物质顺序连续扫描，减少缓存跳转。
/// </summary>
public static class ChemistryField
{
    static float[][] amounts = new float[0][];
    static int size;
    static int stride;
    static int substanceCount;

    public static bool IsAllocated => amounts != null && amounts.Length > 0 && size > 0;
    public static int Size => size;
    public static int Stride => stride;
    public static int SubstanceCount => substanceCount;

    public static void Allocate(int worldSize, int count)
    {
        size = Mathf.Max(0, worldSize);
        stride = size + 2;
        substanceCount = Mathf.Max(0, count);
        amounts = new float[substanceCount][];
        int length = stride * stride;
        for (int i = 0; i < substanceCount; i++)
            amounts[i] = new float[length];
    }

    public static void EnsureAllocatedFromEnvirData(Envir[,] envirData)
    {
        int worldSize = SimulationConfig.EnvirSize;
        int count = ChemistrySystem.SubstanceCount;
        if (!IsAllocatedFor(worldSize, count))
        {
            float[][] oldAmounts = amounts;
            int oldSize = size;
            int oldCount = substanceCount;
            Allocate(worldSize, count);

            if (oldAmounts != null && oldSize == worldSize)
            {
                int copyCount = Mathf.Min(oldCount, count);
                for (int s = 0; s < copyCount; s++)
                {
                    if (oldAmounts[s] == null || amounts[s] == null)
                        continue;
                    int copyLength = Mathf.Min(oldAmounts[s].Length, amounts[s].Length);
                    System.Array.Copy(oldAmounts[s], amounts[s], copyLength);
                }
            }
        }

        if (envirData == null)
            return;

        for (int y = 1; y <= worldSize; y++)
        {
            for (int x = 1; x <= worldSize; x++)
            {
                Envir env = envirData[x, y];
                if (env == null)
                    continue;

                env.SetPosition(x, y);
                if (env.ChemAmounts == null)
                    continue;

                int copyCount = Mathf.Min(env.ChemAmounts.Length, count);
                int index = ToIndex(x, y);
                for (int s = 0; s < copyCount; s++)
                    amounts[s][index] = env.ChemAmounts[s];
                env.ChemAmounts = null;
            }
        }
    }

    public static bool IsAllocatedFor(int worldSize, int count)
    {
        return IsAllocated && size == worldSize && substanceCount >= count;
    }

    public static int ToIndex(int x, int y)
    {
        return y * stride + x;
    }

    public static float[] GetSubstanceBuffer(int substanceIndex)
    {
        if (substanceIndex < 0 || substanceIndex >= substanceCount)
            return null;
        return amounts[substanceIndex];
    }

    public static float GetAmount(int x, int y, int substanceIndex)
    {
        float[] buffer = GetSubstanceBuffer(substanceIndex);
        if (buffer == null || !InBounds(x, y))
            return 0f;
        return buffer[ToIndex(x, y)];
    }

    public static void SetAmount(int x, int y, int substanceIndex, float amount)
    {
        float[] buffer = GetSubstanceBuffer(substanceIndex);
        if (buffer == null || !InBounds(x, y))
            return;
        buffer[ToIndex(x, y)] = amount < 0f ? 0f : amount;
    }

    public static void AddAmount(int x, int y, int substanceIndex, float delta)
    {
        float[] buffer = GetSubstanceBuffer(substanceIndex);
        if (buffer == null || !InBounds(x, y))
            return;

        int index = ToIndex(x, y);
        float next = buffer[index] + delta;
        buffer[index] = next < 0f ? 0f : next;
    }

    static bool InBounds(int x, int y)
    {
        return x >= 1 && x <= size && y >= 1 && y <= size;
    }
}
