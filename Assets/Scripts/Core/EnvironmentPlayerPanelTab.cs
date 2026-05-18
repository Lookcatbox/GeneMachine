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
            ? string.Format("当前锁定坐标: ({0}, {1})。再次点击同一格可取消锁定。", context.LockedEnvironmentX, context.LockedEnvironmentY)
            : "当前未锁定，左键点击环境格可固定显示。";

        string infoText = string.Format(
            "显示来源: {0}\n坐标: ({1}, {2})\n地形类型: {3} ({4})\n高度 Height: {5}\n温度 Temp: {6}\n光照 Light: {7}\n当前细胞数 CellNum: {8}\n最大容量 MaxCellNum: {9}\nCellList 长度: {10}\n玩家细胞数: {11}\nNPC细胞数: {12}\n最高优先级: {13}\n\n{14}",
            sourceText,
            context.DisplayEnvironmentX,
            context.DisplayEnvironmentY,
            GetTopographyName(env.Topography),
            env.Topography,
            env.Height,
            tempC.ToString("F1"),
            env.Light,
            env.CellNum,
            env.MaxCellNum,
            env.CellList != null ? env.CellList.Length - 1 : 0,
            playerCount,
            npcCount,
            maxPriority,
            lockHint);

        DrawTextBlock(innerRect, infoText, contentStyle, contentShadowStyle);
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