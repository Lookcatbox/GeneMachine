// DisplaySettings.cs - 画面设置：帧率上限，PlayerPrefs 持久化
using System.Threading;
using UnityEngine;

/// <summary>画面相关玩家设置；启动与修改时调用 <see cref="Apply"/> 生效。</summary>
public static class DisplaySettings
{
    const string PlayerPrefsKey = "GeneMachine.TargetFpsCap";

    static int currentFpsCap = SimulationConfig.DefaultTargetFpsCap;
    static bool loaded;

    /// <summary>当前帧率上限；-1 表示无限制。</summary>
    public static int CurrentFpsCap => currentFpsCap;

    public static void Load()
    {
        currentFpsCap = PlayerPrefs.GetInt(PlayerPrefsKey, SimulationConfig.DefaultTargetFpsCap);
        if (!IsValidFpsCap(currentFpsCap))
            currentFpsCap = SimulationConfig.DefaultTargetFpsCap;
        loaded = true;
    }

    public static void LoadAndApply()
    {
        Load();
        Apply();
    }

    /// <summary>设置帧率上限并保存；-1 为无限制。</summary>
    public static void SetFpsCap(int fpsCap)
    {
        if (!IsValidFpsCap(fpsCap))
            return;

        if (!loaded)
            Load();

        if (currentFpsCap == fpsCap)
            return;

        currentFpsCap = fpsCap;
        PlayerPrefs.SetInt(PlayerPrefsKey, fpsCap);
        PlayerPrefs.Save();
        Apply();
    }

    /// <summary>关闭垂直同步并按配置设置 <see cref="Application.targetFrameRate"/>。</summary>
    public static void Apply()
    {
        if (!loaded)
            Load();

        // vSync 开启时 Unity 会忽略 targetFrameRate；项目 Ultra 档位默认 vSync=1，需每帧压回 0
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = currentFpsCap;
        DisplaySettingsApplicator.EnsureExists();
    }

    /// <summary>帧末补等待，使上限在编辑器与 vSync 被改回时仍生效。</summary>
    public static void PaceFrameAfterRender(float frameStartRealtime)
    {
        if (!SimulationConfig.EnforceSoftwareFrameCap || currentFpsCap <= 0)
            return;

        float budget = 1f / currentFpsCap;
        float used = Time.realtimeSinceStartup - frameStartRealtime;
        float remain = budget - used;
        if (remain < 0.0005f)
            return;

        int sleepMs = Mathf.Clamp(Mathf.RoundToInt(remain * 1000f), 1, 1000);
        Thread.Sleep(sleepMs);
    }

    public static bool IsInfinity(int fpsCap)
    {
        return fpsCap == SimulationConfig.TargetFpsCapInfinity;
    }

    public static string GetFpsCapLabel(int fpsCap)
    {
        if (IsInfinity(fpsCap))
            return SimulationConfig.TargetFpsCapInfinityLabel;
        return fpsCap.ToString();
    }

    static bool IsValidFpsCap(int fpsCap)
    {
        if (IsInfinity(fpsCap))
            return true;

        int[] presets = SimulationConfig.TargetFpsCapPresets;
        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i] == fpsCap)
                return true;
        }
        return false;
    }
}
