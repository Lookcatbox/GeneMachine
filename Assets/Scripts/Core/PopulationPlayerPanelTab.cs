using UnityEngine;

public class PopulationPlayerPanelTab : PlayerPanelTabPage
{
    public PopulationPlayerPanelTab() : base("种群")
    {
    }

    public override void Draw(Rect contentRect, GUIStyle labelStyle, GUIStyle shadowStyle, PlayerPanelContext context)
    {
        GUIStyle contentStyle = BuildWrappedStyle(labelStyle);
        GUIStyle contentShadowStyle = BuildWrappedStyle(shadowStyle);
        Rect innerRect = new Rect(contentRect.x + 12f, contentRect.y + 12f, contentRect.width - 24f, contentRect.height - 24f);
        DrawTextBlock(innerRect, "种群标签页已预留为独立代码文件，后续可以在这里补充种群信息与管理操作。", contentStyle, contentShadowStyle);
    }
}