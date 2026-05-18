using UnityEngine;

public abstract class PlayerPanelTabPage
{
    public string Title { get; private set; }

    protected PlayerPanelTabPage(string title)
    {
        Title = title;
    }

    public abstract void Draw(Rect contentRect, GUIStyle labelStyle, GUIStyle shadowStyle, PlayerPanelContext context);

    protected void DrawTextBlock(Rect rect, string text, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadowStyle);
        GUI.Label(rect, text, labelStyle);
    }

    protected GUIStyle BuildWrappedStyle(GUIStyle sourceStyle)
    {
        GUIStyle style = new GUIStyle(sourceStyle);
        style.alignment = TextAnchor.UpperLeft;
        style.wordWrap = true;
        style.fontSize = 15;
        return style;
    }
}

public struct PlayerPanelContext
{
    public bool HasHoveredEnvironmentCell;
    public int HoveredEnvironmentX;
    public int HoveredEnvironmentY;
    public bool HasLockedEnvironmentCell;
    public int LockedEnvironmentX;
    public int LockedEnvironmentY;
    public bool HasDisplayEnvironmentCell;
    public int DisplayEnvironmentX;
    public int DisplayEnvironmentY;
    public Envir DisplayEnvironment;
}