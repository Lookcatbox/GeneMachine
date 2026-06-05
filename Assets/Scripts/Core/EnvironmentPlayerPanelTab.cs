// EnvironmentPlayerPanelTab.cs - 玩家面板「环境」页：悬停/锁定格的环境与化学信息

using UnityEngine;



/// <summary>根据 <see cref="PlayerPanelContext"/> 显示温度、光照、地形与物质存量。</summary>

public class EnvironmentPlayerPanelTab : PlayerPanelTabPage

{

    bool showStepChangeDetail;

    Vector2 stepChangeScroll;



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

            showStepChangeDetail = false;

            string emptyText = "将鼠标移动到地图环境格上即可查看环境属性。\n左键点击环境格可锁定，再次点击同一格取消锁定。";

            DrawTextBlock(innerRect, emptyText, contentStyle, contentShadowStyle);

            return;

        }



        if (showStepChangeDetail)

        {

            DrawStepChangeDetail(innerRect, context, contentStyle, contentShadowStyle, labelStyle, shadowStyle);

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

        int cellX = context.DisplayEnvironmentX;

        int cellY = context.DisplayEnvironmentY;

        GUI.Box(rect, "");



        float padding = 8f;

        float headerHeight = 28f;

        float buttonHeight = 26f;

        int overlayMask = ChemistrySystem.ChemicalOverlayMask;



        Rect headerRect = new Rect(rect.x + padding, rect.y + padding, rect.width - padding * 2f, headerHeight);

        string headerText = overlayMask != 0 ? "物质列表（点击切换地图热力图）" : "物质列表（点击显示浓度热力图）";

        DrawTextBlock(headerRect, headerText, labelStyle, shadowStyle);



        float actionY = headerRect.yMax + 4f;

        float clearWidth = 88f;

        float auditWidth = 148f;

        float actionRowHeight = buttonHeight + 4f;



        if (overlayMask != 0)

        {

            Rect clearRect = new Rect(rect.x + padding, actionY, clearWidth, buttonHeight);

            if (GUI.Button(clearRect, "清除视图"))

                ChemistrySystem.SetChemicalOverlayMask(0);



            Rect auditRect = new Rect(clearRect.xMax + 6f, actionY, auditWidth, buttonHeight);

            if (GUI.Button(auditRect, "查看本回合变动"))

            {

                ChemicalStepAudit.SetTarget(cellX, cellY, overlayMask);

                showStepChangeDetail = true;

                stepChangeScroll = Vector2.zero;

            }

        }

        else

        {

            Rect hintRect = new Rect(rect.x + padding, actionY, rect.width - padding * 2f, buttonHeight);

            GUI.enabled = false;

            GUI.Button(hintRect, "查看本回合变动（请先选中物质）");

            GUI.enabled = true;

        }



        float rowHeight = 34f;

        float rowSpacing = 4f;

        float y = actionY + actionRowHeight + 4f;

        float rowWidth = rect.width - padding * 2f;

        ChemistrySubstanceRuntime[] substances = ChemistrySystem.Substances;



        for (int i = 0; i < substances.Length; i++)

        {

            if (y + rowHeight > rect.yMax - padding)

                break;



            ChemistrySubstanceRuntime substance = substances[i];

            bool selected = ChemistrySystem.IsChemicalOverlaySelected(substance.Index);

            Rect rowRect = new Rect(rect.x + padding, y, rowWidth, rowHeight);



            GUI.backgroundColor = selected ? new Color(0.24f, 0.34f, 0.52f) : new Color(0.22f, 0.22f, 0.22f);

            if (GUI.Button(rowRect, ""))

                ChemistrySystem.ToggleChemicalOverlay(substance.Index);

            GUI.backgroundColor = Color.white;



            Color substanceColor = substance.Color;

            Rect swatchRect = new Rect(rowRect.x + 6f, rowRect.y + 8f, 18f, 18f);

            GUI.color = substanceColor;

            GUI.DrawTexture(swatchRect, Texture2D.whiteTexture);

            GUI.color = Color.white;



            string phaseName = GetPhaseName(substance.Phase);

            float amount = GetDisplayChemicalAmount(cellX, cellY, env, substance.Index);

            string rowText = string.Format("{0} ({1})  {2:F2} 单位{3}",

                substance.DisplayName,

                phaseName,

                amount,

                selected ? "  [显示中]" : "");



            Rect textRect = new Rect(swatchRect.xMax + 8f, rowRect.y + 6f, rowRect.width - 40f, rowHeight - 8f);

            GUI.Label(new Rect(textRect.x + 1f, textRect.y + 1f, textRect.width, textRect.height), rowText, shadowStyle);

            GUI.Label(textRect, rowText, labelStyle);



            y += rowHeight + rowSpacing;

        }

    }



    void DrawStepChangeDetail(Rect rect, PlayerPanelContext context, GUIStyle contentStyle, GUIStyle contentShadowStyle,

        GUIStyle labelStyle, GUIStyle shadowStyle)

    {

        int cellX = context.DisplayEnvironmentX;

        int cellY = context.DisplayEnvironmentY;

        int overlayMask = ChemistrySystem.ChemicalOverlayMask;



        ChemicalStepAudit.SetTarget(cellX, cellY, overlayMask);

        ChemicalCellStepReport report = ChemicalStepAudit.GetLastReport();



        GUI.Box(rect, "");



        float padding = 10f;

        Rect backRect = new Rect(rect.x + padding, rect.y + padding, 72f, 26f);

        if (GUI.Button(backRect, "返回"))

            showStepChangeDetail = false;



        GUIStyle titleStyle = new GUIStyle(labelStyle);

        titleStyle.fontSize = 16;

        titleStyle.fontStyle = FontStyle.Bold;



        Rect titleRect = new Rect(backRect.xMax + 8f, rect.y + padding, rect.width - backRect.width - padding * 3f, 26f);

        string title = string.Format("本回合物质变动  格({0},{1})  回合 #{2}",

            cellX, cellY, report.HasData ? report.Step : SimulationCore.GetResearchStepCounter());

        DrawTextBlock(titleRect, title, titleStyle, contentShadowStyle);



        Rect listRect = new Rect(rect.x + padding, titleRect.yMax + 8f, rect.width - padding * 2f, rect.yMax - titleRect.yMax - padding - 8f);

        GUI.Box(listRect, "");



        if (overlayMask == 0)

        {

            Rect emptyRect = new Rect(listRect.x + 8f, listRect.y + 8f, listRect.width - 16f, listRect.height - 16f);

            DrawTextBlock(emptyRect, "未选中任何物质。返回后点击物质行以选中，再查看变动。", contentStyle, contentShadowStyle);

            return;

        }



        if (report.X != cellX || report.Y != cellY)

        {

            Rect noteRect = new Rect(listRect.x + 8f, listRect.y + 8f, listRect.width - 16f, 36f);

            DrawTextBlock(noteRect, "等待模拟线程完成下一回合以生成该格报告…", contentStyle, contentShadowStyle);

        }



        float contentHeight = EstimateStepChangeContentHeight(report);

        Rect viewRect = new Rect(0f, 0f, listRect.width - 24f, contentHeight);

        stepChangeScroll = GUI.BeginScrollView(listRect, stepChangeScroll, viewRect);



        float y = 6f;

        if (report.SubstanceReports == null || report.SubstanceReports.Length == 0)

        {

            Rect emptyRect = new Rect(6f, y, viewRect.width - 12f, 48f);

            string emptyText = report.HasData

                ? "上一回合选中物质在本格无可见变动（变动量低于显示精度或为 0）。"

                : "尚无回合报告。请让模拟运行至少 1 步。";

            DrawTextBlock(emptyRect, emptyText, contentStyle, contentShadowStyle);

        }

        else

        {

            for (int i = 0; i < report.SubstanceReports.Length; i++)

            {

                ChemicalSubstanceStepReport substanceReport = report.SubstanceReports[i];

                float blockHeight = EstimateSubstanceReportHeight(substanceReport);

                Rect blockRect = new Rect(6f, y, viewRect.width - 12f, blockHeight);

                DrawSubstanceReportBlock(blockRect, substanceReport, contentStyle, contentShadowStyle, labelStyle, shadowStyle);

                y += blockHeight + 10f;

            }

        }



        GUI.EndScrollView();

    }



    void DrawSubstanceReportBlock(Rect rect, ChemicalSubstanceStepReport report, GUIStyle contentStyle, GUIStyle contentShadowStyle,

        GUIStyle labelStyle, GUIStyle shadowStyle)

    {

        GUI.Box(rect, "");



        GUIStyle headerStyle = new GUIStyle(labelStyle);

        headerStyle.fontStyle = FontStyle.Bold;



        float padding = 8f;

        float y = rect.y + padding;

        string header = string.Format("{0}  |  {1:F4} → {2:F4} 单位",

            report.SubstanceName, report.AmountBefore, report.AmountAfter);

        Rect headerRect = new Rect(rect.x + padding, y, rect.width - padding * 2f, 22f);

        DrawTextBlock(headerRect, header, headerStyle, contentShadowStyle);

        y = headerRect.yMax + 4f;



        if (report.HasDiffusionSnapshot)

        {

            string snapLine = string.Format(

                "扩散诊断  center={0:F4}  neighborAvg={1:F4}  差值(邻均−center)={2}",

                report.DiffusionCenter,

                report.DiffusionNeighborAvg,

                ChemicalStepAudit.FormatDelta(report.DiffusionGradient));

            Rect snapRect = new Rect(rect.x + padding, y, rect.width - padding * 2f, 20f);

            DrawTextBlock(snapRect, snapLine, contentStyle, contentShadowStyle);

            y = snapRect.yMax + 4f;

        }



        if (report.Entries == null || report.Entries.Length == 0)

        {

            Rect lineRect = new Rect(rect.x + padding, y, rect.width - padding * 2f, 20f);

            DrawTextBlock(lineRect, "（本回合无分项记录）", contentStyle, contentShadowStyle);

            y = lineRect.yMax + 4f;

        }

        else

        {

            for (int i = 0; i < report.Entries.Length; i++)

            {

                ChemicalChangeEntry entry = report.Entries[i];

                string line = string.Format("  {0}    {1}", entry.Source, ChemicalStepAudit.FormatDelta(entry.Delta));

                Rect lineRect = new Rect(rect.x + padding, y, rect.width - padding * 2f, 20f);

                DrawTextBlock(lineRect, line, contentStyle, contentShadowStyle);

                y = lineRect.yMax + 2f;

            }

        }



        GUIStyle totalStyle = new GUIStyle(contentStyle);

        totalStyle.fontStyle = FontStyle.Bold;

        string totalLine = string.Format("本回合总变化：{0}", ChemicalStepAudit.FormatDelta(report.TotalDelta));

        Rect totalRect = new Rect(rect.x + padding, y, rect.width - padding * 2f, 22f);

        DrawTextBlock(totalRect, totalLine, totalStyle, contentShadowStyle);

    }



    float EstimateStepChangeContentHeight(ChemicalCellStepReport report)

    {

        if (report.SubstanceReports == null || report.SubstanceReports.Length == 0)

            return 60f;



        float height = 6f;

        for (int i = 0; i < report.SubstanceReports.Length; i++)

            height += EstimateSubstanceReportHeight(report.SubstanceReports[i]) + 10f;

        return height + 6f;

    }



    float EstimateSubstanceReportHeight(ChemicalSubstanceStepReport report)

    {

        int entryCount = report.Entries != null ? report.Entries.Length : 0;

        if (entryCount == 0)

            entryCount = 1;

        float height = 22f + 4f;

        if (report.HasDiffusionSnapshot)

            height += 24f;

        return height + entryCount * 22f + 4f + 24f + 16f;

    }



    static float GetDisplayChemicalAmount(int x, int y, Envir env, int substanceIndex)

    {

        if (ChemistryField.IsAllocated)

            return ChemistryField.GetAmount(x, y, substanceIndex);

        return env.GetChemicalAmount(substanceIndex);

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


