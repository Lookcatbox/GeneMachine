// DisplaySettingsApplicator.cs - 每帧确保帧率上限生效（vSync 关闭 + 可选软件 pacing）
using System.Collections;
using UnityEngine;

/// <summary>常驻组件：防止画质档位把 vSync 改回 1，并在帧末补足等待时间。</summary>
public sealed class DisplaySettingsApplicator : MonoBehaviour
{
    static DisplaySettingsApplicator instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        var go = new GameObject(nameof(DisplaySettingsApplicator));
        instance = go.AddComponent<DisplaySettingsApplicator>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        DisplaySettings.LoadAndApply();
        StartCoroutine(FramePacingLoop());
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void LateUpdate()
    {
        DisplaySettings.Apply();
    }

    IEnumerator FramePacingLoop()
    {
        while (true)
        {
            float frameStart = Time.realtimeSinceStartup;
            yield return new WaitForEndOfFrame();
            DisplaySettings.PaceFrameAfterRender(frameStart);
        }
    }
}
