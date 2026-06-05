// ChemicalStepAudit.cs - 单格化学物质本回合变动审计（反应 / 扩散分项）
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>一条影响某物质存量的变动记录。</summary>
public struct ChemicalChangeEntry
{
    public string Source;
    public float Delta;
}

/// <summary>某一物质在本回合的变动汇总。</summary>
public class ChemicalSubstanceStepReport
{
    public int SubstanceIndex;
    public string SubstanceName;
    public float AmountBefore;
    public float AmountAfter;
    public float TotalDelta;
    public ChemicalChangeEntry[] Entries;
    /// <summary>扩散步评估时本格存量（center）。</summary>
    public bool HasDiffusionSnapshot;
    public float DiffusionCenter;
    /// <summary>参与邻域平均的 8 邻格算术均值。</summary>
    public float DiffusionNeighborAvg;
    /// <summary>邻均 − center；扩散增量 ∝ 此差值。</summary>
    public float DiffusionGradient;
}

/// <summary>某一环境格在本回合的化学变动报告（主线程只读快照）。</summary>
public class ChemicalCellStepReport
{
    public long Step;
    public int X;
    public int Y;
    public bool HasData;
    public ChemicalSubstanceStepReport[] SubstanceReports;
}

/// <summary>
/// 跟踪指定格、指定物质 mask 的每步化学变动；由模拟线程写入，UI 读取 <see cref="GetLastReport"/>。
/// </summary>
public static class ChemicalStepAudit
{
    const int MaxSubstances = 32;
    const float Epsilon = 1e-7f;

    static volatile int targetX;
    static volatile int targetY;
    static volatile int trackMask;
    static volatile bool auditEnabled;

    static long workingStep;
    static bool stepActive;
    static readonly float[] stepStartAmounts = new float[MaxSubstances];
    static readonly float[] diffusionCenter = new float[MaxSubstances];
    static readonly float[] diffusionNeighborAvg = new float[MaxSubstances];
    static readonly bool[] hasDiffusionSnapshot = new bool[MaxSubstances];
    static readonly List<ChemicalChangeEntry>[] workingEntries = new List<ChemicalChangeEntry>[MaxSubstances];

    static readonly object publishLock = new object();
    static ChemicalCellStepReport lastPublished = new ChemicalCellStepReport();

    static ChemicalStepAudit()
    {
        for (int i = 0; i < MaxSubstances; i++)
            workingEntries[i] = new List<ChemicalChangeEntry>(4);
    }

    /// <summary>主线程设置要审计的格与物质 mask（overlay 选中的物质）。</summary>
    public static void SetTarget(int x, int y, int substanceMask)
    {
        targetX = x;
        targetY = y;
        trackMask = substanceMask;
        auditEnabled = x >= 1 && y >= 1 && substanceMask != 0;
    }

    public static int TargetX => targetX;
    public static int TargetY => targetY;
    public static bool IsEnabled => auditEnabled;

    public static bool IsAuditCell(int x, int y)
    {
        return auditEnabled && x == targetX && y == targetY;
    }

    public static ChemicalCellStepReport GetLastReport()
    {
        lock (publishLock)
        {
            return CloneReport(lastPublished);
        }
    }

    public static void BeginChemistryStep(long step)
    {
        if (!auditEnabled || !ChemistryField.IsAllocated)
        {
            stepActive = false;
            return;
        }

        int x = targetX;
        int y = targetY;
        int mask = trackMask;
        if (!SimulationCore.InBounds(x, y) || mask == 0)
        {
            stepActive = false;
            return;
        }

        workingStep = step;
        stepActive = true;

        for (int i = 0; i < MaxSubstances; i++)
        {
            workingEntries[i].Clear();
            hasDiffusionSnapshot[i] = false;
        }

        ChemistrySubstanceRuntime[] substances = ChemistrySystem.Substances;
        int count = substances != null ? substances.Length : 0;
        for (int s = 0; s < count && s < MaxSubstances; s++)
        {
            int bit = 1 << s;
            if ((mask & bit) == 0)
            {
                stepStartAmounts[s] = 0f;
                continue;
            }
            stepStartAmounts[s] = ChemistryField.GetAmount(x, y, s);
        }
    }

    public static void RecordReactionChange(int x, int y, ChemicalReactionRuntime reaction, float reactionDelta)
    {
        if (!stepActive || !IsAuditCell(x, y) || reaction == null || reactionDelta <= 0f)
            return;

        string source = string.IsNullOrEmpty(reaction.Name)
            ? "化学反应"
            : string.Format("化学反应：{0}", reaction.Name);

        if (reaction.Reactants != null && reaction.ReactantCoeffs != null)
        {
            for (int i = 0; i < reaction.Reactants.Length && i < reaction.ReactantCoeffs.Length; i++)
            {
                float coeff = reaction.ReactantCoeffs[i];
                if (coeff <= 0f)
                    continue;
                AddEntry(reaction.Reactants[i], source, -coeff * reactionDelta);
            }
        }

        if (reaction.Products != null && reaction.ProductCoeffs != null)
        {
            for (int i = 0; i < reaction.Products.Length && i < reaction.ProductCoeffs.Length; i++)
            {
                float coeff = reaction.ProductCoeffs[i];
                if (coeff <= 0f)
                    continue;
                AddEntry(reaction.Products[i], source, coeff * reactionDelta);
            }
        }
    }

    /// <summary>扩散核评估时的 center / 邻均（与是否写回、增量是否为 0 无关）。</summary>
    public static void RecordDiffusionSnapshot(int x, int y, int substanceIndex, float center, float neighborAvg, int neighborCount)
    {
        if (!stepActive || !IsAuditCell(x, y))
            return;
        if (substanceIndex < 0 || substanceIndex >= MaxSubstances)
            return;
        if ((trackMask & (1 << substanceIndex)) == 0)
            return;

        hasDiffusionSnapshot[substanceIndex] = true;
        diffusionCenter[substanceIndex] = center;
        diffusionNeighborAvg[substanceIndex] = neighborCount > 0 ? neighborAvg : center;
    }

    public static void RecordDiffusionChange(int x, int y, int substanceIndex, float before, float after)
    {
        if (!stepActive || !IsAuditCell(x, y))
            return;
        if (substanceIndex < 0 || substanceIndex >= MaxSubstances)
            return;
        if ((trackMask & (1 << substanceIndex)) == 0)
            return;

        float delta = after - before;
        if (Mathf.Abs(delta) < Epsilon)
            return;

        ChemistrySubstanceRuntime[] substances = ChemistrySystem.Substances;
        string substanceName = substanceIndex < substances.Length
            ? substances[substanceIndex].DisplayName
            : string.Format("物质#{0}", substanceIndex);
        AddEntry(substanceIndex, string.Format("扩散：{0}", substanceName), delta);
    }

    public static void EndChemistryStep(long step)
    {
        if (!stepActive)
            return;

        stepActive = false;
        int x = targetX;
        int y = targetY;
        int mask = trackMask;

        var substanceReports = new List<ChemicalSubstanceStepReport>();
        ChemistrySubstanceRuntime[] substances = ChemistrySystem.Substances;
        int count = substances != null ? substances.Length : 0;

        for (int s = 0; s < count && s < MaxSubstances; s++)
        {
            if ((mask & (1 << s)) == 0)
                continue;

            float before = stepStartAmounts[s];
            float after = ChemistryField.GetAmount(x, y, s);
            float total = after - before;

            float snapCenter = 0f;
            float snapNeighborAvg = 0f;
            float snapGradient = 0f;
            bool hasSnap = hasDiffusionSnapshot[s];
            if (hasSnap)
            {
                snapCenter = diffusionCenter[s];
                snapNeighborAvg = diffusionNeighborAvg[s];
                snapGradient = snapNeighborAvg - snapCenter;
            }

            substanceReports.Add(new ChemicalSubstanceStepReport
            {
                SubstanceIndex = s,
                SubstanceName = substances[s].DisplayName,
                AmountBefore = before,
                AmountAfter = after,
                TotalDelta = total,
                Entries = workingEntries[s].ToArray(),
                HasDiffusionSnapshot = hasSnap,
                DiffusionCenter = snapCenter,
                DiffusionNeighborAvg = snapNeighborAvg,
                DiffusionGradient = snapGradient
            });
        }

        var report = new ChemicalCellStepReport
        {
            Step = step,
            X = x,
            Y = y,
            HasData = true,
            SubstanceReports = substanceReports.ToArray()
        };

        lock (publishLock)
        {
            lastPublished = report;
        }
    }

    static void AddEntry(int substanceIndex, string source, float delta)
    {
        if (substanceIndex < 0 || substanceIndex >= MaxSubstances)
            return;
        if ((trackMask & (1 << substanceIndex)) == 0)
            return;
        if (Mathf.Abs(delta) < Epsilon)
            return;

        workingEntries[substanceIndex].Add(new ChemicalChangeEntry
        {
            Source = source,
            Delta = delta
        });
    }

    static ChemicalCellStepReport CloneReport(ChemicalCellStepReport source)
    {
        if (source == null)
            return new ChemicalCellStepReport();

        var clone = new ChemicalCellStepReport
        {
            Step = source.Step,
            X = source.X,
            Y = source.Y,
            HasData = source.HasData,
            SubstanceReports = source.SubstanceReports
        };
        return clone;
    }

    public static string FormatDelta(float delta)
    {
        if (delta >= 0f)
            return string.Format("+{0:F4}", delta);
        return string.Format("{0:F4}", delta);
    }
}
