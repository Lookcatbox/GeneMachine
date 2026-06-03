public static class EnvironmentDiffusionSystem
{
    // 扩散按物质串行调度，每个物质（含温度）内部对行并行（见 HeatDiffusion / ChemicalDiffusion）。
    // 这样并行度始终接近行数，可吃满所有核心，且与外层串行配合避免嵌套并行造成线程过度订阅；
    // 即使后续加入更多物质，按行并行也不会逊于按物质并行。
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
