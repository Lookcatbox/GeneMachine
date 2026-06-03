using System.Threading.Tasks;
using UnityEngine;

public static class ChemicalDiffusion
{
    private static float[][,] amountBuffers = new float[0][,];
    private static float[][,] diffusionBuffers = new float[0][,];
    private static int bufferSize;
    private static int bufferSubstanceCount;

    public static void PrepareBuffers(int size, int substanceCount)
    {
        if (size <= 0)
            return;

        if (amountBuffers != null && diffusionBuffers != null &&
            bufferSize == size && bufferSubstanceCount >= substanceCount)
            return;

        bufferSize = size;
        bufferSubstanceCount = substanceCount;
        amountBuffers = new float[substanceCount][,];
        diffusionBuffers = new float[substanceCount][,];
        for (int i = 0; i < substanceCount; i++)
        {
            amountBuffers[i] = new float[size + 2, size + 2];
            diffusionBuffers[i] = new float[size + 2, size + 2];
        }
    }

    public static void Update(Envir[,] envirData, ChemistrySubstanceRuntime substance)
    {
        if (envirData == null || substance == null)
            return;

        int size = SimulationConfig.EnvirSize;
        int substanceIndex = substance.Index;
        if (substanceIndex < 0 || substanceIndex >= amountBuffers.Length)
            return;

        float strength = GetDiffusionStrength(substance.Phase);
        if (strength <= 0f)
            return;

        bool waterOnly = substance.Phase == ChemicalPhase.Liquid;
        float[,] amountBuffer = amountBuffers[substanceIndex];
        float[,] diffusionBuffer = diffusionBuffers[substanceIndex];

        Parallel.For(1, size + 1, y =>
        {
            for (int x = 1; x <= size; x++)
            {
                Envir env = envirData[x, y];
                if (env == null || (waterOnly && !IsWater(env.Topography)))
                    amountBuffer[x, y] = 0f;
                else
                    amountBuffer[x, y] = env.GetChemicalAmount(substanceIndex);
            }
        });

        Parallel.For(1, size + 1, y =>
        {
            int yDown = y == 1 ? size : y - 1;
            int yUp = y == size ? 1 : y + 1;

            for (int x = 1; x <= size; x++)
            {
                Envir centerEnv = envirData[x, y];
                if (centerEnv == null)
                    continue;

                if (waterOnly && !IsWater(centerEnv.Topography))
                {
                    diffusionBuffer[x, y] = amountBuffer[x, y];
                    continue;
                }

                int xLeft = x == 1 ? size : x - 1;
                int xRight = x == size ? 1 : x + 1;
                float center = amountBuffer[x, y];
                float sum = 0f;
                int count = 0;

                AddNeighbor(envirData, amountBuffer, xLeft, y, waterOnly, ref sum, ref count);
                AddNeighbor(envirData, amountBuffer, xRight, y, waterOnly, ref sum, ref count);
                AddNeighbor(envirData, amountBuffer, x, yDown, waterOnly, ref sum, ref count);
                AddNeighbor(envirData, amountBuffer, x, yUp, waterOnly, ref sum, ref count);
                AddNeighbor(envirData, amountBuffer, xLeft, yDown, waterOnly, ref sum, ref count);
                AddNeighbor(envirData, amountBuffer, xLeft, yUp, waterOnly, ref sum, ref count);
                AddNeighbor(envirData, amountBuffer, xRight, yDown, waterOnly, ref sum, ref count);
                AddNeighbor(envirData, amountBuffer, xRight, yUp, waterOnly, ref sum, ref count);

                if (count == 0)
                {
                    diffusionBuffer[x, y] = center;
                    continue;
                }

                float average = sum / count;
                diffusionBuffer[x, y] = Mathf.Max(0f, center + strength * (average - center));
            }
        });

        Parallel.For(1, size + 1, y =>
        {
            for (int x = 1; x <= size; x++)
            {
                Envir env = envirData[x, y];
                if (env == null)
                    continue;
                if (waterOnly && !IsWater(env.Topography))
                    continue;
                env.SetChemicalAmount(substanceIndex, diffusionBuffer[x, y]);
            }
        });
    }

    private static void AddNeighbor(Envir[,] envirData, float[,] amountBuffer, int x, int y, bool waterOnly, ref float sum, ref int count)
    {
        Envir env = envirData[x, y];
        if (env == null || (waterOnly && !IsWater(env.Topography)))
            return;

        sum += amountBuffer[x, y];
        count++;
    }

    private static float GetDiffusionStrength(ChemicalPhase phase)
    {
        switch (phase)
        {
            case ChemicalPhase.Gas:
                return Mathf.Clamp01(SimulationConfig.ChemicalGasDiffusionStrength);
            case ChemicalPhase.Liquid:
                return Mathf.Clamp01(SimulationConfig.ChemicalLiquidDiffusionStrength);
            default:
                return 0f;
        }
    }

    private static bool IsWater(int topography)
    {
        return topography == 0 || topography == 3 || topography == 4;
    }
}
