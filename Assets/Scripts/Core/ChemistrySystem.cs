using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public enum ChemicalSubstance
{
    Organic = 0,
    CO2 = 1,
    H2 = 2,
    H2S = 3,
    Sulfate = 4,
    Count = 5
}

public enum ChemicalPhase
{
    Solid = 0,
    Liquid = 1,
    Gas = 2
}

struct ChemicalReaction
{
    public int Priority;
    public ChemicalSubstance[] Reactants;
    public float[] ReactantCoeffs;
    public ChemicalSubstance[] Products;
    public float[] ProductCoeffs;
    public float BaseRate;
    public float MinTempC;
    public float MaxTempC;
    public int MinLight;
}

public static class ChemistrySystem
{
    public static readonly ChemicalSubstance[] DefaultOrder =
    {
        ChemicalSubstance.Organic,
        ChemicalSubstance.CO2,
        ChemicalSubstance.H2,
        ChemicalSubstance.H2S,
        ChemicalSubstance.Sulfate
    };

    static ChemicalReaction[] reactions;
    static bool initialized;
    static long chemicalRevision;

    public static int ChemicalOverlayMask { get; private set; }

    public static void Init()
    {
        if (initialized)
            return;

        reactions = BuildReactions();
        Array.Sort(reactions, (a, b) => b.Priority.CompareTo(a.Priority));
        initialized = true;
    }

    public static void ResetOverlayMask()
    {
        SetChemicalOverlayMask(0);
    }

    public static void SetChemicalOverlayMask(int mask)
    {
        ChemicalOverlayMask = mask & ((1 << (int)ChemicalSubstance.Count) - 1);
        SyncOverlayViewMode();
        CellRenderer.InvalidateChemicalOverlay();
    }

    public static void ToggleChemicalOverlay(ChemicalSubstance substance)
    {
        int bit = 1 << (int)substance;
        if ((ChemicalOverlayMask & bit) != 0)
            ChemicalOverlayMask &= ~bit;
        else
            ChemicalOverlayMask |= bit;
        SyncOverlayViewMode();
        CellRenderer.InvalidateChemicalOverlay();
    }

    public static bool IsChemicalOverlaySelected(ChemicalSubstance substance)
    {
        return (ChemicalOverlayMask & (1 << (int)substance)) != 0;
    }

    public static long GetChemicalRevision()
    {
        return Interlocked.Read(ref chemicalRevision);
    }

    public static string GetSubstanceName(ChemicalSubstance substance)
    {
        switch (substance)
        {
            case ChemicalSubstance.Organic: return "有机物";
            case ChemicalSubstance.CO2: return "CO2";
            case ChemicalSubstance.H2: return "H2";
            case ChemicalSubstance.H2S: return "H2S";
            case ChemicalSubstance.Sulfate: return "硫酸盐";
            default: return substance.ToString();
        }
    }

    public static ChemicalPhase GetSubstancePhase(ChemicalSubstance substance)
    {
        switch (substance)
        {
            case ChemicalSubstance.Organic:
            case ChemicalSubstance.Sulfate:
                return ChemicalPhase.Solid;
            case ChemicalSubstance.CO2:
            case ChemicalSubstance.H2:
            case ChemicalSubstance.H2S:
                return ChemicalPhase.Gas;
            default:
                return ChemicalPhase.Solid;
        }
    }

    public static Color GetSubstanceColor(ChemicalSubstance substance)
    {
        switch (substance)
        {
            case ChemicalSubstance.Organic: return SimulationConfig.ChemColorOrganic;
            case ChemicalSubstance.CO2: return SimulationConfig.ChemColorCO2;
            case ChemicalSubstance.H2: return SimulationConfig.ChemColorH2;
            case ChemicalSubstance.H2S: return SimulationConfig.ChemColorH2S;
            case ChemicalSubstance.Sulfate: return SimulationConfig.ChemColorSulfate;
            default: return Color.white;
        }
    }

    public static float NormalizeOverlayAmount(ChemicalSubstance substance, float amount)
    {
        float max = GetOverlayMax(substance);
        if (max <= 0f)
            return 0f;
        return Mathf.Clamp01(amount / max);
    }

    public static void SeedEnvironmentBaselines(Envir[,] envirData)
    {
        if (envirData == null)
            return;

        int size = SimulationConfig.EnvirSize;
        for (int y = 1; y <= size; y++)
        {
            for (int x = 1; x <= size; x++)
            {
                Envir env = envirData[x, y];
                if (env != null)
                    ApplyBaselineForTopography(env);
            }
        }
    }

    public static void ApplyBaselineForTopography(Envir env)
    {
        if (env == null)
            return;

        bool isWater = env.Topography == 0 || env.Topography == 3 || env.Topography == 4;
        env.SetChemicalAmount(ChemicalSubstance.Organic,
            isWater ? SimulationConfig.ChemBaselineOrganicWater : SimulationConfig.ChemBaselineOrganicLand);
        env.SetChemicalAmount(ChemicalSubstance.CO2,
            isWater ? SimulationConfig.ChemBaselineCO2Water : SimulationConfig.ChemBaselineCO2Land);
        env.SetChemicalAmount(ChemicalSubstance.H2,
            isWater ? SimulationConfig.ChemBaselineH2Water : SimulationConfig.ChemBaselineH2Land);
        env.SetChemicalAmount(ChemicalSubstance.H2S,
            isWater ? SimulationConfig.ChemBaselineH2SWater : SimulationConfig.ChemBaselineH2SLand);
        env.SetChemicalAmount(ChemicalSubstance.Sulfate,
            isWater ? SimulationConfig.ChemBaselineSulfateWater : SimulationConfig.ChemBaselineSulfateLand);
    }

    public static void ApplyEnvironmentReactions(Envir[,] envirData)
    {
        if (!initialized || envirData == null || reactions == null || reactions.Length == 0)
            return;

        int size = SimulationConfig.EnvirSize;
        Parallel.For(1, size + 1, y =>
        {
            for (int x = 1; x <= size; x++)
            {
                Envir env = envirData[x, y];
                if (env == null)
                    continue;
                ApplyReactionsToCell(env);
            }
        });
        Interlocked.Increment(ref chemicalRevision);
    }

    static void ApplyReactionsToCell(Envir env)
    {
        float tempC = SimulationCore.KelvinToCelsius(env.Temp);
        int light = env.Light;

        for (int i = 0; i < reactions.Length; i++)
        {
            ref ChemicalReaction reaction = ref reactions[i];
            if (tempC < reaction.MinTempC || tempC > reaction.MaxTempC)
                continue;
            if (light < reaction.MinLight)
                continue;

            float delta = EvaluateKinetics(env, ref reaction);
            if (delta <= 0f)
                continue;

            ApplyReactionDelta(env, ref reaction, delta);
        }
    }

    static float EvaluateKinetics(Envir env, ref ChemicalReaction reaction)
    {
        float limiting = float.MaxValue;
        for (int i = 0; i < reaction.Reactants.Length; i++)
        {
            float coeff = reaction.ReactantCoeffs[i];
            if (coeff <= 0f)
                continue;

            float available = env.GetChemicalAmount(reaction.Reactants[i]) / coeff;
            if (available < limiting)
                limiting = available;
        }

        if (limiting <= 0f || limiting == float.MaxValue)
            return 0f;

        float power = SimulationConfig.ChemKineticsPower;
        return reaction.BaseRate * Mathf.Pow(limiting, power);
    }

    static void ApplyReactionDelta(Envir env, ref ChemicalReaction reaction, float delta)
    {
        for (int i = 0; i < reaction.Reactants.Length; i++)
            env.AddChemicalAmount(reaction.Reactants[i], -reaction.ReactantCoeffs[i] * delta);

        for (int i = 0; i < reaction.Products.Length; i++)
            env.AddChemicalAmount(reaction.Products[i], reaction.ProductCoeffs[i] * delta);
    }

    static ChemicalReaction[] BuildReactions()
    {
        return new[]
        {
            // R1: CO2 + 4H2 -> 有机物（原始氢能合成，需光）
            new ChemicalReaction
            {
                Priority = 5,
                Reactants = new[] { ChemicalSubstance.CO2, ChemicalSubstance.H2 },
                ReactantCoeffs = new[] { 1f, 4f },
                Products = new[] { ChemicalSubstance.Organic },
                ProductCoeffs = new[] { 1f },
                BaseRate = SimulationConfig.ChemRateR1,
                MinTempC = SimulationConfig.ChemTempMinR1,
                MaxTempC = SimulationConfig.ChemTempMaxR1,
                MinLight = SimulationConfig.ChemLightMinR1
            },
            // R2: H2S + CO2 -> 有机物（硫化耦合，需较强光）
            new ChemicalReaction
            {
                Priority = 4,
                Reactants = new[] { ChemicalSubstance.H2S, ChemicalSubstance.CO2 },
                ReactantCoeffs = new[] { 1f, 1f },
                Products = new[] { ChemicalSubstance.Organic },
                ProductCoeffs = new[] { 1f },
                BaseRate = SimulationConfig.ChemRateR2,
                MinTempC = SimulationConfig.ChemTempMinR2,
                MaxTempC = SimulationConfig.ChemTempMaxR2,
                MinLight = SimulationConfig.ChemLightMinR2
            },
            // R3: 有机物 -> CO2 + 2H2（厌氧分解，无光）
            new ChemicalReaction
            {
                Priority = 3,
                Reactants = new[] { ChemicalSubstance.Organic },
                ReactantCoeffs = new[] { 1f },
                Products = new[] { ChemicalSubstance.CO2, ChemicalSubstance.H2 },
                ProductCoeffs = new[] { 1f, 2f },
                BaseRate = SimulationConfig.ChemRateR3,
                MinTempC = SimulationConfig.ChemTempMinR3,
                MaxTempC = SimulationConfig.ChemTempMaxR3,
                MinLight = SimulationConfig.ChemLightMinR3
            },
            // R4: 硫酸盐 + 有机物 -> H2S + CO2（硫酸盐还原）
            new ChemicalReaction
            {
                Priority = 2,
                Reactants = new[] { ChemicalSubstance.Sulfate, ChemicalSubstance.Organic },
                ReactantCoeffs = new[] { 1f, 1f },
                Products = new[] { ChemicalSubstance.H2S, ChemicalSubstance.CO2 },
                ProductCoeffs = new[] { 1f, 1f },
                BaseRate = SimulationConfig.ChemRateR4,
                MinTempC = SimulationConfig.ChemTempMinR4,
                MaxTempC = SimulationConfig.ChemTempMaxR4,
                MinLight = SimulationConfig.ChemLightMinR4
            },
            // R5: 2H2S -> 2H2（光致裂解，固硫物为隐式产物）
            new ChemicalReaction
            {
                Priority = 1,
                Reactants = new[] { ChemicalSubstance.H2S },
                ReactantCoeffs = new[] { 2f },
                Products = new[] { ChemicalSubstance.H2 },
                ProductCoeffs = new[] { 2f },
                BaseRate = SimulationConfig.ChemRateR5,
                MinTempC = SimulationConfig.ChemTempMinR5,
                MaxTempC = SimulationConfig.ChemTempMaxR5,
                MinLight = SimulationConfig.ChemLightMinR5
            }
        };
    }

    static float GetOverlayMax(ChemicalSubstance substance)
    {
        switch (substance)
        {
            case ChemicalSubstance.Organic: return SimulationConfig.ChemOverlayMaxOrganic;
            case ChemicalSubstance.CO2: return SimulationConfig.ChemOverlayMaxCO2;
            case ChemicalSubstance.H2: return SimulationConfig.ChemOverlayMaxH2;
            case ChemicalSubstance.H2S: return SimulationConfig.ChemOverlayMaxH2S;
            case ChemicalSubstance.Sulfate: return SimulationConfig.ChemOverlayMaxSulfate;
            default: return 1f;
        }
    }

    static void SyncOverlayViewMode()
    {
        if (ChemicalOverlayMask != 0)
            CellRenderer.currentOverlayViewMode |= CellRenderer.OverlayViewMode.Chemical;
        else
            CellRenderer.currentOverlayViewMode &= ~CellRenderer.OverlayViewMode.Chemical;
    }
}
