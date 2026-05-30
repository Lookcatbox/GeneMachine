// TerrainGenerator.cs - 地形生成器：柏林噪声 + 水源寻径河流 + 湖泊
using System;
using System.Collections.Generic;
using UnityEngine;

public static class TerrainGenerator
{
    private static readonly int[] DX8 = { -1, -1, -1, 0, 0, 1, 1, 1 };
    private static readonly int[] DY8 = { -1, 0, 1, -1, 1, -1, 0, 1 };

    /// <summary>
    /// 生成完整地形：高度图 → 水源河流/湖泊 → 写入Envir
    /// </summary>
    public static void Generate(Envir[,] envirData, int seed)
    {
        int size = SimulationConfig.EnvirSize;
        float[,] heightMap = new float[size + 2, size + 2];

        GeneratePerlinHeight(heightMap, size, seed);

        // 河流粗细数组，湖泊标记数组
        int[,] riverWidth = new int[size + 2, size + 2];
        bool[,] isLake = new bool[size + 2, size + 2];
        float[,] lakeLevel = new float[size + 2, size + 2]; // 湖泊水位

        // TODO: 河流生成暂时禁用
        // GenerateRiversAndLakes(heightMap, riverWidth, isLake, lakeLevel, size, seed);

        for (int x = 1; x <= size; x++)
        {
            for (int y = 1; y <= size; y++)
            {
                int minHeight = Mathf.RoundToInt(SimulationConfig.AltitudeMin);
                int maxHeight = Mathf.RoundToInt(SimulationConfig.AltitudeMax);
                int h = Mathf.Clamp(Mathf.RoundToInt(heightMap[x, y]), minHeight, maxHeight);
                envirData[x, y].Height = h;

                if (isLake[x, y])
                    envirData[x, y].Topography = 4; // 湖泊
                else if (h < SimulationConfig.AltitudeThreshold)
                    envirData[x, y].Topography = 0; // 海洋
                else if (riverWidth[x, y] > 0)
                    envirData[x, y].Topography = 3; // 河流
                else if (h < SimulationConfig.AltitudeBeach)
                    envirData[x, y].Topography = 2; // 沙滩
                else
                    envirData[x, y].Topography = 1; // 陆地
            }
        }
    }

    // ======== 柏林噪声高度图 ========

    private static void GeneratePerlinHeight(float[,] map, int size, int seed)
    {
        float baseOffsetX = ((seed * 17 + 31) % 10000) * 1.3f;
        float baseOffsetY = ((seed * 53 + 97) % 10000) * 1.3f;

        float baseScale = SimulationConfig.NoiseBaseScale;
        int layerCount = SimulationConfig.NoiseLayerCount;
        float frequencyMultiplier = SimulationConfig.NoiseFrequencyMultiplier;
        float weightDecay = SimulationConfig.NoiseWeightDecay;

        float[] layerOffsetX = new float[layerCount];
        float[] layerOffsetY = new float[layerCount];
        float maxWeight = 0f;
        float weight = 1f;

        for (int layer = 0; layer < layerCount; layer++)
        {
            layerOffsetX[layer] = baseOffsetX + 137.21f * (layer + 1);
            layerOffsetY[layer] = baseOffsetY + 263.17f * (layer + 1);
            maxWeight += weight;
            weight *= weightDecay;
        }

        for (int x = 1; x <= size; x++)
        {
            for (int y = 1; y <= size; y++)
            {
                float value = 0f;
                float normalizedX = size > 1 ? (x - 1f) / (size - 1f) : 0f;
                float normalizedY = size > 1 ? (y - 1f) / (size - 1f) : 0f;
                float frequency = 1f;
                float layerWeight = 1f;

                for (int layer = 0; layer < layerCount; layer++)
                {
                    float tilePeriod = Mathf.Max(0.0001f, (size - 1f) * baseScale * frequency);
                    float layerNoise = SampleTiledPerlin(
                        normalizedX,
                        normalizedY,
                        tilePeriod,
                        layerOffsetX[layer],
                        layerOffsetY[layer]);
                    value += layerNoise * layerWeight;
                    frequency *= frequencyMultiplier;
                    layerWeight *= weightDecay;
                }

                float t = Mathf.Clamp01(value / maxWeight);
                map[x, y] = Mathf.Lerp(SimulationConfig.AltitudeMin, SimulationConfig.AltitudeMax, t);
            }
        }
    }

    private static float SampleTiledPerlin(float normalizedX, float normalizedY,
        float tilePeriod, float offsetX, float offsetY)
    {
        float sampleX = normalizedX * tilePeriod;
        float sampleY = normalizedY * tilePeriod;

        float sample00 = Mathf.PerlinNoise(offsetX + sampleX, offsetY + sampleY);
        float sample10 = Mathf.PerlinNoise(offsetX + sampleX - tilePeriod, offsetY + sampleY);
        float sample01 = Mathf.PerlinNoise(offsetX + sampleX, offsetY + sampleY - tilePeriod);
        float sample11 = Mathf.PerlinNoise(offsetX + sampleX - tilePeriod, offsetY + sampleY - tilePeriod);

        float blendBottom = Mathf.Lerp(sample00, sample10, normalizedX);
        float blendTop = Mathf.Lerp(sample01, sample11, normalizedX);
        return Mathf.Lerp(blendBottom, blendTop, normalizedY);
    }

    // ======== 水源寻径河流 + 湖泊生成 ========

    private struct RiverSource
    {
        public int x, y, flow;
        public bool fromLake; // 湖泊溢出产生的水源跳过最短长度检查
    }

    private static void GenerateRiversAndLakes(float[,] map, int[,] riverWidth,
        bool[,] isLake, float[,] lakeLevel, int size, int seed)
    {
        var rng = new System.Random(seed + 77);
        int numSources = SimulationConfig.RiverSourceCount;

        int[,] flowCount = new int[size + 2, size + 2];
        int[,] lakeIdMap = new int[size + 2, size + 2];
        // 用代数标记代替每次分配 visited 数组
        int[,] visitedGen = new int[size + 2, size + 2];
        int currentGen = 0;
        int nextLakeId = 1;

        // 每个湖泊的溢出口坐标
        List<int> lakeSpillXList = new List<int> { 0 }; // index 0 unused
        List<int> lakeSpillYList = new List<int> { 0 };

        Queue<RiverSource> sourceQueue = new Queue<RiverSource>();

        // 生成初始随机水源（只在陆地高处）
        for (int s = 0; s < numSources; s++)
        {
            int sx, sy;
            int attempts = 0;
            do
            {
                sx = rng.Next(2, size);
                sy = rng.Next(2, size);
                attempts++;
            } while (map[sx, sy] < SimulationConfig.AltitudeThreshold + 20 && attempts < 500);
            if (attempts >= 500) continue;

            sourceQueue.Enqueue(new RiverSource { x = sx, y = sy, flow = 1, fromLake = false });
        }

        int maxIter = numSources * 5; // 防无限循环
        int processed = 0;

        while (sourceQueue.Count > 0 && processed < maxIter)
        {
            processed++;
            RiverSource src = sourceQueue.Dequeue();
            currentGen++;

            List<int> pathX = new List<int>(256);
            List<int> pathY = new List<int>(256);
            int cx = src.x, cy = src.y;
            bool reachedEnd = false;

            for (int step = 0; step < SimulationConfig.RiverMaxLength; step++)
            {
                if (cx < 1 || cx > size || cy < 1 || cy > size) break;
                if (visitedGen[cx, cy] == currentGen) break;
                visitedGen[cx, cy] = currentGen;
                pathX.Add(cx);
                pathY.Add(cy);

                float curH = map[cx, cy];

                // 到达海洋
                if (curH < SimulationConfig.AltitudeThreshold)
                {
                    reachedEnd = true;
                    break;
                }

                // 到达已有河流 → 汇入
                if (flowCount[cx, cy] > 0)
                {
                    reachedEnd = true;
                    break;
                }

                // 到达已有湖泊 → 流量从溢出口继续
                if (isLake[cx, cy] && lakeIdMap[cx, cy] > 0)
                {
                    int lid = lakeIdMap[cx, cy];
                    int spX = lakeSpillXList[lid];
                    int spY = lakeSpillYList[lid];
                    if (spX > 0)
                        sourceQueue.Enqueue(new RiverSource { x = spX, y = spY, flow = src.flow, fromLake = true });
                    reachedEnd = true;
                    break;
                }

                // 寻找8邻域中未访问的最低点
                float bestH = curH;
                int bx = -1, by = -1;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DX8[d];
                    int ny = cy + DY8[d];
                    if (nx < 1 || nx > size || ny < 1 || ny > size) continue;
                    if (visitedGen[nx, ny] == currentGen) continue;
                    if (map[nx, ny] < bestH)
                    {
                        bestH = map[nx, ny];
                        bx = nx;
                        by = ny;
                    }
                }

                if (bx < 0)
                {
                    // 极低点 → 找溢出口（未访问邻域中最低的，高度 ≥ curH）
                    float spillH = float.MaxValue;
                    int spillBx = -1, spillBy = -1;
                    for (int d = 0; d < 8; d++)
                    {
                        int nx = cx + DX8[d];
                        int ny = cy + DY8[d];
                        if (nx < 1 || nx > size || ny < 1 || ny > size) continue;
                        if (visitedGen[nx, ny] == currentGen) continue;
                        if (map[nx, ny] < spillH)
                        {
                            spillH = map[nx, ny];
                            spillBx = nx;
                            spillBy = ny;
                        }
                    }

                    // 创建湖泊
                    int lid = nextLakeId++;
                    lakeSpillXList.Add(spillBx);
                    lakeSpillYList.Add(spillBy);

                    if (spillBx >= 0)
                    {
                        // 湖面 = 极低点周围低于溢出口高度的连通区域（有限面积）
                        FillLakeBasin(map, isLake, lakeIdMap, lakeLevel, lid, cx, cy, spillH, size);
                        // 溢出口产生新水源，继承来源流量
                        sourceQueue.Enqueue(new RiverSource { x = spillBx, y = spillBy, flow = src.flow, fromLake = true });
                    }
                    else
                    {
                        // 所有邻域都已访问 → 只标记当前格为湖泊
                        isLake[cx, cy] = true;
                        lakeIdMap[cx, cy] = lid;
                        lakeLevel[cx, cy] = curH;
                    }

                    reachedEnd = true;
                    break;
                }

                cx = bx;
                cy = by;
            }

            if (!reachedEnd) continue;
            // 初始水源要求最短长度，湖泊溢出则不限
            if (pathX.Count < SimulationConfig.RiverMinLength && !src.fromLake) continue;

            // 将流量写入路径
            for (int i = 0; i < pathX.Count; i++)
                flowCount[pathX[i], pathY[i]] += src.flow;
        }

        // 根据累积流量涂河流宽度：基底宽度 + 汇入加粗
        for (int x = 1; x <= size; x++)
        {
            for (int y = 1; y <= size; y++)
            {
                if (flowCount[x, y] <= 0) continue;
                if (map[x, y] < SimulationConfig.AltitudeThreshold) continue;
                if (isLake[x, y]) continue;

                int width = SimulationConfig.RiverBaseWidth + flowCount[x, y] / SimulationConfig.RiverWidenPerFlow;
                PaintRiver(riverWidth, x, y, width, size);
            }
        }
    }

    /// <summary>
    /// 填充湖泊盆地：从极低点BFS扩展，仅标记高度 &lt; spillHeight 的连通区域（面积受地形约束）
    /// </summary>
    private static void FillLakeBasin(float[,] map, bool[,] isLake, int[,] lakeIdMap,
        float[,] lakeLevel, int lid, int sinkX, int sinkY, float spillHeight, int size)
    {
        Queue<int> queue = new Queue<int>(64);
        queue.Enqueue(sinkX * (size + 2) + sinkY);
        isLake[sinkX, sinkY] = true;
        lakeIdMap[sinkX, sinkY] = lid;
        lakeLevel[sinkX, sinkY] = spillHeight;

        while (queue.Count > 0)
        {
            int packed = queue.Dequeue();
            int x = packed / (size + 2);
            int y = packed % (size + 2);

            for (int d = 0; d < 8; d++)
            {
                int nx = x + DX8[d];
                int ny = y + DY8[d];
                if (nx < 1 || nx > size || ny < 1 || ny > size) continue;
                if (isLake[nx, ny]) continue;
                if (map[nx, ny] >= spillHeight) continue;

                isLake[nx, ny] = true;
                lakeIdMap[nx, ny] = lid;
                lakeLevel[nx, ny] = spillHeight;
                queue.Enqueue(nx * (size + 2) + ny);
            }
        }
    }

    /// <summary>
    /// 以 (cx,cy) 为中心在半径 width/2 范围涂河流宽度
    /// </summary>
    private static void PaintRiver(int[,] riverWidth, int cx, int cy, int width, int size)
    {
        int r = width / 2;
        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx < 1 || nx > size || ny < 1 || ny > size) continue;
                if (riverWidth[nx, ny] < width)
                    riverWidth[nx, ny] = width;
            }
        }
    }
}
