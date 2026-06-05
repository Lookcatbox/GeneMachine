// ChemistryConfigData.cs - 化学 JSON 配置的数据结构（与 StreamingAssets/chemistry-reactions.json 对应）

/// <summary>化学配置文件根节点。</summary>
[System.Serializable]
public class ChemistryConfigFile
{
    public int version = 1;                              // 配置版本号
    public ChemistrySubstanceConfig[] substances;        // 物质列表
    public ChemistryReactionConfig[] reactions;        // 反应列表
}

/// <summary>单种物质的静态定义（相态、颜色、热力图与开局基准浓度）。</summary>
[System.Serializable]
public class ChemistrySubstanceConfig
{
    public string id;              // 物质唯一 id（反应表达式中引用）
    public string displayName;     // 显示名称
    public string phase;           // Solid / Liquid / Gas
    public string color;           // 热力图颜色（#RRGGBB）
    public float overlayMax = 1f;  // 浓度热力图归一化上限
    public float baselineLand = 0f;  // 陆地格开局初始浓度（仅 SeedEnvironmentBaselines 使用）
    public float baselineWater = 0f; // 水域格开局初始浓度
}

/// <summary>单条非生物化学反应定义。</summary>
[System.Serializable]
public class ChemistryReactionConfig
{
    public string id;                              // 反应 id
    public string name;                            // 显示名称
    public bool enabled = true;                    // 是否启用
    public int priority;                           // 优先级（大者优先判定）
    public ChemistryReactionTermConfig[] reactants; // 反应物及化学计量系数
    public ChemistryReactionTermConfig[] products;  // 生成物及系数
    public string condition;                       // 条件表达式（0=不反应）
    public string rateExpression;                  // 速率表达式（每回合消耗的反应物摩尔进度）
}

/// <summary>反应式中单项物质及其系数。</summary>
[System.Serializable]
public class ChemistryReactionTermConfig
{
    public string substanceId; // 物质 id
    public float coeff = 1f;   // 化学计量系数（须 > 0）
}
