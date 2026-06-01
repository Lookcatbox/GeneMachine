using UnityEngine;

public class EnvironmentPlayerPanelTab : PlayerPanelTabPage
{
    public EnvironmentPlayerPanelTab() : base("环境")
    {
    }

    public override void Draw(Rect contentRect, GUIStyle labelStyle, GUIStyle shadowStyle, PlayerPanelContext context)
    {
        GUIStyle contentStyle = BuildWrappedStyle(labelStyle);
        GUIStyle contentShadowStyle = BuildWrappedStyle(shadowStyle);
        Rect innerRect = new Rect(contentRect.x + 12f, contentRect.y + 12f, contentRect.width - 24f, contentRect.height - 24f);

        if (!context.HasDisplayEnvironmentCell || context.DisplayEnvironment == null)
        {
            string emptyText = "将鼠标移动到地图环境格上即可查看环境属性。\n左键点击环境格可锁定，再次点击同一格取消锁定。";
            DrawTextBlock(innerRect, emptyText, contentStyle, contentShadowStyle);
            return;
        }

        float infoHeight = 210f;
        Rect infoRect = new Rect(innerRect.x, innerRect.y, innerRect.width, infoHeight);
        DrawEnvironmentInfo(infoRect, context, contentStyle, contentShadowStyle);

        Rect chemRect = new Rect(innerRect.x, infoRect.yMax + 8f, innerRect.width, innerRect.yMax - infoRect.yMax - 8f);
        DrawChemicalList(chemRect, context, contentStyle, contentShadowStyle);
    }

    void DrawEnvironmentInfo(Rect rect, PlayerPanelContext context, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        Envir env = context.DisplayEnvironment;
        float tempC = SimulationCore.KelvinToCelsius(env.Temp);
        int playerCount = 0;
        int npcCount = 0;
        int maxPriority = 0;
        if (env.CellList != null)
        {
            int limit = Mathf.Min(env.CellNum, env.CellList.Length - 1);
            for (int i = 1; i <= limit; i++)
            {
                Cell cell = env.CellList[i];
                if (cell == null)
                    continue;

                if (cell.isPlayer) playerCount++;
                else npcCount++;
                if (cell.priority > maxPriority)
                    maxPriority = cell.priority;
            }
        }

        string sourceText = context.HasLockedEnvironmentCell ? "锁定" : "悬停";
        string lockHint = context.HasLockedEnvironmentCell
            ? string.Format("锁定 ({0},{1})，再次点击取消。", context.LockedEnvironmentX, context.LockedEnvironmentY)
            : "左键点击环境格可固定显示。";

        string infoText = string.Format(
            "来源: {0} | 坐标: ({1}, {2})\n地形: {3} | 高度: {4} | 温度: {5}°C | 光照: {6}\n细胞: {7}/{8} (玩家 {9}, NPC {10}) | 最高优先级: {11}\n{12}",
            sourceText,
            context.DisplayEnvironmentX,
            context.DisplayEnvironmentY,
            GetTopographyName(env.Topography),
            env.Height,
            tempC.ToString("F1"),
            env.Light,
            env.CellNum,
            env.MaxCellNum,
            playerCount,
            npcCount,
            maxPriority,
            lockHint);

        DrawTextBlock(rect, infoText, labelStyle, shadowStyle);
    }

    void DrawChemicalList(Rect rect, PlayerPanelContext context, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        Envir env = context.DisplayEnvironment;
        GUI.Box(rect, "");

        float padding = 8f;
        float headerHeight = 28f;
        Rect headerRect = new Rect(rect.x + padding, rect.y + padding, rect.width - padding * 2f, headerHeight);
        int overlayMask = ChemistrySystem.ChemicalOverlayMask;
        string headerText = overlayMask != 0 ? "物质列表（点击切换地图热力图）" : "物质列表（点击显示浓度热力图）";
        DrawTextBlock(headerRect, headerText, labelStyle, shadowStyle);

        if (overlayMask != 0)
        {
            float clearWidth = 88f;
            Rect clearRect = new Rect(headerRect.xMax - clearWidth, headerRect.y, clearWidth, headerHeight);
            if (GUI.Button(clearRect, "清除视图"))
                ChemistrySystem.SetChemicalOverlayMask(0);
        }

        float rowHeight = 34f;
        float rowSpacing = 4f;
        float y = headerRect.yMax + 6f;
        float rowWidth = rect.width - padding * 2f;
        ChemicalSubstance[] substances = ChemistrySystem.DefaultOrder;

        for (int i = 0; i < substances.Length; i++)
        {
            if (y + rowHeight > rect.yMax - padding)
                break;

            ChemicalSubstance substance = substances[i];
            bool selected = ChemistrySystem.IsChemicalOverlaySelected(substance);
            Rect rowRect = new Rect(rect.x + padding, y, rowWidth, rowHeight);

            GUI.backgroundColor = selected ? new Color(0.24f, 0.34f, 0.52f) : new Color(0.22f, 0.22f, 0.22f);
            if (GUI.Button(rowRect, ""))
                ChemistrySystem.ToggleChemicalOverlay(substance);
            GUI.backgroundColor = Color.white;

            Color substanceColor = ChemistrySystem.GetSubstanceColor(substance);
            Rect swatchRect = new Rect(rowRect.x + 6f, rowRect.y + 8f, 18f, 18f);
            GUI.color = substanceColor;
            GUI.DrawTexture(swatchRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            string phaseName = GetPhaseName(ChemistrySystem.GetSubstancePhase(substance));
            float amount = env.GetChemicalAmount(substance);
            string rowText = string.Format("{0} ({1})  {2:F2} 单位{3}",
                ChemistrySystem.GetSubstanceName(substance),
                phaseName,
                amount,
                selected ? "  [显示中]" : "");

            Rect textRect = new Rect(swatchRect.xMax + 8f, rowRect.y + 6f, rowRect.width - 40f, rowHeight - 8f);
            GUI.Label(new Rect(textRect.x + 1f, textRect.y + 1f, textRect.width, textRect.height), rowText, shadowStyle);
            GUI.Label(textRect, rowText, labelStyle);

            y += rowHeight + rowSpacing;
        }
    }

    string GetPhaseName(ChemicalPhase phase)
    {
        switch (phase)
        {
            case ChemicalPhase.Solid: return "固";
            case ChemicalPhase.Liquid: return "液";
            case ChemicalPhase.Gas: return "气";
            default: return "?";
        }
    }

    string GetTopographyName(int topography)
    {
        switch (topography)
        {
            case 0: return "海洋";
            case 1: return "陆地";
            case 2: return "沙滩";
            case 3: return "河流";
            case 4: return "湖泊";
            default: return "未知";
        }
    }
}
