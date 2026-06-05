// EnvironmentDiffusionSystem.cs - 环境扩散调度（温度 + 各可扩散物质，物质间串行）
/// <summary>每步依次执行热扩散与各可扩散物质扩散，并通知化学 revision。</summary>
public static class EnvironmentDiffusionSystem
{
    // 扩散按物质串行调度，每个物质（含温度）内部对行并行（见 HeatDiffusion / ChemicalDiffusion）。
    // 每步总复杂度 ≈ O(EnvirSize² × (1 + diffusibleSubstances))，每种物质/温度各 3 遍全图扫描。
    // 串行调度是为避免「物质并行 × 行并行」嵌套并行导致线程过度订阅；行并行度≈EnvirSize，可吃满核心。
    public static void Update(Envir[,] envirData)
    {
        if (envirData == null)
            return;

        ChemistrySubstanceRuntime[] diffusibleSubstances = ChemistrySystem.GetDiffusibleSubstances();
        int size = SimulationConfig.EnvirSize;
        ChemicalDiffusion.PrepareBuffers(size, ChemistrySystem.SubstanceCount);

        HeatDiffusion.Update(envirData);
        for (int i = 0; i < diffusibleSubstances.Length; i++)
        {
            ChemicalDiffusion.Update(envirData, diffusibleSubstances[i]);
        }

        if (diffusibleSubstances.Length > 0)
            ChemistrySystem.MarkChemicalAmountsChanged();
    }
}
