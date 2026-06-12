// GeneMachineGuiTheme.cs - IMGUI theme shared by menu, HUD and player panels
using UnityEngine;

/// <summary>GeneMachine 的深色荧光 IMGUI 主题。</summary>
public static class GeneMachineGuiTheme
{
    public static readonly Color Text = new Color(0.91f, 1f, 0.97f, 1f);
    public static readonly Color MutedText = new Color(0.54f, 0.66f, 0.63f, 1f);
    public static readonly Color Panel = new Color(0.04f, 0.09f, 0.10f, 0.91f);
    public static readonly Color PanelRaised = new Color(0.07f, 0.15f, 0.16f, 0.94f);
    public static readonly Color Inset = new Color(0.02f, 0.05f, 0.06f, 0.88f);
    public static readonly Color Border = new Color(0.55f, 0.96f, 1f, 0.22f);
    public static readonly Color BorderHot = new Color(0.24f, 0.91f, 1f, 0.72f);
    public static readonly Color GeneGreen = new Color(0.62f, 1f, 0.30f, 1f);
    public static readonly Color Cyan = new Color(0.24f, 0.91f, 1f, 1f);
    public static readonly Color Amber = new Color(1f, 0.74f, 0.26f, 1f);
    public static readonly Color Danger = new Color(1f, 0.36f, 0.45f, 1f);

    static Texture2D pixel;

    public static Texture2D Pixel
    {
        get
        {
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                pixel.filterMode = FilterMode.Point;
                pixel.wrapMode = TextureWrapMode.Clamp;
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            return pixel;
        }
    }

    public static GUIStyle BuildLabelStyle(int fontSize)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.normal.textColor = Text;
        return style;
    }

    public static GUIStyle BuildShadowStyle(GUIStyle sourceStyle)
    {
        GUIStyle style = new GUIStyle(sourceStyle);
        style.normal.textColor = new Color(0f, 0f, 0f, 0.72f);
        return style;
    }

    public static GUIStyle BuildTitleStyle(int fontSize, TextAnchor alignment)
    {
        GUIStyle style = BuildLabelStyle(fontSize);
        style.fontStyle = FontStyle.Bold;
        style.alignment = alignment;
        style.normal.textColor = Text;
        return style;
    }

    public static void DrawPanel(Rect rect)
    {
        DrawBox(new Rect(rect.x + 4f, rect.y + 6f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.34f), Color.clear);
        DrawBox(rect, Panel, Border);
        DrawBox(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 1f), new Color(1f, 1f, 1f, 0.06f), Color.clear);
        DrawAccentBar(rect, Cyan);
    }

    public static void DrawInset(Rect rect)
    {
        DrawBox(rect, Inset, new Color(0.55f, 0.96f, 1f, 0.13f));
    }

    public static void DrawCard(Rect rect, bool active)
    {
        DrawBox(rect, active ? new Color(0.08f, 0.19f, 0.17f, 0.94f) : PanelRaised, active ? BorderHot : Border);
        DrawAccentBar(rect, active ? GeneGreen : Cyan);
    }

    public static void DrawSectionHeader(Rect rect, string eyebrow, string title, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        GUIStyle eyebrowStyle = new GUIStyle(labelStyle);
        eyebrowStyle.fontSize = Mathf.Max(10, labelStyle.fontSize - 3);
        eyebrowStyle.fontStyle = FontStyle.Bold;
        eyebrowStyle.normal.textColor = GeneGreen;
        eyebrowStyle.alignment = TextAnchor.UpperLeft;

        GUIStyle titleStyle = new GUIStyle(labelStyle);
        titleStyle.fontSize = Mathf.Max(18, labelStyle.fontSize + 6);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Text;
        titleStyle.alignment = TextAnchor.UpperLeft;

        DrawText(new Rect(rect.x, rect.y, rect.width, 18f), eyebrow.ToUpperInvariant(), eyebrowStyle, shadowStyle);
        DrawText(new Rect(rect.x, rect.y + 18f, rect.width, rect.height - 18f), title, titleStyle, shadowStyle);
    }

    public static void DrawMetricChip(Rect rect, string label, string value, Color accent, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        DrawBox(rect, new Color(0.02f, 0.06f, 0.07f, 0.94f), new Color(accent.r, accent.g, accent.b, 0.32f));
        DrawBox(new Rect(rect.x, rect.y, rect.width, 2f), new Color(accent.r, accent.g, accent.b, 0.76f), Color.clear);

        GUIStyle smallStyle = new GUIStyle(labelStyle);
        smallStyle.fontSize = Mathf.Max(10, labelStyle.fontSize - 4);
        smallStyle.normal.textColor = MutedText;

        GUIStyle valueStyle = new GUIStyle(labelStyle);
        valueStyle.fontSize = Mathf.Max(15, labelStyle.fontSize + 1);
        valueStyle.fontStyle = FontStyle.Bold;
        valueStyle.normal.textColor = Text;

        DrawText(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 16f), label, smallStyle, shadowStyle);
        DrawText(new Rect(rect.x + 10f, rect.y + 24f, rect.width - 20f, 22f), value, valueStyle, shadowStyle);
    }

    public static void DrawStatusDot(Vector2 center, Color color)
    {
        DrawBox(new Rect(center.x - 4f, center.y - 4f, 8f, 8f), color, Color.clear);
        DrawBox(new Rect(center.x - 7f, center.y - 7f, 14f, 14f), new Color(color.r, color.g, color.b, 0.10f), new Color(color.r, color.g, color.b, 0.26f));
    }

    public static void DrawGeneMark(Rect rect, float alpha)
    {
        Color cyan = new Color(Cyan.r, Cyan.g, Cyan.b, alpha);
        Color green = new Color(GeneGreen.r, GeneGreen.g, GeneGreen.b, alpha);
        Color amber = new Color(Amber.r, Amber.g, Amber.b, alpha);
        Color frame = new Color(Text.r, Text.g, Text.b, alpha * 0.28f);

        DrawBox(rect, new Color(0.02f, 0.06f, 0.07f, alpha * 0.20f), frame);

        float unit = Mathf.Min(rect.width, rect.height) / 9f;
        float left = rect.x + unit * 1.5f;
        float top = rect.y + unit * 1.5f;
        float right = rect.xMax - unit * 1.5f;
        float bottom = rect.yMax - unit * 1.5f;
        float midX = rect.center.x;
        float midY = rect.center.y;

        DrawBox(new Rect(left, top, unit * 3.2f, unit), cyan, Color.clear);
        DrawBox(new Rect(left, top, unit, unit * 3.2f), cyan, Color.clear);
        DrawBox(new Rect(left, midY - unit * 0.5f, unit * 3.2f, unit), cyan, Color.clear);
        DrawBox(new Rect(left, midY - unit * 0.5f, unit, unit * 3.2f), green, Color.clear);
        DrawBox(new Rect(left, bottom - unit, unit * 3.2f, unit), green, Color.clear);

        DrawBox(new Rect(midX - unit * 0.5f, top, unit, bottom - top), new Color(cyan.r, cyan.g, cyan.b, alpha * 0.66f), Color.clear);
        DrawBox(new Rect(midX - unit * 0.5f, midY - unit * 0.5f, unit * 3.7f, unit), green, Color.clear);
        DrawBox(new Rect(right - unit, top, unit, bottom - top), green, Color.clear);

        DrawNode(new Vector2(left, top), unit, cyan);
        DrawNode(new Vector2(left, midY - unit * 0.5f), unit, amber);
        DrawNode(new Vector2(midX - unit * 0.5f, midY - unit * 0.5f), unit, green);
        DrawNode(new Vector2(right - unit, midY - unit * 0.5f), unit, amber);
        DrawNode(new Vector2(right - unit, bottom - unit), unit, green);
    }

    static void DrawNode(Vector2 position, float size, Color color)
    {
        Rect glow = new Rect(position.x - size * 0.35f, position.y - size * 0.35f, size * 1.7f, size * 1.7f);
        DrawBox(glow, new Color(color.r, color.g, color.b, color.a * 0.14f), Color.clear);
        DrawBox(new Rect(position.x, position.y, size, size), new Color(0.02f, 0.06f, 0.07f, 0.96f), color);
    }

    public static void DrawBox(Rect rect, Color fill, Color border)
    {
        Color previousColor = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(rect, Pixel);

        if (border.a > 0f && rect.width >= 2f && rect.height >= 2f)
        {
            GUI.color = border;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Pixel);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Pixel);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), Pixel);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Pixel);
        }

        GUI.color = previousColor;
    }

    public static void DrawAccentBar(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), Pixel);
        GUI.color = previousColor;
    }

    public static void DrawGrid(Rect rect, float spacing)
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.035f);

        for (float x = rect.x + spacing; x < rect.xMax; x += spacing)
            GUI.DrawTexture(new Rect(x, rect.y, 1f, rect.height), Pixel);
        for (float y = rect.y + spacing; y < rect.yMax; y += spacing)
            GUI.DrawTexture(new Rect(rect.x, y, rect.width, 1f), Pixel);

        GUI.color = previousColor;
    }

    /// <summary>绘制单行文字（单层，无阴影、无悬停位移）。shadowStyle 保留参数以兼容旧调用。</summary>
    public static void DrawText(Rect rect, string text, GUIStyle style, GUIStyle shadowStyle)
    {
        GUI.Label(rect, text, style);
    }

    public static bool DrawButton(Rect rect, string label, bool active)
    {
        bool enabled = GUI.enabled;
        bool hover = enabled && rect.Contains(Event.current.mousePosition);
        Color fill = enabled
            ? (active ? new Color(0.10f, 0.26f, 0.20f, 0.96f) : new Color(0.06f, 0.12f, 0.13f, 0.94f))
            : new Color(0.05f, 0.06f, 0.06f, 0.72f);
        Color border = active ? new Color(GeneGreen.r, GeneGreen.g, GeneGreen.b, 0.78f) : Border;
        if (hover)
            border = BorderHot;

        DrawBox(rect, fill, border);
        if (active)
            DrawAccentBar(rect, GeneGreen);

        if (!string.IsNullOrEmpty(label))
        {
            GUIStyle textStyle = BuildTitleStyle(13, TextAnchor.MiddleCenter);
            textStyle.normal.textColor = enabled ? (active ? GeneGreen : Text) : MutedText;
            DrawText(rect, label, textStyle, null);
        }

        return TryConsumeClick(rect, enabled);
    }

    public static bool DrawCloseButton(Rect rect)
    {
        return DrawButton(rect, "X", false);
    }

    public static bool DrawTransparentClick(Rect rect)
    {
        return TryConsumeClick(rect, GUI.enabled);
    }

    /// <summary>手动检测左键点击，不用 GUI.Button，避免与自绘文字叠出重影。</summary>
    static bool TryConsumeClick(Rect rect, bool enabled)
    {
        if (!enabled)
            return false;

        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || !rect.Contains(e.mousePosition))
            return false;

        e.Use();
        return true;
    }

    public static void DrawModalShell(Rect rect, string title, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        DrawPanel(rect);
        DrawGrid(rect, 24f);

        GUIStyle titleStyle = new GUIStyle(labelStyle);
        titleStyle.fontSize = Mathf.Max(18, labelStyle.fontSize + 3);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Text;

        DrawText(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 26f), title, titleStyle, shadowStyle);
        DrawBox(new Rect(rect.x + 16f, rect.y + 43f, rect.width - 32f, 1f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.32f), Color.clear);
    }

    public static bool DrawNavTab(Rect rect, string index, string title, bool selected, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        bool clicked = DrawButton(rect, "", selected);
        Color accent = selected ? GeneGreen : MutedText;

        GUIStyle indexStyle = new GUIStyle(labelStyle);
        indexStyle.fontSize = 11;
        indexStyle.fontStyle = FontStyle.Bold;
        indexStyle.normal.textColor = accent;
        indexStyle.alignment = TextAnchor.UpperLeft;

        GUIStyle titleStyle = new GUIStyle(labelStyle);
        titleStyle.fontSize = 15;
        titleStyle.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        titleStyle.normal.textColor = selected ? Text : MutedText;
        titleStyle.alignment = TextAnchor.UpperLeft;

        DrawText(new Rect(rect.x + 10f, rect.y + 7f, rect.width - 20f, 16f), index, indexStyle, shadowStyle);
        DrawText(new Rect(rect.x + 10f, rect.y + 25f, rect.width - 20f, rect.height - 28f), title, titleStyle, shadowStyle);
        return clicked;
    }

    public static void DrawStatusPanel(Rect rect, string[] lines, GUIStyle style, GUIStyle shadowStyle)
    {
        DrawPanel(rect);
        DrawGrid(rect, 24f);

        float y = rect.y + 10f;
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i]))
            {
                y += 8f;
                continue;
            }

            Rect lineRect = new Rect(rect.x + 14f, y, rect.width - 28f, 22f);
            DrawText(lineRect, lines[i], style, shadowStyle);
            y += 22f;
        }
    }
}
