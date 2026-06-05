// PlayerPanelTabPage.cs - 玩家右侧操作面板页签基类
using UnityEngine;

/// <summary>玩家面板单个页签的抽象基类；子类实现 <see cref="Draw"/> 绘制内容。</summary>
public abstract class PlayerPanelTabPage
{
    /// <summary>页签标题（显示在标签栏）。</summary>
    public string Title { get; private set; }

    protected PlayerPanelTabPage(string title)
    {
        Title = title;
    }

    /// <summary>在指定矩形内绘制页签内容。</summary>
    public abstract void Draw(Rect contentRect, GUIStyle labelStyle, GUIStyle shadowStyle, PlayerPanelContext context);

    /// <summary>带阴影的文本绘制（标签 + 偏移阴影层）。</summary>
    protected void DrawTextBlock(Rect rect, string text, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadowStyle);
        GUI.Label(rect, text, labelStyle);
    }

    /// <summary>从源样式派生可换行的左上对齐样式。</summary>
    protected GUIStyle BuildWrappedStyle(GUIStyle sourceStyle)
    {
        GUIStyle style = new GUIStyle(sourceStyle);
        style.alignment = TextAnchor.UpperLeft;
        style.wordWrap = true;
        style.fontSize = 15;
        return style;
    }
}

/// <summary>页签绘制时传入的环境格悬停/锁定状态。</summary>
public struct PlayerPanelContext
{
    public bool HasHoveredEnvironmentCell;   // 鼠标是否悬停在有效环境格上
    public int HoveredEnvironmentX;
    public int HoveredEnvironmentY;
    public bool HasLockedEnvironmentCell;    // 是否已左键锁定某一格
    public int LockedEnvironmentX;
    public int LockedEnvironmentY;
    public bool HasDisplayEnvironmentCell;    // 当前用于展示详情的格（锁定优先于悬停）
    public int DisplayEnvironmentX;
    public int DisplayEnvironmentY;
    public Envir DisplayEnvironment;         // 展示用环境格引用（可能为 null）
}
