// TemperatureField.cs - 全图温度连续存储（SoA）
using UnityEngine;

/// <summary>
/// 环境温度的 SoA：单段 float[]，下标 y * stride + x。
/// 热扩散与 overlay 可连续扫描，Envir.Temp 在已分配时委托到此场。
/// </summary>
public static class TemperatureField
{
    static float[] temperatures;
    static int size;
    static int stride;

    public static bool IsAllocated => temperatures != null && size > 0;
    public static int Size => size;
    public static int Stride => stride;

    public static void Allocate(int worldSize)
    {
        size = Mathf.Max(0, worldSize);
        stride = size + 2;
        temperatures = new float[stride * stride];
    }

    public static bool IsAllocatedFor(int worldSize)
    {
        return IsAllocated && size == worldSize;
    }

    public static void EnsureAllocated(int worldSize)
    {
        if (!IsAllocatedFor(worldSize))
            Allocate(worldSize);
    }

    /// <summary>从 Envir 兜底字段迁入 SoA（读档后或旧路径兼容）。</summary>
    public static void EnsureAllocatedFromEnvirData(Envir[,] envirData)
    {
        int worldSize = SimulationConfig.EnvirSize;
        if (!IsAllocatedFor(worldSize))
            Allocate(worldSize);

        if (envirData == null)
            return;

        for (int y = 1; y <= worldSize; y++)
        {
            for (int x = 1; x <= worldSize; x++)
            {
                Envir env = envirData[x, y];
                if (env == null)
                    continue;
                temperatures[ToIndex(x, y)] = env.Temp;
            }
        }
    }

    public static int ToIndex(int x, int y)
    {
        return y * stride + x;
    }

    public static float[] GetBuffer()
    {
        return temperatures;
    }

    public static float Get(int x, int y)
    {
        if (!IsAllocated || !InBounds(x, y))
            return 0f;
        return temperatures[ToIndex(x, y)];
    }

    public static void Set(int x, int y, float value)
    {
        if (!IsAllocated || !InBounds(x, y))
            return;
        temperatures[ToIndex(x, y)] = value;
    }

    public static void Add(int x, int y, float delta)
    {
        if (!IsAllocated || !InBounds(x, y))
            return;
        temperatures[ToIndex(x, y)] += delta;
    }

    /// <summary>全图温度加偏移（摄氏→内部开尔文存储等）。</summary>
    public static void AddAll(float delta)
    {
        if (!IsAllocated || delta == 0f)
            return;
        for (int i = 0; i < temperatures.Length; i++)
            temperatures[i] += delta;
    }

    static bool InBounds(int x, int y)
    {
        return x >= 1 && x <= size && y >= 1 && y <= size;
    }
}
