// GenePlayerPanelTab.cs - 玩家面板「基因」页：基因视图筛选与全图基因统计
using System.Collections.Generic;
using UnityEngine;

/// <summary>基因列表与 <see cref="CellRenderer.geneFilterBaseId"/> 筛选控制。</summary>
public class GenePlayerPanelTab : PlayerPanelTabPage
{
    public GenePlayerPanelTab() : base("基因")
    {
    }

    public override void Draw(Rect contentRect, GUIStyle labelStyle, GUIStyle shadowStyle, PlayerPanelContext context)
    {
        GUIStyle contentStyle = BuildWrappedStyle(labelStyle);
        GUIStyle contentShadowStyle = BuildWrappedStyle(shadowStyle);
        float padding = 10f;
        Rect innerRect = new Rect(contentRect.x + padding, contentRect.y + padding,
            contentRect.width - padding * 2f, contentRect.height - padding * 2f);

        // 顶部：当前基因视图状态与清除按钮
        float headerHeight = 52f;
        Rect headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, headerHeight);
        DrawFilterHeader(headerRect, contentStyle, contentShadowStyle);

        // 基因列表区域
        Rect listRect = new Rect(innerRect.x, headerRect.yMax + 8f, innerRect.width, innerRect.yMax - headerRect.yMax - 8f);
        DrawGeneList(listRect, contentStyle, contentShadowStyle);
    }

    void DrawFilterHeader(Rect rect, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        int filterId = CellRenderer.geneFilterBaseId;
        string statusText = filterId > 0
            ? string.Format("基因视图: {0}", Gene.GetBaseName(filterId))
            : "基因视图: 未选中（显示全部细胞）";

        float buttonWidth = 110f;
        float buttonHeight = 26f;
        Rect textRect = new Rect(rect.x, rect.y, rect.width - buttonWidth - 8f, rect.height - buttonHeight - 4f);
        DrawTextBlock(textRect, statusText, labelStyle, shadowStyle);

        Rect clearRect = new Rect(rect.xMax - buttonWidth, rect.yMax - buttonHeight, buttonWidth, buttonHeight);
        GUI.enabled = filterId > 0;
        if (GUI.Button(clearRect, "显示全部细胞"))
            CellRenderer.geneFilterBaseId = 0;
        GUI.enabled = true;
    }

    void DrawGeneList(Rect rect, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        GUI.Box(rect, "");

        List<GenePresenceEntry> entries = Gene.BuildPresenceList();
        if (entries.Count == 0)
        {
            DrawTextBlock(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f),
                "当前没有任何细胞拥有基因。", labelStyle, shadowStyle);
            return;
        }

        float rowSpacing = 6f;
        float y = rect.y + 8f;
        float rowWidth = rect.width - 16f;
        int filterId = CellRenderer.geneFilterBaseId;

        for (int i = 0; i < entries.Count; i++)
        {
            GenePresenceEntry entry = entries[i];
            float rowHeight = CalcRowHeight(entry, rowWidth - 12f, labelStyle);
            if (y + rowHeight > rect.yMax - 8f)
                break;

            Rect rowRect = new Rect(rect.x + 8f, y, rowWidth, rowHeight);
            bool selected = entry.baseId == filterId;
            GUI.backgroundColor = selected ? new Color(0.24f, 0.34f, 0.52f) : new Color(0.22f, 0.22f, 0.22f);
            if (GUI.Button(rowRect, ""))
            {
                if (selected)
                    CellRenderer.geneFilterBaseId = 0;
                else
                    CellRenderer.geneFilterBaseId = entry.baseId;
            }
            GUI.backgroundColor = Color.white;

            float textX = rowRect.x + 8f;
            float textW = rowRect.width - 16f;
            string nameText = Gene.GetBaseName(entry.baseId);
            string countText = string.Format("拥有细胞数: {0}", entry.cellCount);
            string descText = Gene.GetBaseDescription(entry.baseId);

            Rect nameRect = new Rect(textX, rowRect.y + 4f, textW, 20f);
            Rect countRect = new Rect(textX, nameRect.yMax + 2f, textW, 18f);
            Rect descRect = new Rect(textX, countRect.yMax + 2f, textW, rowHeight - (countRect.yMax - rowRect.y) - 4f);

            GUIStyle nameStyle = new GUIStyle(labelStyle);
            nameStyle.fontStyle = FontStyle.Bold;
            GUIStyle nameShadow = new GUIStyle(shadowStyle);
            nameShadow.fontStyle = FontStyle.Bold;

            GUI.Label(new Rect(nameRect.x + 1f, nameRect.y + 1f, nameRect.width, nameRect.height), nameText, nameShadow);
            GUI.Label(nameRect, nameText, nameStyle);
            GUI.Label(new Rect(countRect.x + 1f, countRect.y + 1f, countRect.width, countRect.height), countText, shadowStyle);
            GUI.Label(countRect, countText, labelStyle);
            DrawTextBlock(descRect, descText, labelStyle, shadowStyle);

            y += rowHeight + rowSpacing;
        }
    }

    float CalcRowHeight(GenePresenceEntry entry, float textWidth, GUIStyle labelStyle)
    {
        string descText = Gene.GetBaseDescription(entry.baseId);
        GUIStyle wrapStyle = new GUIStyle(labelStyle);
        wrapStyle.wordWrap = true;
        float descHeight = wrapStyle.CalcHeight(new GUIContent(descText), textWidth);
        return 4f + 20f + 2f + 18f + 2f + descHeight + 4f;
    }
}
