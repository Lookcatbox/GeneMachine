[System.Serializable]
public class ChemistryConfigFile
{
    public int version = 1;
    public ChemistrySubstanceConfig[] substances;
    public ChemistryReactionConfig[] reactions;
}

[System.Serializable]
public class ChemistrySubstanceConfig
{
    public string id;
    public string displayName;
    public string phase;
    public string color;
    public float overlayMax = 1f;
    public float baselineLand = 0f;
    public float baselineWater = 0f;
}

[System.Serializable]
public class ChemistryReactionConfig
{
    public string id;
    public string name;
    public bool enabled = true;
    public int priority;
    public ChemistryReactionTermConfig[] reactants;
    public ChemistryReactionTermConfig[] products;
    public string condition;
    public string rateExpression;
}

[System.Serializable]
public class ChemistryReactionTermConfig
{
    public string substanceId;
    public float coeff = 1f;
}
