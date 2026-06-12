// OverlayBufferPool.cs - 温度/光照/化学叠加层的双缓冲池，供主线程按步提交、按帧显示
using UnityEngine;

/// <summary>
/// 维护多份叠加层 GPU 纹理快照；写入与显示分离，模拟步推进时才写入新快照。
/// </summary>
public sealed class OverlayBufferPool : System.IDisposable
{
    /// <summary>单份缓冲：CPU 像素 + 源纹理 + 合成 RenderTexture。</summary>
    public sealed class Slot
    {
        public Color32[] TempLightPixels;
        public Color32[] ChemicalPixels;
        public Texture2D TempLightTexture;
        public Texture2D ChemicalTexture;
        public RenderTexture CompositeTexture;
        public long CapturedStep = -1;
        public CellRenderer.OverlayViewMode ViewMode;
        public int ChemicalMask;
        public bool Ready;
    }

    Slot[] slots;
    int displayIndex = -1;
    int nextWriteIndex;

    public int TempLightTextureSize { get; private set; }
    public int ChemicalTextureSize { get; private set; }
    public bool HasDisplayBuffer => displayIndex >= 0 && slots != null && slots[displayIndex].Ready;
    public RenderTexture DisplayComposite => HasDisplayBuffer ? slots[displayIndex].CompositeTexture : null;
    public long DisplayStep => HasDisplayBuffer ? slots[displayIndex].CapturedStep : -1;

    /// <summary>按配置创建缓冲池；poolSize 至少为 2（乒乓写入）。</summary>
    public void Initialize(int poolSize, int tempLightSize, int chemicalSize)
    {
        DisposeSlots();
        TempLightTextureSize = Mathf.Max(1, tempLightSize);
        ChemicalTextureSize = Mathf.Max(1, chemicalSize);
        int count = Mathf.Max(2, poolSize);
        slots = new Slot[count];
        for (int i = 0; i < count; i++)
            slots[i] = CreateSlot();
        displayIndex = -1;
        nextWriteIndex = 0;
    }

    Slot CreateSlot()
    {
        var slot = new Slot
        {
            TempLightPixels = new Color32[TempLightTextureSize * TempLightTextureSize],
            ChemicalPixels = new Color32[ChemicalTextureSize * ChemicalTextureSize],
            TempLightTexture = new Texture2D(TempLightTextureSize, TempLightTextureSize, TextureFormat.RGBA32, false),
            ChemicalTexture = new Texture2D(ChemicalTextureSize, ChemicalTextureSize, TextureFormat.RGBA32, false)
        };
        slot.TempLightTexture.filterMode = FilterMode.Bilinear;
        slot.TempLightTexture.wrapMode = TextureWrapMode.Clamp;
        slot.ChemicalTexture.filterMode = FilterMode.Bilinear;
        slot.ChemicalTexture.wrapMode = TextureWrapMode.Clamp;

        slot.CompositeTexture = new RenderTexture(TempLightTextureSize, TempLightTextureSize, 0, RenderTextureFormat.ARGB32);
        slot.CompositeTexture.filterMode = FilterMode.Bilinear;
        slot.CompositeTexture.wrapMode = TextureWrapMode.Clamp;
        slot.CompositeTexture.Create();
        return slot;
    }

    /// <summary>取得下一写入槽（不切换显示）。</summary>
    public Slot AcquireWriteSlot()
    {
        if (slots == null || slots.Length == 0)
            return null;
        return slots[nextWriteIndex];
    }

    /// <summary>写入完成后发布为显示缓冲，并轮转写入索引。</summary>
    public void CommitWrite(Slot slot, long step, CellRenderer.OverlayViewMode viewMode, int chemicalMask)
    {
        if (slot == null || slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (!ReferenceEquals(slots[i], slot))
                continue;

            slot.CapturedStep = step;
            slot.ViewMode = viewMode;
            slot.ChemicalMask = chemicalMask;
            slot.Ready = true;
            displayIndex = i;
            nextWriteIndex = (i + 1) % slots.Length;
            return;
        }
    }

    public void Dispose()
    {
        DisposeSlots();
    }

    void DisposeSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null)
                continue;

            if (slot.CompositeTexture != null)
            {
                slot.CompositeTexture.Release();
                Object.Destroy(slot.CompositeTexture);
            }
            if (slot.TempLightTexture != null)
                Object.Destroy(slot.TempLightTexture);
            if (slot.ChemicalTexture != null)
                Object.Destroy(slot.ChemicalTexture);
        }

        slots = null;
        displayIndex = -1;
        nextWriteIndex = 0;
    }
}
