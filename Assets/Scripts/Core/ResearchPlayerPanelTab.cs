using UnityEngine;

public class ResearchPlayerPanelTab : PlayerPanelTabPage
{
    private bool showUpgradePopup = false;
    private Rect lastUpgradePopupRect;
    private Rect lastUpgradeButtonRect;

    public ResearchPlayerPanelTab() : base("研发")
    {
    }

    public override void Draw(Rect contentRect, GUIStyle labelStyle, GUIStyle shadowStyle, PlayerPanelContext context)
    {
        GUIStyle contentStyle = BuildWrappedStyle(labelStyle);
        GUIStyle contentShadowStyle = BuildWrappedStyle(shadowStyle);
        Rect innerRect = new Rect(contentRect.x + 12f, contentRect.y + 12f, contentRect.width - 24f, contentRect.height - 24f);

        Event currentEvent = Event.current;
        Vector2 mousePos = currentEvent.mousePosition;

        GUIStyle titleStyle = new GUIStyle(labelStyle);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontSize = 18;
        titleStyle.fontStyle = FontStyle.Bold;

        GUIStyle titleShadowStyle = new GUIStyle(shadowStyle);
        titleShadowStyle.alignment = TextAnchor.MiddleCenter;
        titleShadowStyle.fontSize = 18;
        titleShadowStyle.fontStyle = FontStyle.Bold;

        float headerHeight = 32f;
        Rect headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, headerHeight);
        string pointText = string.Format("研发点: {0}  每回合+{1}",
            SimulationCore.GetResearchPoints(),
            SimulationCore.GetLastResearchGainAmount());
        GUI.Label(new Rect(headerRect.x + 1f, headerRect.y + 1f, headerRect.width, headerRect.height), pointText, titleShadowStyle);
        GUI.Label(headerRect, pointText, titleStyle);

        float infoPanelWidth = Mathf.Clamp(innerRect.width * 0.32f, 180f, 260f);
        float spacing = 12f;
        Rect treeRect = new Rect(innerRect.x, innerRect.y + headerHeight + spacing, innerRect.width - infoPanelWidth - spacing, innerRect.height - headerHeight - spacing);
        Rect infoRect = new Rect(treeRect.xMax + spacing, treeRect.y, infoPanelWidth, treeRect.height);

        GUI.Box(treeRect, "");
        GUI.Box(infoRect, "");

        string hoveredInfo = "将鼠标悬停在研发节点上以查看详细信息。";

        float nodeWidth = Mathf.Min(220f, treeRect.width - 16f);
        float nodeHeight = 118f;
        Rect baseNodeRect = new Rect(treeRect.center.x - nodeWidth * 0.5f, treeRect.y + 12f, nodeWidth, nodeHeight);

        DrawGeneNode(baseNodeRect,
            "基础温度耐受",
            "初始拥有",
            "已拥有",
            false,
            contentStyle,
            contentShadowStyle);

        if (baseNodeRect.Contains(mousePos))
        {
            hoveredInfo = BuildBaseTempInfo();
        }

        Rect expandRect = new Rect(baseNodeRect.x + 6f, baseNodeRect.yMax + 6f, baseNodeRect.width - 12f, 24f);
        lastUpgradeButtonRect = expandRect;
        string expandLabel = showUpgradePopup ? "收起升级" : "展开升级";
        if (GUI.Button(expandRect, expandLabel))
        {
            showUpgradePopup = !showUpgradePopup;
        }

        if (showUpgradePopup)
        {
            DrawUpgradePopup(treeRect, expandRect, mousePos, ref hoveredInfo, contentStyle, contentShadowStyle);
        }

        DrawInfoPanel(infoRect, hoveredInfo, contentStyle, contentShadowStyle);

        if (showUpgradePopup && currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            if (!lastUpgradePopupRect.Contains(mousePos) && !lastUpgradeButtonRect.Contains(mousePos))
            {
                showUpgradePopup = false;
                currentEvent.Use();
            }
        }
    }

    private void DrawUpgradePopup(Rect treeRect, Rect anchorRect, Vector2 mousePos, ref string hoveredInfo,
        GUIStyle contentStyle, GUIStyle contentShadowStyle)
    {
        float popupWidth = Mathf.Min(treeRect.width - 16f, anchorRect.width + 160f);
        float stageHeight = 108f;
        float stageSpacing = 8f;
        int maxLevel = SimulationConfig.ResearchTempUpgradeMaxLevel;
        float popupHeight = 16f + maxLevel * stageHeight + (maxLevel - 1) * stageSpacing;

        Rect popupRect = new Rect(
            treeRect.center.x - popupWidth * 0.5f,
            anchorRect.yMax + 8f,
            popupWidth,
            Mathf.Min(popupHeight, treeRect.yMax - anchorRect.yMax - 12f));

        lastUpgradePopupRect = popupRect;
        GUI.Box(popupRect, "");

        float padding = 8f;
        float columnSpacing = 10f;
        float columnWidth = (popupRect.width - padding * 2f - columnSpacing) * 0.5f;

        Rect leftColumn = new Rect(popupRect.x + padding, popupRect.y + padding, columnWidth, popupRect.height - padding * 2f);
        Rect rightColumn = new Rect(leftColumn.xMax + columnSpacing, popupRect.y + padding, columnWidth, popupRect.height - padding * 2f);

        DrawUpgradeColumn(leftColumn, "低温耐受升级", true, mousePos, ref hoveredInfo, contentStyle, contentShadowStyle);
        DrawUpgradeColumn(rightColumn, "高温耐受升级", false, mousePos, ref hoveredInfo, contentStyle, contentShadowStyle);
    }

    private void DrawUpgradeColumn(Rect columnRect, string title, bool isLowUpgrade, Vector2 mousePos,
        ref string hoveredInfo, GUIStyle contentStyle, GUIStyle contentShadowStyle)
    {
        GUI.Label(new Rect(columnRect.x + 4f, columnRect.y, columnRect.width - 8f, 20f), title, contentStyle);

        int currentLevel = isLowUpgrade ? SimulationCore.TempLowUpgradeLevel : SimulationCore.TempHighUpgradeLevel;
        int maxLevel = SimulationConfig.ResearchTempUpgradeMaxLevel;
        float nodeHeight = 108f;
        float spacing = 10f;
        float y = columnRect.y + 24f;

        for (int level = 1; level <= maxLevel; level++)
        {
            Rect nodeRect = new Rect(columnRect.x + 2f, y, columnRect.width - 4f, nodeHeight);
            int cost = SimulationCore.GetTempUpgradeCost(level);
            bool researched = level <= currentLevel;
            bool canResearch = level == currentLevel + 1;

            string name = string.Format("{0} Lv{1}", title, level);
            string costText = string.Format("消耗: {0}", cost);
            string buttonLabel = researched ? "已研发" : (canResearch ? "研发" : "未解锁");

            bool buttonEnabled = canResearch && SimulationCore.GetResearchPoints() >= cost;

            DrawGeneNode(nodeRect, name, costText, buttonLabel, buttonEnabled, contentStyle, contentShadowStyle,
                () =>
                {
                    if (isLowUpgrade)
                        SimulationCore.TryUpgradeTempLow();
                    else
                        SimulationCore.TryUpgradeTempHigh();
                });

            if (nodeRect.Contains(mousePos))
            {
                hoveredInfo = BuildUpgradeInfo(isLowUpgrade, level, cost);
            }

            y += nodeHeight + spacing;
        }
    }

    private void DrawGeneNode(Rect rect, string name, string costText, string buttonLabel, bool buttonEnabled,
        GUIStyle contentStyle, GUIStyle contentShadowStyle, System.Action onClick = null)
    {
        GUI.Box(rect, "");
        float lineHeight = Mathf.Max(20f, contentStyle.fontSize + 6f);
        float desiredGap = Mathf.Max(17f, contentStyle.fontSize * 0.9f + 3f);
        float topPadding = 8f;
        float bottomPadding = 8f;
        float lineSpacing = Mathf.Max(9f, contentStyle.fontSize * 0.35f + 3f);
        float buttonHeight = Mathf.Max(24f, contentStyle.fontSize + 6f);
        Rect nameRect = new Rect(rect.x + 8f, rect.y + topPadding, rect.width - 16f, lineHeight);
        Rect costRect = new Rect(rect.x + 8f, nameRect.yMax + lineSpacing, rect.width - 16f, lineHeight);
        float maxGap = rect.yMax - bottomPadding - buttonHeight - costRect.yMax;
        float gap = Mathf.Min(desiredGap, maxGap);
        if (maxGap >= 9f)
            gap = Mathf.Max(9f, gap);
        float buttonY = costRect.yMax + gap;
        Rect buttonRect = new Rect(rect.x + 8f, buttonY, rect.width - 16f, buttonHeight);

        GUI.Label(new Rect(nameRect.x + 1f, nameRect.y + 1f, nameRect.width, nameRect.height), name, contentShadowStyle);
        GUI.Label(nameRect, name, contentStyle);
        GUI.Label(new Rect(costRect.x + 1f, costRect.y + 1f, costRect.width, costRect.height), costText, contentShadowStyle);
        GUI.Label(costRect, costText, contentStyle);

        bool originalEnabled = GUI.enabled;
        GUI.enabled = buttonEnabled && onClick != null;
        if (GUI.Button(buttonRect, buttonLabel) && onClick != null)
            onClick();
        GUI.enabled = originalEnabled;
    }

    private void DrawInfoPanel(Rect infoRect, string infoText, GUIStyle contentStyle, GUIStyle contentShadowStyle)
    {
        Rect textRect = new Rect(infoRect.x + 10f, infoRect.y + 10f, infoRect.width - 20f, infoRect.height - 20f);
        DrawTextBlock(textRect, infoText, contentStyle, contentShadowStyle);
    }

    private string BuildBaseTempInfo()
    {
        return string.Format(
            "名称: 基础温度耐受\n描述: 在{0}-{1}°C范围内获得适应奖励，超出范围将致死。\n效果: 低温/高温耐受可通过升级扩展。\n研发: 初始拥有",
            SimulationConfig.BaseTempToleranceMin,
            SimulationConfig.BaseTempToleranceMax);
    }

    private string BuildUpgradeInfo(bool isLowUpgrade, int level, int cost)
    {
        string title = isLowUpgrade ? "基础低温耐受升级" : "基础高温耐受升级";
        string effect = isLowUpgrade
            ? string.Format("温度下限 -{0}", level)
            : string.Format("温度上限 +{0}", level);
        return string.Format(
            "名称: {0} Lv{1}\n效果: {2}\n研发消耗: {3}",
            title,
            level,
            effect,
            cost);
    }
}