// HeatDiffusion.cs - 温度增温、散热与 8 邻域热扩散
using System.Threading.Tasks;
using UnityEngine;

/// <summary>每步更新全图温度场（三遍并行扫描）。</summary>
public static class HeatDiffusion
{
    private static float[,] tempBuffer;
    private static float[,] diffusionBuffer;
    private static int bufferSize;

    public static void Update(Envir[,] envirData)
    {
        if (envirData == null) return;

        int size = SimulationConfig.EnvirSize;
        EnsureBuffers(size);

        // --- 第 1 遍：增温 + 散热，O(EnvirSize²) 按行并行 ---
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

        Parallel.For(1, size + 1, y =>
        {
            for (int x = 1; x <= size; x++)
            {
                Envir env = envirData[x, y];
                float temp = env.Temp;
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
                tempBuffer[x, y] = temp;
            }
        });

        float diffusionStrength = Mathf.Clamp01(SimulationConfig.HeatDiffusionStrength);
        if (diffusionStrength > 0f)
        {
            // --- 第 2 遍：8 邻域热扩散，读 tempBuffer 写 diffusionBuffer ---
            Parallel.For(1, size + 1, y =>
            {
                int yDown = y == 1 ? size : y - 1;
                int yUp = y == size ? 1 : y + 1;

                for (int x = 1; x <= size; x++)
                {
                    int xLeft = x == 1 ? size : x - 1;
                    int xRight = x == size ? 1 : x + 1;

                    float center = tempBuffer[x, y];
                    float sum = tempBuffer[xLeft, y]
                        + tempBuffer[xRight, y]
                        + tempBuffer[x, yDown]
                        + tempBuffer[x, yUp]
                        + tempBuffer[xLeft, yDown]
                        + tempBuffer[xLeft, yUp]
                        + tempBuffer[xRight, yDown]
                        + tempBuffer[xRight, yUp];

                    float average = sum * 0.125f;
                    diffusionBuffer[x, y] = center + diffusionStrength * (average - center);
                }
            });
        }
        else
        {
            Parallel.For(1, size + 1, y =>
            {
                for (int x = 1; x <= size; x++)
                {
                    diffusionBuffer[x, y] = tempBuffer[x, y];
                }
            });
        }

        // --- 第 3 遍：写回 Envir.Temp ---
        Parallel.For(1, size + 1, y =>
        {
            for (int x = 1; x <= size; x++)
            {
                envirData[x, y].Temp = diffusionBuffer[x, y];
            }
        });
    }

    private static float GetConductionRate(int topography, float landRate, float sandRate, float waterRate)
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

    private static void EnsureBuffers(int size)
    {
        if (tempBuffer != null && diffusionBuffer != null && bufferSize == size)
            return;

        bufferSize = size;
        tempBuffer = new float[size + 2, size + 2];
        diffusionBuffer = new float[size + 2, size + 2];
    }
}
