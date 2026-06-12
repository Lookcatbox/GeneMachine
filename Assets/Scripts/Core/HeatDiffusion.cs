// HeatDiffusion.cs - 温度增温、散热与 8 邻域热扩散（TemperatureField SoA）
using System.Threading.Tasks;
using UnityEngine;

/// <summary>每步更新全图温度场（三遍并行扫描，直接在 SoA 与平坦 scratch 上运算）。</summary>
public static class HeatDiffusion
{
    static float[] flatTempScratch;
    static float[] flatDiffusionScratch;
    static int bufferStride;

    public static void Update(Envir[,] envirData)
    {
        if (envirData == null || !TemperatureField.IsAllocated)
            return;

        int size = SimulationConfig.EnvirSize;
        int stride = TemperatureField.Stride;
        float[] tempField = TemperatureField.GetBuffer();
        if (tempField == null)
            return;

        EnsureFlatBuffers(stride);

        float lightGainAtFull = SimulationConfig.HeatLightGainAtFull;
        float baseLoss = SimulationConfig.HeatLossLand;
        float conductionLand = SimulationConfig.HeatConductionLand;
        float conductionSand = SimulationConfig.HeatConductionSand;
        float conductionWater = SimulationConfig.HeatConductionWater;
        float kelvinOffset = SimulationConfig.KelvinOffset;
        float efficiencyMinTempC = SimulationConfig.HeatLossEfficiencyTempMin;
        float efficiencyZeroTempC = SimulationConfig.HeatLossEfficiencyTempZero;
        float efficiencyMaxTempC = SimulationConfig.HeatLossEfficiencyTempMax;
        float efficiencyAtZero = SimulationConfig.HeatLossEfficiencyAtZero;
        float efficiencyAtMax = SimulationConfig.HeatLossEfficiencyAtMax;
        float midPower = Mathf.Max(0.01f, SimulationConfig.HeatLossEfficiencyMidCurvePower);
        float lowPower = Mathf.Max(0.01f, SimulationConfig.HeatLossEfficiencyLowCurvePower);

        // --- 第 1 遍：增温 + 散热，读 SoA 写 scratch ---
        Parallel.For(1, size + 1, SimulationParallel.SimulationOptions, y =>
        {
            int rowBase = y * stride;
            for (int x = 1; x <= size; x++)
            {
                Envir env = envirData[x, y];
                int index = rowBase + x;
                float temp = tempField[index];
                float conduction = GetConductionRate(env.Topography, conductionLand, conductionSand, conductionWater);
                float light01 = env.Light / 100f;
                temp += light01 * lightGainAtFull * conduction;
                float tempC = temp - kelvinOffset;
                float lossEfficiency;
                if (tempC <= efficiencyZeroTempC)
                {
                    float denom = Mathf.Max(0.0001f, efficiencyZeroTempC - efficiencyMinTempC);
                    float t = Mathf.Clamp01((tempC - efficiencyMinTempC) / denom);
                    lossEfficiency = efficiencyAtZero * Mathf.Pow(t, lowPower);
                }
                else
                {
                    float denom = Mathf.Max(0.0001f, efficiencyMaxTempC - efficiencyZeroTempC);
                    float t = (tempC - efficiencyZeroTempC) / denom;
                    float tClamped = Mathf.Clamp01(t);
                    float baseEfficiency = Mathf.Lerp(efficiencyAtZero, efficiencyAtMax, Mathf.Pow(tClamped, midPower));
                    if (t <= 1f)
                    {
                        lossEfficiency = baseEfficiency;
                    }
                    else
                    {
                        float slope = (efficiencyAtMax - efficiencyAtZero) * midPower / denom;
                        lossEfficiency = efficiencyAtMax + slope * (tempC - efficiencyMaxTempC);
                    }
                }
                if (lossEfficiency < 0f)
                    lossEfficiency = 0f;
                float lossRate = baseLoss * lossEfficiency * conduction;
                temp *= 1f - lossRate;
                flatTempScratch[index] = temp;
            }
        });

        float diffusionStrength = Mathf.Clamp01(SimulationConfig.HeatDiffusionStrength);
        if (diffusionStrength > 0f)
        {
            // --- 第 2 遍：8 邻域热扩散 ---
            Parallel.For(1, size + 1, SimulationParallel.SimulationOptions, y =>
            {
                int yDown = y == 1 ? size : y - 1;
                int yUp = y == size ? 1 : y + 1;
                int rowBase = y * stride;
                int rowDown = yDown * stride;
                int rowUp = yUp * stride;

                for (int x = 1; x <= size; x++)
                {
                    int xLeft = x == 1 ? size : x - 1;
                    int xRight = x == size ? 1 : x + 1;
                    int centerIndex = rowBase + x;

                    float center = flatTempScratch[centerIndex];
                    float sum = flatTempScratch[rowBase + xLeft]
                        + flatTempScratch[rowBase + xRight]
                        + flatTempScratch[rowDown + x]
                        + flatTempScratch[rowUp + x]
                        + flatTempScratch[rowDown + xLeft]
                        + flatTempScratch[rowDown + xRight]
                        + flatTempScratch[rowUp + xLeft]
                        + flatTempScratch[rowUp + xRight];

                    float average = sum * 0.125f;
                    flatDiffusionScratch[centerIndex] = center + diffusionStrength * (average - center);
                }
            });
        }
        else
        {
            Parallel.For(1, size + 1, SimulationParallel.SimulationOptions, y =>
            {
                int rowBase = y * stride;
                for (int x = 1; x <= size; x++)
                    flatDiffusionScratch[rowBase + x] = flatTempScratch[rowBase + x];
            });
        }

        // --- 第 3 遍：写回 SoA ---
        Parallel.For(1, size + 1, SimulationParallel.SimulationOptions, y =>
        {
            int rowBase = y * stride;
            for (int x = 1; x <= size; x++)
            {
                int index = rowBase + x;
                tempField[index] = flatDiffusionScratch[index];
            }
        });
    }

    static float GetConductionRate(int topography, float landRate, float sandRate, float waterRate)
    {
        switch (topography)
        {
            case 2:
                return sandRate;
            case 0:
            case 3:
            case 4:
                return waterRate;
            default:
                return landRate;
        }
    }

    static void EnsureFlatBuffers(int stride)
    {
        if (flatTempScratch != null && flatDiffusionScratch != null && bufferStride == stride)
            return;

        bufferStride = stride;
        int length = stride * stride;
        flatTempScratch = new float[length];
        flatDiffusionScratch = new float[length];
    }
}
