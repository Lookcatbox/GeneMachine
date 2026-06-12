// ChemicalDiffusion.cs - 单种化学物质的环境格扩散（气体全图 / 液体仅水域）
using System.Threading.Tasks;
using UnityEngine;

/// <summary>化学物质扩散：每物质三遍全图扫描（采集→邻域平均→写回）。</summary>
public static class ChemicalDiffusion
{
    private static float[][,] amountBuffers = new float[0][,];
    private static float[][,] diffusionBuffers = new float[0][,];
    private static float[][] flatDiffusionBuffers = new float[0][];
    private static int bufferSize;
    private static int bufferSubstanceCount;

    /// <summary>按地图尺寸与物质数分配/复用 float 缓冲（避免每步 GC）。</summary>
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
        flatDiffusionBuffers = new float[substanceCount][];
        int flatLength = (size + 2) * (size + 2);
        for (int i = 0; i < substanceCount; i++)
        {
            amountBuffers[i] = new float[size + 2, size + 2];
            diffusionBuffers[i] = new float[size + 2, size + 2];
            flatDiffusionBuffers[i] = new float[flatLength];
        }
    }

    public static void Update(Envir[,] envirData, ChemistrySubstanceRuntime substance)
    {
        if (envirData == null || substance == null)
            return;

        // 单物质 3 遍全图：采集 → 8 邻域扩散 → 写回 ChemAmounts。
        // 液体仅在水域格参与邻域平均，陆地格跳过写回（边界相当于不扩散）。
        int size = SimulationConfig.EnvirSize;
        int substanceIndex = substance.Index;
        if (substanceIndex < 0 || substanceIndex >= amountBuffers.Length)
            return;

        float strength = GetDiffusionStrength(substance.Phase);
        if (strength <= 0f)
            return;

        if (ChemistryField.IsAllocatedFor(size, ChemistrySystem.SubstanceCount))
        {
            UpdateSoA(envirData, substance, strength);
            return;
        }

        bool waterOnly = substance.Phase == ChemicalPhase.Liquid;
        float[,] amountBuffer = amountBuffers[substanceIndex];
        float[,] diffusionBuffer = diffusionBuffers[substanceIndex];

        Parallel.For(1, size + 1, SimulationParallel.SimulationOptions, y =>
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

        // 扩散核：center + strength × (neighborAvg - center)；strength 越小收敛越慢
        Parallel.For(1, size + 1, SimulationParallel.SimulationOptions, y =>
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
                    if (ChemicalStepAudit.IsAuditCell(x, y))
                        ChemicalStepAudit.RecordDiffusionSnapshot(x, y, substanceIndex, amountBuffer[x, y], amountBuffer[x, y], 0);
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
                    if (ChemicalStepAudit.IsAuditCell(x, y))
                        ChemicalStepAudit.RecordDiffusionSnapshot(x, y, substanceIndex, center, center, 0);
                    continue;
                }

                float average = sum / count;
                if (ChemicalStepAudit.IsAuditCell(x, y))
                    ChemicalStepAudit.RecordDiffusionSnapshot(x, y, substanceIndex, center, average, count);
                diffusionBuffer[x, y] = Mathf.Max(0f, center + strength * (average - center));
            }
        });

        Parallel.For(1, size + 1, SimulationParallel.SimulationOptions, y =>
        {
            for (int x = 1; x <= size; x++)
            {
                Envir env = envirData[x, y];
                if (env == null)
                    continue;
                if (waterOnly && !IsWater(env.Topography))
                    continue;
                float newAmount = diffusionBuffer[x, y];
                if (ChemicalStepAudit.IsAuditCell(x, y))
                {
                    float oldAmount = env.GetChemicalAmount(substanceIndex);
                    ChemicalStepAudit.RecordDiffusionChange(x, y, substanceIndex, oldAmount, newAmount);
                }
                env.SetChemicalAmount(substanceIndex, newAmount);
            }
        });
    }

    static void UpdateSoA(Envir[,] envirData, ChemistrySubstanceRuntime substance, float strength)
    {
        int size = SimulationConfig.EnvirSize;
        int substanceIndex = substance.Index;
        float[] amountBuffer = ChemistryField.GetSubstanceBuffer(substanceIndex);
        if (amountBuffer == null || substanceIndex < 0 || substanceIndex >= flatDiffusionBuffers.Length)
            return;

        bool waterOnly = substance.Phase == ChemicalPhase.Liquid;
        float[] diffusionBuffer = flatDiffusionBuffers[substanceIndex];
        int stride = ChemistryField.Stride;

        // SoA 路径省掉“采集”一遍全图，直接在连续物质数组上做邻域扩散。
        Parallel.For(1, size + 1, SimulationParallel.SimulationOptions, y =>
        {
            int yDown = y == 1 ? size : y - 1;
            int yUp = y == size ? 1 : y + 1;

            for (int x = 1; x <= size; x++)
            {
                Envir centerEnv = envirData[x, y];
                int centerIndex = y * stride + x;
                if (centerEnv == null)
                    continue;

                if (waterOnly && !IsWater(centerEnv.Topography))
                {
                    diffusionBuffer[centerIndex] = amountBuffer[centerIndex];
                    if (ChemicalStepAudit.IsAuditCell(x, y))
                        ChemicalStepAudit.RecordDiffusionSnapshot(x, y, substanceIndex, amountBuffer[centerIndex], amountBuffer[centerIndex], 0);
                    continue;
                }

                int xLeft = x == 1 ? size : x - 1;
                int xRight = x == size ? 1 : x + 1;
                float center = amountBuffer[centerIndex];
                float sum = 0f;
                int count = 0;

                AddNeighborSoA(envirData, amountBuffer, xLeft, y, waterOnly, stride, ref sum, ref count);
                AddNeighborSoA(envirData, amountBuffer, xRight, y, waterOnly, stride, ref sum, ref count);
                AddNeighborSoA(envirData, amountBuffer, x, yDown, waterOnly, stride, ref sum, ref count);
                AddNeighborSoA(envirData, amountBuffer, x, yUp, waterOnly, stride, ref sum, ref count);
                AddNeighborSoA(envirData, amountBuffer, xLeft, yDown, waterOnly, stride, ref sum, ref count);
                AddNeighborSoA(envirData, amountBuffer, xLeft, yUp, waterOnly, stride, ref sum, ref count);
                AddNeighborSoA(envirData, amountBuffer, xRight, yDown, waterOnly, stride, ref sum, ref count);
                AddNeighborSoA(envirData, amountBuffer, xRight, yUp, waterOnly, stride, ref sum, ref count);

                if (count == 0)
                {
                    diffusionBuffer[centerIndex] = center;
                    if (ChemicalStepAudit.IsAuditCell(x, y))
                        ChemicalStepAudit.RecordDiffusionSnapshot(x, y, substanceIndex, center, center, 0);
                    continue;
                }

                float average = sum / count;
                if (ChemicalStepAudit.IsAuditCell(x, y))
                    ChemicalStepAudit.RecordDiffusionSnapshot(x, y, substanceIndex, center, average, count);
                diffusionBuffer[centerIndex] = Mathf.Max(0f, center + strength * (average - center));
            }
        });

        Parallel.For(1, size + 1, SimulationParallel.SimulationOptions, y =>
        {
            int rowBase = y * stride;
            for (int x = 1; x <= size; x++)
            {
                Envir env = envirData[x, y];
                if (env == null)
                    continue;
                if (waterOnly && !IsWater(env.Topography))
                    continue;
                int index = rowBase + x;
                if (ChemicalStepAudit.IsAuditCell(x, y))
                {
                    float before = amountBuffer[index];
                    float after = diffusionBuffer[index];
                    ChemicalStepAudit.RecordDiffusionChange(x, y, substanceIndex, before, after);
                }
                amountBuffer[index] = diffusionBuffer[index];
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

    private static void AddNeighborSoA(Envir[,] envirData, float[] amountBuffer, int x, int y, bool waterOnly, int stride, ref float sum, ref int count)
    {
        Envir env = envirData[x, y];
        if (env == null || (waterOnly && !IsWater(env.Topography)))
            return;

        sum += amountBuffer[y * stride + x];
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
