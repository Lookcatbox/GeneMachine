// EventsPlayerPanelTab.cs - 玩家面板「事件」页：活跃环境事件列表
using UnityEngine;

/// <summary>滚动展示 <see cref="EventSystem"/> 当前活跃事件快照。</summary>
public class EventsPlayerPanelTab : PlayerPanelTabPage
{
    Vector2 scrollPosition;

    public EventsPlayerPanelTab() : base("事件")
    {
    }

    public override void Draw(Rect contentRect, GUIStyle labelStyle, GUIStyle shadowStyle, PlayerPanelContext context)
    {
        GUIStyle contentStyle = BuildWrappedStyle(labelStyle);
        GUIStyle contentShadowStyle = BuildWrappedStyle(shadowStyle);
        GUIStyle titleStyle = new GUIStyle(labelStyle);
        titleStyle.fontSize = 17;
        titleStyle.fontStyle = FontStyle.Bold;

        Rect innerRect = new Rect(contentRect.x + 12f, contentRect.y + 12f, contentRect.width - 24f, contentRect.height - 24f);
        EventSystem.ActiveEventSnapshot[] snapshots = EventSystem.GetActiveEventSnapshots();

        Rect headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 28f);
        string header = string.Format("正在发生的事件: {0}", snapshots.Length);
        DrawTextBlock(headerRect, header, titleStyle, contentShadowStyle);

        Rect listRect = new Rect(innerRect.x, innerRect.y + 34f, innerRect.width, innerRect.height - 34f);
        GUI.Box(listRect, "");

        if (snapshots.Length == 0)
        {
            Rect emptyRect = new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, listRect.height - 20f);
            DrawTextBlock(emptyRect, "当前没有正在发生的事件。", contentStyle, contentShadowStyle);
            return;
        }

        float contentHeight = 0f;
        for (int i = 0; i < snapshots.Length; i++)
            contentHeight += EstimateCardHeight(listRect.width - 28f, snapshots[i]) + 10f;

        Rect viewRect = new Rect(0f, 0f, listRect.width - 24f, contentHeight);
        scrollPosition = GUI.BeginScrollView(listRect, scrollPosition, viewRect);

        float y = 0f;
        for (int i = 0; i < snapshots.Length; i++)
        {
            EventSystem.ActiveEventSnapshot snapshot = snapshots[i];
            float cardHeight = EstimateCardHeight(viewRect.width - 12f, snapshot);
            Rect cardRect = new Rect(6f, y, viewRect.width - 12f, cardHeight);
            DrawEventCard(cardRect, snapshot, contentStyle, contentShadowStyle, titleStyle, contentShadowStyle);
            y += cardHeight + 10f;
        }

        GUI.EndScrollView();
    }

    float EstimateCardHeight(float width, EventSystem.ActiveEventSnapshot snapshot)
    {
        GUIStyle measureStyle = new GUIStyle(GUI.skin.label);
        measureStyle.wordWrap = true;
        measureStyle.fontSize = 15;
        float detailHeight = measureStyle.CalcHeight(new GUIContent(snapshot.detail), width - 16f);
        return 34f + detailHeight + 12f;
    }

    void DrawEventCard(
        Rect cardRect,
        EventSystem.ActiveEventSnapshot snapshot,
        GUIStyle bodyStyle,
        GUIStyle bodyShadowStyle,
        GUIStyle titleStyle,
        GUIStyle titleShadowStyle)
    {
        GUI.Box(cardRect, "");
        Rect inner = new Rect(cardRect.x + 8f, cardRect.y + 6f, cardRect.width - 16f, cardRect.height - 12f);

        string titleLine = string.Format("#{0}  {1}", snapshot.instanceId, snapshot.title);
        Rect titleRect = new Rect(inner.x, inner.y, inner.width, 22f);
        DrawTextBlock(titleRect, titleLine, titleStyle, titleShadowStyle);

        Rect detailRect = new Rect(inner.x, inner.y + 24f, inner.width, inner.height - 24f);
        DrawTextBlock(detailRect, snapshot.detail, bodyStyle, bodyShadowStyle);
    }
}
