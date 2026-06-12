// SimulationParallel.cs - 模拟与叠加层栅格化的并行度控制，为渲染主线程预留 CPU
using System;
using System.Threading.Tasks;

/// <summary>统一提供 <see cref="ParallelOptions"/>，按 <see cref="SimulationConfig"/> 为 UI 主线程预留核心。</summary>
public static class SimulationParallel
{
    static ParallelOptions simulationOptions;
    static ParallelOptions overlayRasterOptions;
    static int cachedSimulationReserve = int.MinValue;
    static int cachedOverlayMax = int.MinValue;

    /// <summary>后台模拟步内 Parallel.For/Invoke 使用；预留 <see cref="SimulationConfig.SimulationParallelReserveCores"/> 个逻辑核。</summary>
    public static ParallelOptions SimulationOptions
    {
        get
        {
            int reserve = Math.Max(0, SimulationConfig.SimulationParallelReserveCores);
            if (simulationOptions == null || cachedSimulationReserve != reserve)
            {
                int cores = Environment.ProcessorCount;
                int max = Math.Max(1, cores - reserve);
                simulationOptions = new ParallelOptions { MaxDegreeOfParallelism = max };
                cachedSimulationReserve = reserve;
            }
            return simulationOptions;
        }
    }

    /// <summary>主线程叠加层栅格化使用；限制并行度避免与 Unity 主线程抢满 CPU。</summary>
    public static ParallelOptions OverlayRasterOptions
    {
        get
        {
            int max = Math.Max(1, SimulationConfig.OverlayRasterMaxParallelism);
            if (overlayRasterOptions == null || cachedOverlayMax != max)
            {
                overlayRasterOptions = new ParallelOptions { MaxDegreeOfParallelism = max };
                cachedOverlayMax = max;
            }
            return overlayRasterOptions;
        }
    }
}
