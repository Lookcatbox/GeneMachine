using UnityEngine;

public class DevicePlayerPanelTab : PlayerPanelTabPage
{
    private int selectedTypeId = -1;

    public DevicePlayerPanelTab() : base("装置")
    {
    }

    public override void Draw(Rect contentRect, GUIStyle labelStyle, GUIStyle shadowStyle, PlayerPanelContext context)
    {
        GUIStyle contentStyle = BuildWrappedStyle(labelStyle);
        GUIStyle contentShadowStyle = BuildWrappedStyle(shadowStyle);

        float padding = 10f;
        float listHeight = contentRect.height * SimulationConfig.DevicePanelListRatio;
        Rect listRect = new Rect(contentRect.x + padding, contentRect.y + padding, contentRect.width - padding * 2f, listHeight - padding);
        Rect infoRect = new Rect(contentRect.x + padding, listRect.yMax + padding, contentRect.width - padding * 2f, contentRect.yMax - listRect.yMax - padding);

        GUI.Box(listRect, "");
        GUI.Box(infoRect, "");

        DrawDeviceList(listRect, labelStyle, shadowStyle);
        DrawDeviceInfo(infoRect, contentStyle, contentShadowStyle);
    }

    void DrawDeviceList(Rect rect, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        var types = DeviceSystem.GetDeviceTypes();
        float rowHeight = SimulationConfig.DeviceIconSize + 8f;
        float iconSize = SimulationConfig.DeviceIconSize;
        float y = rect.y + 6f;

        for (int i = 0; i < types.Count; i++)
        {
            DeviceType type = types[i];
            Rect rowRect = new Rect(rect.x + 6f, y, rect.width - 12f, rowHeight);
            bool selected = type.TypeId == selectedTypeId;
            GUI.backgroundColor = selected ? new Color(0.24f, 0.34f, 0.52f) : new Color(0.22f, 0.22f, 0.22f);
            if (GUI.Button(rowRect, ""))
                selectedTypeId = type.TypeId;
            GUI.backgroundColor = Color.white;

            Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + (rowHeight - iconSize) * 0.5f, iconSize, iconSize);
            if (type.Icon != null)
                GUI.DrawTexture(iconRect, type.Icon, ScaleMode.StretchToFill);

            float textX = iconRect.xMax + 8f;
            GUI.Label(new Rect(textX + 1f, rowRect.y + 6f, rowRect.width - 80f, rowHeight), type.Name, shadowStyle);
            GUI.Label(new Rect(textX, rowRect.y + 5f, rowRect.width - 80f, rowHeight), type.Name, labelStyle);

            int count = DeviceSystem.GetDeviceCount(type.TypeId);
            string countText = count.ToString();
            Vector2 size = labelStyle.CalcSize(new GUIContent(countText));
            Rect countRect = new Rect(rowRect.xMax - size.x - 10f, rowRect.y + 6f, size.x + 2f, rowHeight);
            GUI.Label(new Rect(countRect.x + 1f, countRect.y + 1f, countRect.width, countRect.height), countText, shadowStyle);
            GUI.Label(countRect, countText, labelStyle);

            y += rowHeight + 4f;
            if (y > rect.yMax - rowHeight)
                break;
        }
    }

    void DrawDeviceInfo(Rect rect, GUIStyle labelStyle, GUIStyle shadowStyle)
    {
        if (selectedTypeId < 0)
        {
            DrawTextBlock(rect, "请选择一个装置类型查看详情。", labelStyle, shadowStyle);
            return;
        }

        DeviceType type = DeviceSystem.GetDeviceType(selectedTypeId);
        if (type == null)
        {
            DrawTextBlock(rect, "装置类型不存在。", labelStyle, shadowStyle);
            return;
        }

        string craftText = type.Craftable ? "可制作" : "不可制作";
        int craftCost = DeviceSystem.GetCraftCost(type.TypeId);
        int crafted = DeviceSystem.GetCraftCount(type.TypeId);
        int craftMax = DeviceSystem.GetCraftMax(type.TypeId);
        string costText = "";
        if (type.Craftable)
        {
            if (craftMax > 0)
            {
                costText = string.Format("制作消耗: {0}  已制作: {1}/{2}", craftCost, crafted, craftMax);
                if (crafted >= craftMax)
                    costText += "\n已达制造上限";
            }
            else
            {
                costText = string.Format("制作消耗: {0}  已制作: {1}", craftCost, crafted);
            }
        }
        string infoText = string.Format(
            "名称: {0}\n类型ID: {1}\n数量: {2}\n标签: {3}\n{4}\n\n{5}",
            type.Name,
            type.TypeId,
            DeviceSystem.GetDeviceCount(type.TypeId),
            craftText,
            costText,
            type.Description);

        float buttonHeight = 28f;
        float buttonWidth = (rect.width - 12f) * 0.5f;
        float buttonY = rect.yMax - buttonHeight - 8f;
        DrawTextBlock(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - buttonHeight - 14f), infoText, labelStyle, shadowStyle);

        Rect placeRect = new Rect(rect.x + 6f, buttonY, buttonWidth, buttonHeight);
        Rect craftRect = new Rect(placeRect.xMax + 12f, buttonY, buttonWidth, buttonHeight);

        GUI.enabled = DeviceSystem.GetDeviceCount(type.TypeId) > 0;
        if (GUI.Button(placeRect, "放置"))
            DeviceSystem.BeginPlacement(type.TypeId);

        bool canCraft = DeviceSystem.CanCraftDevice(type.TypeId);
        GUI.enabled = canCraft;
        if (GUI.Button(craftRect, "制作"))
            DeviceSystem.TryCraftDevice(type.TypeId);
        GUI.enabled = true;
    }
}
