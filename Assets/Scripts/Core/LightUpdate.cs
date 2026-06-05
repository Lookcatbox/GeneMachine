// LightUpdate.cs - 每回合更新环境格光照（柏林噪声 + 纬度曲线）
using System.Threading.Tasks;
using UnityEngine;

/// <summary>并行更新全图 <see cref="Envir.Light"/>。</summary>
public static class LightUpdate
{
    private static bool seeded = false;
    private static int zIndex = 0;
    private static float baseOffsetX;
    private static float baseOffsetY;
    private static float baseOffsetZ;

    public static void Update(Envir[,] envirData)
    {
        if (envirData == null) return;

        // O(EnvirSize² × octaves × 3 Perlin)；octaves 与 EnvirSize 增大时线性/平方放大
        EnsureSeeded();
        int size = SimulationConfig.EnvirSize;
        int minLight = Mathf.RoundToInt(SimulationConfig.LightMin);
        int maxLight = Mathf.RoundToInt(SimulationConfig.LightMax);
        float baseScale = SimulationConfig.LightNoiseScale;
        float zCoord = baseOffsetZ + zIndex * SimulationConfig.LightNoiseZStep;
        int octaves = Mathf.Max(1, SimulationConfig.LightNoiseOctaves);
        float frequencyMultiplier = Mathf.Max(1f, SimulationConfig.LightNoiseFrequencyMultiplier);
        float weightDecay = Mathf.Clamp01(SimulationConfig.LightNoiseWeightDecay);

        float weight = 1f;
        float maxWeight = 0f;
        for (int i = 0; i < octaves; i++)
        {
            maxWeight += weight;
            weight *= weightDecay;
        }

        Parallel.For(1, size + 1, y =>
        {
            float ny = size > 1 ? (y - 1f) / (size - 1f) : 0f;
            float latitudeRad = Mathf.Abs((ny - 0.5f) * Mathf.PI);
            float tiltRad = Mathf.Abs(SimulationConfig.LightLatitudeAxialTiltDegrees) * Mathf.Deg2Rad;
            float sunAngleSummer = Mathf.Max(0f, Mathf.Cos(latitudeRad - tiltRad));
            float sunAngleWinter = Mathf.Max(0f, Mathf.Cos(latitudeRad + tiltRad));
            float sunAngleAnnual = 0.5f * (sunAngleSummer + sunAngleWinter);
            float sunAnglePole = 0.5f * Mathf.Sin(tiltRad);
            float sunAngleEquator = Mathf.Cos(tiltRad);
            float denom = Mathf.Max(0.0001f, sunAngleEquator - sunAnglePole);
            float curveT = Mathf.Clamp01((sunAngleAnnual - sunAnglePole) / denom);
            float latitudeMultiplier = Mathf.Lerp(
                SimulationConfig.LightLatitudeMinMultiplier,
                SimulationConfig.LightLatitudeMaxMultiplier,
                curveT);
            for (int x = 1; x <= size; x++)
            {
                float nx = size > 1 ? (x - 1f) / (size - 1f) : 0f;
                float frequency = 1f;
                float layerWeight = 1f;
                float noiseSum = 0f;

                for (int layer = 0; layer < octaves; layer++)
                {
                    float tilePeriod = Mathf.Max(0.0001f, (size - 1f) * baseScale * frequency);
                    float sx = baseOffsetX + nx * tilePeriod;
                    float sy = baseOffsetY + ny * tilePeriod;
                    float sz = zCoord * frequency;

                    float n1 = Mathf.PerlinNoise(sx, sy);
                    float n2 = Mathf.PerlinNoise(sy, sz);
                    float n3 = Mathf.PerlinNoise(sz, sx);
                    float n = (n1 + n2 + n3) / 3f;

                    noiseSum += n * layerWeight;
                    frequency *= frequencyMultiplier;
                    layerWeight *= weightDecay;
                }

                float noise01 = maxWeight > 0f ? noiseSum / maxWeight : 0f;
                float baseLight = Mathf.Lerp(minLight, maxLight, Mathf.Clamp01(noise01));
                float lightValue = baseLight * latitudeMultiplier;
                envirData[x, y].Light = Mathf.RoundToInt(Mathf.Clamp(lightValue, minLight, maxLight));
            }
        });

        zIndex++;
        if (zIndex >= 1000000)
            zIndex = 0;
    }

    private static void EnsureSeeded()
    {
        if (seeded) return;
        int seed = SimulationConfig.WorldSeed;
        baseOffsetX = ((seed * 17 + 31) % 10000) * 0.13f + 13.7f;
        baseOffsetY = ((seed * 53 + 97) % 10000) * 0.11f + 37.9f;
        baseOffsetZ = ((seed * 89 + 23) % 10000) * 0.07f + 71.3f;
        seeded = true;
    }
}
