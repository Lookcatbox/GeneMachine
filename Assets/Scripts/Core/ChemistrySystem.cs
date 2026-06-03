using System;
using System.Collections.Generic;
using System.IO;
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

public class ChemistrySubstanceRuntime
{
    public int Index;
    public string Id;
    public string DisplayName;
    public ChemicalPhase Phase;
    public Color Color;
    public float OverlayMax;
    public float BaselineLand;
    public float BaselineWater;
}

public class ChemicalReactionRuntime
{
    public string Id;
    public string Name;
    public bool Enabled;
    public int Priority;
    public int[] Reactants;
    public string[] ReactantIds;
    public float[] ReactantCoeffs;
    public int[] Products;
    public string[] ProductIds;
    public float[] ProductCoeffs;
    public ChemistryExpression Condition;
    public ChemistryExpression RateExpression;
}

public static class ChemistrySystem
{
    const string ConfigFileName = "chemistry-reactions.json";

    static ChemistrySubstanceRuntime[] substances = new ChemistrySubstanceRuntime[0];
    static ChemistrySubstanceRuntime[] diffusibleSubstances = new ChemistrySubstanceRuntime[0];
    static ChemicalReactionRuntime[] reactions = new ChemicalReactionRuntime[0];
    static Dictionary<string, int> substanceIndexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static HashSet<string> loggedReactionRuntimeErrors = new HashSet<string>();
    static readonly object reactionRuntimeErrorLock = new object();
    static bool initialized;
    static long chemicalRevision;

    public static int ChemicalOverlayMask { get; private set; }
    public static int SubstanceCount => substances != null ? substances.Length : 0;
    public static ChemistrySubstanceRuntime[] Substances => substances;

    public static void Init()
    {
        if (initialized)
            return;

        LoadConfig();
        initialized = true;
    }

    public static void ReloadConfig()
    {
        initialized = false;
        LoadConfig();
        initialized = true;
        SetChemicalOverlayMask(0);
        Interlocked.Increment(ref chemicalRevision);
    }

    public static void ResetOverlayMask()
    {
        SetChemicalOverlayMask(0);
    }

    public static void SetChemicalOverlayMask(int mask)
    {
        int maxMask = SubstanceCount >= 31 ? -1 : ((1 << SubstanceCount) - 1);
        ChemicalOverlayMask = mask & maxMask;
        SyncOverlayViewMode();
        CellRenderer.InvalidateChemicalOverlay();
    }

    public static void ToggleChemicalOverlay(int substanceIndex)
    {
        if (substanceIndex < 0 || substanceIndex >= SubstanceCount || substanceIndex >= 31)
            return;

        int bit = 1 << substanceIndex;
        if ((ChemicalOverlayMask & bit) != 0)
            ChemicalOverlayMask &= ~bit;
        else
            ChemicalOverlayMask |= bit;
        SyncOverlayViewMode();
        CellRenderer.InvalidateChemicalOverlay();
    }

    public static bool IsChemicalOverlaySelected(int substanceIndex)
    {
        if (substanceIndex < 0 || substanceIndex >= 31)
            return false;
        return (ChemicalOverlayMask & (1 << substanceIndex)) != 0;
    }

    public static long GetChemicalRevision()
    {
        return Interlocked.Read(ref chemicalRevision);
    }

    public static void MarkChemicalAmountsChanged()
    {
        Interlocked.Increment(ref chemicalRevision);
    }

    public static int GetSubstanceIndex(string id)
    {
        if (string.IsNullOrEmpty(id))
            return -1;
        return substanceIndexById.TryGetValue(id, out int index) ? index : -1;
    }

    public static string GetSubstanceId(int substanceIndex)
    {
        ChemistrySubstanceRuntime substance = GetSubstance(substanceIndex);
        return substance != null ? substance.Id : string.Empty;
    }

    public static string GetSubstanceName(int substanceIndex)
    {
        ChemistrySubstanceRuntime substance = GetSubstance(substanceIndex);
        return substance != null ? substance.DisplayName : "未知物质";
    }

    public static ChemistrySubstanceRuntime[] GetDiffusibleSubstances()
    {
        return diffusibleSubstances ?? new ChemistrySubstanceRuntime[0];
    }

    public static ChemicalPhase GetSubstancePhase(int substanceIndex)
    {
        ChemistrySubstanceRuntime substance = GetSubstance(substanceIndex);
        return substance != null ? substance.Phase : ChemicalPhase.Solid;
    }

    public static Color GetSubstanceColor(int substanceIndex)
    {
        ChemistrySubstanceRuntime substance = GetSubstance(substanceIndex);
        return substance != null ? substance.Color : Color.white;
    }

    public static float NormalizeOverlayAmount(int substanceIndex, float amount)
    {
        ChemistrySubstanceRuntime substance = GetSubstance(substanceIndex);
        if (substance == null || substance.OverlayMax <= 0f)
            return 0f;
        return Mathf.Clamp01(amount / substance.OverlayMax);
    }

    public static string GetSubstanceName(ChemicalSubstance substance) => GetSubstanceName((int)substance);
    public static ChemicalPhase GetSubstancePhase(ChemicalSubstance substance) => GetSubstancePhase((int)substance);
    public static Color GetSubstanceColor(ChemicalSubstance substance) => GetSubstanceColor((int)substance);
    public static float NormalizeOverlayAmount(ChemicalSubstance substance, float amount) => NormalizeOverlayAmount((int)substance, amount);
    public static void ToggleChemicalOverlay(ChemicalSubstance substance) => ToggleChemicalOverlay((int)substance);
    public static bool IsChemicalOverlaySelected(ChemicalSubstance substance) => IsChemicalOverlaySelected((int)substance);

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

        env.EnsureChemicalCapacity(SubstanceCount);
        bool isWater = env.Topography == 0 || env.Topography == 3 || env.Topography == 4;
        for (int i = 0; i < SubstanceCount; i++)
        {
            ChemistrySubstanceRuntime substance = substances[i];
            env.SetChemicalAmount(i, isWater ? substance.BaselineWater : substance.BaselineLand);
        }
    }

    public static void ApplyEnvironmentReactions(Envir[,] envirData)
    {
        if (!initialized || envirData == null || reactions == null || reactions.Length == 0)
            return;

        int size = SimulationConfig.EnvirSize;
        int substanceCount = SubstanceCount;
        Parallel.For(1, size + 1, y =>
        {
            for (int x = 1; x <= size; x++)
            {
                Envir env = envirData[x, y];
                if (env == null)
                    continue;
                env.EnsureChemicalCapacity(substanceCount);
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
            ChemicalReactionRuntime reaction = reactions[i];
            if (!reaction.Enabled)
                continue;

            try
            {
                float limiting = GetLimitingAmount(env, reaction);
                if (limiting <= 0f)
                    continue;

                ChemistryExpressionContext context = BuildExpressionContext(env, reaction, tempC, light, limiting);
                if (reaction.Condition != null && reaction.Condition.Evaluate(context) == 0f)
                    continue;

                float delta = reaction.RateExpression != null ? reaction.RateExpression.Evaluate(context) : 0f;
                if (delta <= 0f)
                    continue;

                ApplyReactionDelta(env, reaction, delta);
            }
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogReactionRuntimeErrorOnce(reaction, ex);
            }
        }
    }

    static void LogReactionRuntimeErrorOnce(ChemicalReactionRuntime reaction, Exception ex)
    {
        string id = reaction != null ? reaction.Id : "<unknown>";
        lock (reactionRuntimeErrorLock)
        {
            if (!loggedReactionRuntimeErrors.Add(id))
                return;
        }

        Debug.LogError("化学反应运行失败，已跳过该反应以保持模拟继续: " + id + " - " + ex.Message);
    }

    static float GetLimitingAmount(Envir env, ChemicalReactionRuntime reaction)
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

        if (limiting == float.MaxValue)
            return 0f;
        return limiting;
    }

    static ChemistryExpressionContext BuildExpressionContext(Envir env, ChemicalReactionRuntime reaction, float tempC, int light, float limiting)
    {
        return new ChemistryExpressionContext
        {
            tempC = tempC,
            light = light,
            height = env.Height,
            topography = env.Topography,
            limiting = limiting,
            env = env,
            reaction = reaction
        };
    }

    public static float GetTermCoeff(ChemicalReactionRuntime reaction, string id, bool reactant)
    {
        if (reaction == null)
            return 0f;
        return reactant
            ? GetCoeff(id, reaction.ReactantIds, reaction.ReactantCoeffs)
            : GetCoeff(id, reaction.ProductIds, reaction.ProductCoeffs);
    }

    static float GetCoeff(string id, string[] ids, float[] coeffs)
    {
        if (ids == null || coeffs == null)
            return 0f;
        for (int i = 0; i < ids.Length && i < coeffs.Length; i++)
        {
            if (string.Equals(ids[i], id, StringComparison.OrdinalIgnoreCase))
                return coeffs[i];
        }
        return 0f;
    }

    static void ApplyReactionDelta(Envir env, ChemicalReactionRuntime reaction, float delta)
    {
        for (int i = 0; i < reaction.Reactants.Length; i++)
            env.AddChemicalAmount(reaction.Reactants[i], -reaction.ReactantCoeffs[i] * delta);

        for (int i = 0; i < reaction.Products.Length; i++)
            env.AddChemicalAmount(reaction.Products[i], reaction.ProductCoeffs[i] * delta);
    }

    static void LoadConfig()
    {
        string path = GetConfigPath();
        ChemistryConfigFile config = null;
        if (File.Exists(path))
        {
            try
            {
                config = JsonUtility.FromJson<ChemistryConfigFile>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogError("化学配置读取失败，使用内置默认配置: " + ex.Message);
            }
        }

        if (!TryApplyConfig(config, out string error))
        {
            Debug.LogError("化学配置无效，使用内置默认配置: " + error);
            TryApplyConfig(BuildDefaultConfig(), out _);
        }
    }

    static bool TryApplyConfig(ChemistryConfigFile config, out string error)
    {
        error = string.Empty;
        if (config == null)
        {
            error = "配置为空";
            return false;
        }
        if (config.substances == null || config.substances.Length == 0)
        {
            error = "至少需要 1 个物质";
            return false;
        }

        Dictionary<string, int> nextIndexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<ChemistrySubstanceRuntime> nextSubstances = new List<ChemistrySubstanceRuntime>();
        for (int i = 0; i < config.substances.Length; i++)
        {
            ChemistrySubstanceConfig item = config.substances[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
            {
                error = "物质 id 不能为空";
                return false;
            }
            if (nextIndexById.ContainsKey(item.id))
            {
                error = "物质 id 重复: " + item.id;
                return false;
            }

            ChemistrySubstanceRuntime substance = new ChemistrySubstanceRuntime
            {
                Index = i,
                Id = item.id,
                DisplayName = string.IsNullOrWhiteSpace(item.displayName) ? item.id : item.displayName,
                Phase = ParsePhase(item.phase),
                Color = ParseColor(item.color, Color.white),
                OverlayMax = item.overlayMax > 0f ? item.overlayMax : 1f,
                BaselineLand = Mathf.Max(0f, item.baselineLand),
                BaselineWater = Mathf.Max(0f, item.baselineWater)
            };
            nextIndexById[item.id] = i;
            nextSubstances.Add(substance);
        }

        List<ChemicalReactionRuntime> nextReactions = new List<ChemicalReactionRuntime>();
        ChemistryReactionConfig[] configReactions = config.reactions ?? new ChemistryReactionConfig[0];
        for (int i = 0; i < configReactions.Length; i++)
        {
            ChemistryReactionConfig source = configReactions[i];
            if (source == null)
                continue;

            if (!TryBuildReaction(source, nextIndexById, out ChemicalReactionRuntime reaction, out error))
                return false;
            reaction.Priority = source.priority != 0 ? source.priority : configReactions.Length - i;
            nextReactions.Add(reaction);
        }

        nextReactions.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        substances = nextSubstances.ToArray();
        diffusibleSubstances = BuildDiffusibleSubstanceList(substances);
        reactions = nextReactions.ToArray();
        substanceIndexById = nextIndexById;
        lock (reactionRuntimeErrorLock)
            loggedReactionRuntimeErrors.Clear();
        return true;
    }

    static ChemistrySubstanceRuntime[] BuildDiffusibleSubstanceList(ChemistrySubstanceRuntime[] source)
    {
        if (source == null || source.Length == 0)
            return new ChemistrySubstanceRuntime[0];

        List<ChemistrySubstanceRuntime> result = new List<ChemistrySubstanceRuntime>();
        for (int i = 0; i < source.Length; i++)
        {
            ChemistrySubstanceRuntime substance = source[i];
            if (substance != null && (substance.Phase == ChemicalPhase.Gas || substance.Phase == ChemicalPhase.Liquid))
                result.Add(substance);
        }
        return result.ToArray();
    }

    static bool TryBuildReaction(ChemistryReactionConfig source, Dictionary<string, int> indexById, out ChemicalReactionRuntime reaction, out string error)
    {
        reaction = null;
        error = string.Empty;

        if (source.reactants == null || source.reactants.Length == 0 || source.products == null || source.products.Length == 0)
        {
            error = "反应必须同时包含反应物和生成物: " + source.id;
            return false;
        }

        if (!TryBuildTerms(source.reactants, indexById, out int[] reactants, out string[] reactantIds, out float[] reactantCoeffs, out error))
            return false;
        if (!TryBuildTerms(source.products, indexById, out int[] products, out string[] productIds, out float[] productCoeffs, out error))
            return false;

        try
        {
            reaction = new ChemicalReactionRuntime
            {
                Id = string.IsNullOrWhiteSpace(source.id) ? source.name : source.id,
                Name = string.IsNullOrWhiteSpace(source.name) ? source.id : source.name,
                Enabled = source.enabled,
                Reactants = reactants,
                ReactantIds = reactantIds,
                ReactantCoeffs = reactantCoeffs,
                Products = products,
                ProductIds = productIds,
                ProductCoeffs = productCoeffs,
                Condition = ChemistryExpression.Compile(source.condition),
                RateExpression = ChemistryExpression.Compile(source.rateExpression)
            };
            return true;
        }
        catch (Exception ex)
        {
            error = "反应表达式错误 " + source.id + ": " + ex.Message;
            return false;
        }
    }

    static bool TryBuildTerms(ChemistryReactionTermConfig[] terms, Dictionary<string, int> indexById, out int[] indices, out string[] ids, out float[] coeffs, out string error)
    {
        error = string.Empty;
        indices = new int[terms.Length];
        ids = new string[terms.Length];
        coeffs = new float[terms.Length];

        for (int i = 0; i < terms.Length; i++)
        {
            ChemistryReactionTermConfig term = terms[i];
            if (term == null || string.IsNullOrWhiteSpace(term.substanceId))
            {
                error = "反应项物质 id 不能为空";
                return false;
            }
            if (!indexById.TryGetValue(term.substanceId, out int index))
            {
                error = "反应引用了不存在的物质: " + term.substanceId;
                return false;
            }
            if (term.coeff <= 0f)
            {
                error = "反应系数必须大于 0: " + term.substanceId;
                return false;
            }

            indices[i] = index;
            ids[i] = term.substanceId;
            coeffs[i] = term.coeff;
        }
        return true;
    }

    static ChemistrySubstanceRuntime GetSubstance(int index)
    {
        if (substances == null || index < 0 || index >= substances.Length)
            return null;
        return substances[index];
    }

    static ChemicalPhase ParsePhase(string phase)
    {
        if (string.Equals(phase, "Liquid", StringComparison.OrdinalIgnoreCase)) return ChemicalPhase.Liquid;
        if (string.Equals(phase, "Gas", StringComparison.OrdinalIgnoreCase)) return ChemicalPhase.Gas;
        return ChemicalPhase.Solid;
    }

    static Color ParseColor(string hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;
        if (!hex.StartsWith("#"))
            hex = "#" + hex;
        return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : fallback;
    }

    static string GetConfigPath()
    {
        return Path.Combine(Application.streamingAssetsPath, ConfigFileName);
    }

    static ChemistryConfigFile BuildDefaultConfig()
    {
        return new ChemistryConfigFile
        {
            version = 1,
            substances = new[]
            {
                new ChemistrySubstanceConfig { id = "organic", displayName = "有机物", phase = "Solid", color = "#5A8C38", overlayMax = 8f, baselineLand = SimulationConfig.ChemBaselineOrganicLand, baselineWater = SimulationConfig.ChemBaselineOrganicWater },
                new ChemistrySubstanceConfig { id = "co2", displayName = "CO2", phase = "Gas", color = "#8C8C8C", overlayMax = 6f, baselineLand = SimulationConfig.ChemBaselineCO2Land, baselineWater = SimulationConfig.ChemBaselineCO2Water },
                new ChemistrySubstanceConfig { id = "h2", displayName = "H2", phase = "Gas", color = "#33BFDD", overlayMax = 4f, baselineLand = SimulationConfig.ChemBaselineH2Land, baselineWater = SimulationConfig.ChemBaselineH2Water },
                new ChemistrySubstanceConfig { id = "h2s", displayName = "H2S", phase = "Gas", color = "#E6CC26", overlayMax = 4f, baselineLand = SimulationConfig.ChemBaselineH2SLand, baselineWater = SimulationConfig.ChemBaselineH2SWater },
                new ChemistrySubstanceConfig { id = "sulfate", displayName = "硫酸盐", phase = "Liquid", color = "#C0C7F2", overlayMax = 5f, baselineLand = SimulationConfig.ChemBaselineSulfateLand, baselineWater = SimulationConfig.ChemBaselineSulfateWater }
            },
            reactions = new[]
            {
                Reaction("co2-h2-organic", "原始氢能合成", 5, new[] { Term("co2", 1f), Term("h2", 4f) }, new[] { Term("organic", 1f) }, "tempC >= 5 && tempC <= 65 && light >= 10", "0.05 * pow(limiting, 0.75)"),
                Reaction("h2s-co2-organic", "硫化耦合", 4, new[] { Term("h2s", 1f), Term("co2", 1f) }, new[] { Term("organic", 1f) }, "tempC >= 5 && tempC <= 70 && light >= 15", "0.05 * pow(limiting, 0.75)"),
                Reaction("organic-decompose", "有机物厌氧分解", 3, new[] { Term("organic", 1f) }, new[] { Term("co2", 1f), Term("h2", 2f) }, "tempC >= 15 && tempC <= 90", "0.05 * pow(limiting, 0.75)"),
                Reaction("sulfate-organic-h2s", "硫酸盐还原", 2, new[] { Term("sulfate", 1f), Term("organic", 1f) }, new[] { Term("h2s", 1f), Term("co2", 1f) }, "tempC >= 5 && tempC <= 80", "0.05 * pow(limiting, 0.75)"),
                Reaction("h2s-photolysis", "H2S 光致裂解", 1, new[] { Term("h2s", 2f) }, new[] { Term("h2", 2f) }, "tempC >= 10 && tempC <= 75 && light >= 20", "0.05 * pow(limiting, 0.75)")
            }
        };
    }

    static ChemistryReactionConfig Reaction(string id, string name, int priority, ChemistryReactionTermConfig[] reactants, ChemistryReactionTermConfig[] products, string condition, string rateExpression)
    {
        return new ChemistryReactionConfig
        {
            id = id,
            name = name,
            enabled = true,
            priority = priority,
            reactants = reactants,
            products = products,
            condition = condition,
            rateExpression = rateExpression
        };
    }

    static ChemistryReactionTermConfig Term(string id, float coeff)
    {
        return new ChemistryReactionTermConfig { substanceId = id, coeff = coeff };
    }

    static void SyncOverlayViewMode()
    {
        if (ChemicalOverlayMask != 0)
            CellRenderer.currentOverlayViewMode |= CellRenderer.OverlayViewMode.Chemical;
        else
            CellRenderer.currentOverlayViewMode &= ~CellRenderer.OverlayViewMode.Chemical;
    }
}
