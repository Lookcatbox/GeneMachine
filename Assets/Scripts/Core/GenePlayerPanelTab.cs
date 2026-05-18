using UnityEngine;

public class GenePlayerPanelTab : PlayerPanelTabPage
{
    public GenePlayerPanelTab() : base("基因")
    {
    }

    public override void Draw(Rect contentRect, GUIStyle labelStyle, GUIStyle shadowStyle, PlayerPanelContext context)
    {
        GUIStyle contentStyle = BuildWrappedStyle(labelStyle);
        GUIStyle contentShadowStyle = BuildWrappedStyle(shadowStyle);
        Rect innerRect = new Rect(contentRect.x + 12f, contentRect.y + 12f, contentRect.width - 24f, contentRect.height - 24f);
        DrawTextBlock(innerRect, "基因标签页已接入页签系统，后续可在此文件内继续扩展具体交互。", contentStyle, contentShadowStyle);
    }
}