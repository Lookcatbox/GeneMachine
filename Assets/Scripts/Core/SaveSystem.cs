using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveSlotMeta
{
    public string savedAt;
    public double playSeconds;
}

public class SaveSlotInfo
{
    public bool HasData;
    public string SavedAt;
    public double PlaySeconds;
    public Texture2D Screenshot;
}

public struct ViewSaveData
{
    public Vector3 CameraPosition;
    public float CameraOrthoSize;
    public int BaseViewMode;
    public int OverlayViewMode;
    public bool NormalLightingEnabled;
    public bool PlayerPanelExpanded;
    public int ActivePlayerPanelTabIndex;
    public int SimulationSpeed;
}

public static class SaveSystem
{
    private const int SaveVersion = 1;
    private static int pendingLoadSlot = -1;

    public static bool HasPendingLoadSlot => pendingLoadSlot >= 0;

    public static void SetPendingLoadSlot(int slot)
    {
        pendingLoadSlot = slot;
    }

    public static void ClearPendingLoadSlot()
    {
        pendingLoadSlot = -1;
    }

    public static int ConsumePendingLoadSlot()
    {
        int slot = pendingLoadSlot;
        pendingLoadSlot = -1;
        return slot;
    }

    public static SaveSlotInfo[] LoadAllSlotInfos()
    {
        int count = Mathf.Max(1, SimulationConfig.SaveSlotCount);
        SaveSlotInfo[] infos = new SaveSlotInfo[count];
        for (int i = 0; i < count; i++)
        {
            infos[i] = LoadSlotInfo(i);
        }
        return infos;
    }

    public static string FormatPlayTime(double seconds)
    {
        if (seconds < 0) seconds = 0;
        TimeSpan span = TimeSpan.FromSeconds(seconds);
        return string.Format("{0:D2}:{1:D2}:{2:D2}",
            (int)span.TotalHours,
            span.Minutes,
            span.Seconds);
    }

    public static void SaveToSlot(int slot, MainManager manager)
    {
        if (slot < 0)
            return;

        if (SimulationCore.EnvirData == null || SimulationCore.AllCells == null)
            return;

        string dataPath = GetSlotDataPath(slot);
        string metaPath = GetSlotMetaPath(slot);
        string screenshotPath = GetSlotScreenshotPath(slot);

        using (FileStream stream = new FileStream(dataPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            WriteSaveData(writer, manager);
        }

        SaveSlotMeta meta = new SaveSlotMeta
        {
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            playSeconds = SimulationCore.GetPlaySeconds()
        };
        File.WriteAllText(metaPath, JsonUtility.ToJson(meta));

        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        if (screenshot != null)
        {
            byte[] png = screenshot.EncodeToPNG();
            File.WriteAllBytes(screenshotPath, png);
            UnityEngine.Object.Destroy(screenshot);
        }
    }

    public static bool LoadSlotIntoSimulation(int slot, MainManager manager)
    {
        string dataPath = GetSlotDataPath(slot);
        if (!File.Exists(dataPath))
            return false;

        using (FileStream stream = new FileStream(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            return ReadSaveData(reader, manager);
        }
    }

    private static SaveSlotInfo LoadSlotInfo(int slot)
    {
        SaveSlotInfo info = new SaveSlotInfo();
        string dataPath = GetSlotDataPath(slot);
        string metaPath = GetSlotMetaPath(slot);
        string screenshotPath = GetSlotScreenshotPath(slot);

        if (!File.Exists(dataPath) || !File.Exists(metaPath))
        {
            info.HasData = false;
            return info;
        }

        info.HasData = true;
        try
        {
            string json = File.ReadAllText(metaPath);
            SaveSlotMeta meta = JsonUtility.FromJson<SaveSlotMeta>(json);
            if (meta != null)
            {
                info.SavedAt = meta.savedAt;
                info.PlaySeconds = meta.playSeconds;
            }
        }
        catch (Exception)
        {
            info.SavedAt = "";
            info.PlaySeconds = 0;
        }

        if (string.IsNullOrEmpty(info.SavedAt) && File.Exists(dataPath))
        {
            info.SavedAt = File.GetLastWriteTime(dataPath).ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (File.Exists(screenshotPath))
        {
            byte[] bytes = File.ReadAllBytes(screenshotPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.LoadImage(bytes);
            info.Screenshot = tex;
        }

        return info;
    }

    private static string EnsureSaveDirectory()
    {
        string dir = Path.Combine(Application.persistentDataPath, "Saves");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    private static string GetSlotDataPath(int slot)
    {
        return Path.Combine(EnsureSaveDirectory(), string.Format("slot_{0}.bin", slot));
    }

    private static string GetSlotMetaPath(int slot)
    {
        return Path.Combine(EnsureSaveDirectory(), string.Format("slot_{0}.json", slot));
    }

    private static string GetSlotScreenshotPath(int slot)
    {
        return Path.Combine(EnsureSaveDirectory(), string.Format("slot_{0}.png", slot));
    }

    private static void WriteSaveData(BinaryWriter writer, MainManager manager)
    {
        int size = SimulationConfig.EnvirSize;
        Envir[,] envirData = SimulationCore.EnvirData;
        List<Cell> cells = SimulationCore.AllCells;
        writer.Write(SaveVersion);
        writer.Write(size);
        writer.Write(SimulationConfig.WorldSeed);
        writer.Write(SimulationCore.totalSteps);
        writer.Write(SimulationCore.GetResearchPoints());
        writer.Write(SimulationCore.GetResearchStepCounter());
        writer.Write(SimulationCore.TempLowUpgradeLevel);
        writer.Write(SimulationCore.TempHighUpgradeLevel);
        writer.Write(SimulationCore.speedMultiplier);
        writer.Write(SimulationCore.GetPlaySeconds());

        ViewSaveData viewData = manager != null ? manager.CaptureViewState() : new ViewSaveData();
        WriteViewData(writer, viewData);
        WriteDeviceState(writer, DeviceSystem.CaptureSaveState());

        for (int y = 1; y <= size; y++)
        {
            for (int x = 1; x <= size; x++)
            {
                Envir env = envirData[x, y];
                writer.Write(env.Height);
                writer.Write(env.Topography);
                writer.Write(env.Temp);
                writer.Write(env.Light);
                writer.Write(env.MaxCellNum);
            }
        }

        writer.Write(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            writer.Write(cell.px);
            writer.Write(cell.py);
            writer.Write(cell.energy);
            writer.Write(cell.priority);
            writer.Write(cell.alive);
            writer.Write(cell.isPlayer);
            WriteGeneList(writer, cell.MainGeneList);
            WriteGeneList(writer, cell.SubGeneList);
        }
    }

    private static bool ReadSaveData(BinaryReader reader, MainManager manager)
    {
        int version = reader.ReadInt32();
        if (version != SaveVersion)
            return false;

        int size = reader.ReadInt32();
        if (size != SimulationConfig.EnvirSize)
            return false;

        int worldSeed = reader.ReadInt32();
        long totalSteps = reader.ReadInt64();
        long researchPoints = reader.ReadInt64();
        long researchStepCounter = reader.ReadInt64();
        int tempLow = reader.ReadInt32();
        int tempHigh = reader.ReadInt32();
        int speedMultiplier = reader.ReadInt32();
        double playSeconds = reader.ReadDouble();

        SimulationConfig.WorldSeed = worldSeed;

        ViewSaveData viewData = ReadViewData(reader);
        DeviceSystemSaveData deviceState = ReadDeviceState(reader);

        Envir[,] envirData = new Envir[size + 2, size + 2];
        for (int y = 1; y <= size; y++)
        {
            for (int x = 1; x <= size; x++)
            {
                int height = reader.ReadInt32();
                int topo = reader.ReadInt32();
                float temp = reader.ReadSingle();
                int light = reader.ReadInt32();
                int maxCellNum = reader.ReadInt32();

                Envir env = new Envir(maxCellNum);
                env.Height = height;
                env.Topography = topo;
                env.Temp = temp;
                env.Light = light;
                envirData[x, y] = env;
            }
        }

        int cellCount = reader.ReadInt32();
        List<Cell> allCells = new List<Cell>(Mathf.Max(0, cellCount));
        for (int i = 0; i < cellCount; i++)
        {
            int px = reader.ReadInt32();
            int py = reader.ReadInt32();
            int energy = reader.ReadInt32();
            int priority = reader.ReadInt32();
            bool alive = reader.ReadBoolean();
            bool isPlayer = reader.ReadBoolean();

            Cell cell = new Cell(px, py, isPlayer);
            cell.energy = energy;
            cell.priority = priority;
            cell.alive = alive;
            ReadGeneList(reader, cell.MainGeneList);
            ReadGeneList(reader, cell.SubGeneList);

            if (alive)
            {
                Envir env = envirData[px, py];
                if (env != null && env.AddCell(cell))
                    allCells.Add(cell);
            }
        }

        SimulationCore.StopCalculation();
        SimulationCore.EnvirData = envirData;
        SimulationCore.AllCells = allCells;
        SimulationCore.Rng = new System.Random(worldSeed);
        SimulationCore._rngSeedCounter = worldSeed + 1000;
        SimulationCore.totalSteps = totalSteps;
        SimulationCore.aliveCellCount = allCells.Count;
        SimulationCore.TempLowUpgradeLevel = tempLow;
        SimulationCore.TempHighUpgradeLevel = tempHigh;
        SimulationCore.SetResearchPoints(researchPoints);
        SimulationCore.SetResearchStepCounter(researchStepCounter);
        SimulationCore.SetSpeedMultiplier(speedMultiplier);
        SimulationCore.SetPlaySeconds(playSeconds);

        DeviceSystem.ApplySaveState(deviceState);
        if (manager != null)
            manager.ApplyViewState(viewData);

        return true;
    }

    private static void WriteViewData(BinaryWriter writer, ViewSaveData data)
    {
        writer.Write(data.CameraPosition.x);
        writer.Write(data.CameraPosition.y);
        writer.Write(data.CameraPosition.z);
        writer.Write(data.CameraOrthoSize);
        writer.Write(data.BaseViewMode);
        writer.Write(data.OverlayViewMode);
        writer.Write(data.NormalLightingEnabled);
        writer.Write(data.PlayerPanelExpanded);
        writer.Write(data.ActivePlayerPanelTabIndex);
        writer.Write(data.SimulationSpeed);
    }

    private static ViewSaveData ReadViewData(BinaryReader reader)
    {
        ViewSaveData data = new ViewSaveData();
        data.CameraPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        data.CameraOrthoSize = reader.ReadSingle();
        data.BaseViewMode = reader.ReadInt32();
        data.OverlayViewMode = reader.ReadInt32();
        data.NormalLightingEnabled = reader.ReadBoolean();
        data.PlayerPanelExpanded = reader.ReadBoolean();
        data.ActivePlayerPanelTabIndex = reader.ReadInt32();
        data.SimulationSpeed = reader.ReadInt32();
        return data;
    }

    private static void WriteDeviceState(BinaryWriter writer, DeviceSystemSaveData data)
    {
        writer.Write(data.NextDeviceId);
        WriteDeviceInstances(writer, data.Devices);
        WriteDeviceEntries(writer, data.Inventory);
        WriteDeviceEntries(writer, data.CraftCounts);
    }

    private static DeviceSystemSaveData ReadDeviceState(BinaryReader reader)
    {
        DeviceSystemSaveData data = new DeviceSystemSaveData();
        data.NextDeviceId = reader.ReadInt32();
        data.Devices = ReadDeviceInstances(reader);
        data.Inventory = ReadDeviceEntries(reader);
        data.CraftCounts = ReadDeviceEntries(reader);
        return data;
    }

    private static void WriteDeviceInstances(BinaryWriter writer, DeviceInstance[] instances)
    {
        int count = instances != null ? instances.Length : 0;
        writer.Write(count);
        for (int i = 0; i < count; i++)
        {
            writer.Write(instances[i].Id);
            writer.Write(instances[i].TypeId);
            writer.Write(instances[i].X);
            writer.Write(instances[i].Y);
        }
    }

    private static DeviceInstance[] ReadDeviceInstances(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count <= 0)
            return new DeviceInstance[0];
        DeviceInstance[] instances = new DeviceInstance[count];
        for (int i = 0; i < count; i++)
        {
            instances[i] = new DeviceInstance
            {
                Id = reader.ReadInt32(),
                TypeId = reader.ReadInt32(),
                X = reader.ReadInt32(),
                Y = reader.ReadInt32()
            };
        }
        return instances;
    }

    private static void WriteDeviceEntries(BinaryWriter writer, DeviceInventoryEntry[] entries)
    {
        int count = entries != null ? entries.Length : 0;
        writer.Write(count);
        for (int i = 0; i < count; i++)
        {
            writer.Write(entries[i].TypeId);
            writer.Write(entries[i].Count);
        }
    }

    private static DeviceInventoryEntry[] ReadDeviceEntries(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count <= 0)
            return new DeviceInventoryEntry[0];
        DeviceInventoryEntry[] entries = new DeviceInventoryEntry[count];
        for (int i = 0; i < count; i++)
        {
            entries[i].TypeId = reader.ReadInt32();
            entries[i].Count = reader.ReadInt32();
        }
        return entries;
    }

    private static void WriteGeneList(BinaryWriter writer, Gene[] list)
    {
        int length = list != null ? list.Length : 0;
        writer.Write(length);
        for (int i = 0; i < length; i++)
        {
            Gene gene = list[i];
            writer.Write(gene.baseId);
            writer.Write(gene.energyCost);
            writer.Write(GetGeneUpgradeHash(gene));
        }
    }

    private static void ReadGeneList(BinaryReader reader, Gene[] list)
    {
        int length = reader.ReadInt32();
        int max = list != null ? list.Length : 0;
        for (int i = 0; i < length; i++)
        {
            int baseId = reader.ReadInt32();
            int energyCost = reader.ReadInt32();
            int upgradeHash = reader.ReadInt32();
            if (i < max)
                list[i] = new Gene(baseId, energyCost, upgradeHash);
        }
    }

    private static int GetGeneUpgradeHash(Gene gene)
    {
        if (gene.baseId == 0)
            return 0;
        if (gene.hashId <= SimulationConfig.GeneNum)
            return 0;
        return gene.hashId & 0xFFFFFF;
    }
 }
