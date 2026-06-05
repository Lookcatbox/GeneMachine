// ChemicalOverlayRasterizer.cs - 从 SoA 化学场生成化学叠加层像素
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 按当前化学 overlay mask 从 <see cref="ChemistryField"/> 连续数组重建降采样像素。
/// </summary>
public static class ChemicalOverlayRasterizer
{
    static readonly object sync = new object();
    static Color32[] pixels;
    static int texSize;
    static int downsampleFactor;
    static int[] maskedSubstanceIndices = new int[0];
    static byte overlayAlpha;
    static int lastMask = -1;

    public static Color32[] Pixels => pixels;
    public static int TextureSize => texSize;

    public static void Configure(Color32[] sharedPixels, int textureSize, int factor)
    {
        lock (sync)
        {
            pixels = sharedPixels;
            texSize = Mathf.Max(1, textureSize);
            downsampleFactor = Mathf.Max(1, factor);
            overlayAlpha = (byte)Mathf.Clamp(Mathf.RoundToInt(SimulationConfig.OverlayAlpha * 255f), 0, 255);
            lastMask = -1;
        }
    }

    public static void NotifyMaskChanged()
    {
        lock (sync)
        {
            lastMask = -1;
        }
    }

    public static void RequestFullRebuild()
    {
    }

    /// <summary>渲染侧按需调用；不在模拟线程中执行。</summary>
    public static void Rebuild(Envir[,] envirData)
    {
        if (envirData == null || pixels == null || !ChemistryField.IsAllocated || ChemistrySystem.ChemicalOverlayMask == 0)
            return;

        EnsureMaskedSubstances();
        RasterizeAll(envirData);
    }

    static void EnsureMaskedSubstances()
    {
        int mask = ChemistrySystem.ChemicalOverlayMask;
        if (mask == lastMask)
            return;

        ChemistrySubstanceRuntime[] substances = ChemistrySystem.Substances;
        if (substances == null || substances.Length == 0)
        {
            maskedSubstanceIndices = new int[0];
            lastMask = mask;
            return;
        }

        var list = new List<int>(8);
        for (int i = 0; i < substances.Length; i++)
        {
            int index = substances[i].Index;
            if (index < 0 || index >= 31)
                continue;
            if ((mask & (1 << index)) != 0)
                list.Add(index);
        }

        maskedSubstanceIndices = list.ToArray();
        lastMask = mask;
    }

    static void RasterizeAll(Envir[,] envirData)
    {
        int size = SimulationConfig.EnvirSize;
        int factor = downsampleFactor;
        int localTexSize = texSize;
        int[] indices = maskedSubstanceIndices;
        Color32[] buffer = pixels;
        byte alpha = overlayAlpha;

        Parallel.For(0, localTexSize, texY =>
        {
            int envY = 1 + texY * factor + factor / 2;
            if (envY > size) envY = size;
            int rowBase = texY * localTexSize;
            for (int texX = 0; texX < localTexSize; texX++)
            {
                int envX = 1 + texX * factor + factor / 2;
                if (envX > size) envX = size;
                Envir env = envirData[envX, envY];
                int dataIndex = ChemistryField.ToIndex(envX, envY);
                buffer[rowBase + texX] = env != null
                    ? ComputePixel(dataIndex, indices, alpha)
                    : new Color32(0, 0, 0, 0);
            }
        });
    }

    static Color32 ComputePixel(int dataIndex, int[] indices, byte alpha)
    {
        Color rgb = Color.black;
        float weightSum = 0f;
        float maxIntensity = 0f;
        ChemistrySubstanceRuntime[] substances = ChemistrySystem.Substances;

        for (int i = 0; i < indices.Length; i++)
        {
            int substanceIndex = indices[i];
            if (substanceIndex < 0 || substances == null || substanceIndex >= substances.Length)
                continue;

            float[] amountBuffer = ChemistryField.GetSubstanceBuffer(substanceIndex);
            if (amountBuffer == null || dataIndex < 0 || dataIndex >= amountBuffer.Length)
                continue;

            float norm = ChemistrySystem.NormalizeOverlayAmount(substanceIndex, amountBuffer[dataIndex]);
            if (norm <= 0f)
                continue;

            Color substanceColor = substances[substanceIndex].Color;
            rgb += substanceColor * norm;
            weightSum += norm;
            if (norm > maxIntensity)
                maxIntensity = norm;
        }

        if (weightSum <= 0f)
            return new Color32(0, 0, 0, 0);

        rgb /= weightSum;
        byte outAlpha = (byte)Mathf.Clamp(Mathf.RoundToInt(maxIntensity * alpha), 0, 255);
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(rgb.r * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(rgb.g * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(rgb.b * 255f), 0, 255),
            outAlpha);
    }

}
