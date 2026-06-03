// SimulationCore.cs - 完全独立的计算模块，运行在后台线程
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using Random = System.Random;
using Stopwatch = System.Diagnostics.Stopwatch;

public static class SimulationCore
{
    // ========== 世界数据 ==========
    public static Envir[,] EnvirData;
    public static List<Cell> AllCells = new List<Cell>();
    public static Random Rng;
    [ThreadStatic] public static Random ThreadRng;
    public static int _rngSeedCounter;

    // ========== 行为系统（各命名空间静态类） ==========
    // Multiply.Behavior, Temperature.Behavior, Light.Behavior, Death.Behavior

    // ========== 线程控制 ==========
    private static bool isRunning = false;
    private static bool isPaused = false;
    private static Thread simulationThread;
    private static Stopwatch timer = new Stopwatch();

    // ========== 统计数据 ==========
    public static long stepsPerSecond = 0;
    public static long totalSteps = 0;
    public static int aliveCellCount = 0;

    // ========== 研发系统 ==========
    private static long researchPoints = 0;
    private static long researchStepCounter = 0;
    private static int lastResearchGainAmount = 0;
    private static double playSeconds = 0;
    public static int TempLowUpgradeLevel = 0;
    public static int TempHighUpgradeLevel = 0;

    // ========== 模拟速度 ==========
    public static int speedMultiplier = 1; // 1x = 1步/秒, 10x = 10步/秒
    private static Vector3 lastClimateLightDirection = SimulationConfig.TerrainLightDirection;
    private static bool climateInitialized = false;
    private static int[,] distanceToLand;

    // ========== 八方向偏移 ==========
    public static readonly int[] DX = { -1, -1, -1, 0, 0, 1, 1, 1 };
    public static readonly int[] DY = { -1, 0, 1, -1, 1, -1, 0, 1 };

    public static void InitWorld()
    {
        Rng = new Random(SimulationConfig.WorldSeed);
        _rngSeedCounter = SimulationConfig.WorldSeed + 1000;
        climateInitialized = false;
        lastClimateLightDirection = SimulationConfig.TerrainLightDirection;
        distanceToLand = null;
        researchPoints = 0;
        researchStepCounter = 0;
        lastResearchGainAmount = 0;
        TempLowUpgradeLevel = 0;
        TempHighUpgradeLevel = 0;
        totalSteps = 0;
        playSeconds = 0;
        int size = SimulationConfig.EnvirSize;
        EnvirData = new Envir[size + 2, size + 2];
        for (int x = 1; x <= size; x++)
        {
            for (int y = 1; y <= size; y++)
            {
                EnvirData[x, y] = new Envir();
            }
        }
        AllCells.Clear();
        // 生成地形（柏林噪声 + 侵蚀 + 河流）
        TerrainGenerator.Generate(EnvirData, SimulationConfig.WorldSeed);
        ChemistrySystem.Init();
        ChemistrySystem.ResetOverlayMask();
        GeneMutationTable.Init();
        EventSystem.Init();
        EventSystem.ResetRuntimeState();
        BuildDistanceToLandField();
        RefreshEnvironmentClimateIfNeeded(true);

        // 从世界中心搜索最近陆地作为玩家出生点
        int cx = size / 2, cy = size / 2;
        for (int r = 0; r <= 200 && EnvirData[cx, cy].Topography == 0; r++)
        {
            bool found = false;
            for (int dx = -r; dx <= r && !found; dx++)
                for (int dy = -r; dy <= r && !found; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    int tx = cx + dx, ty = cy + dy;
                    if (tx >= 1 && tx <= size && ty >= 1 && ty <= size && EnvirData[tx, ty].Topography != 0)
                    { cx = tx; cy = ty; found = true; }
                }
        }
        int baseMinTemp = SimulationConfig.BaseTempToleranceMin;
        int baseMaxTemp = SimulationConfig.BaseTempToleranceMax;
        for (int i = 0; i < SimulationConfig.InitialPlayerCells; i++)
        {
            bool placed = false;
            int px = cx;
            int py = cy;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                px = cx + Rng.Next(-6, 7);
                py = cy + Rng.Next(-6, 7);
                px = Math.Max(1, Math.Min(size, px));
                py = Math.Max(1, Math.Min(size, py));
                Envir env = EnvirData[px, py];
                if (env.Topography == 0) continue;
                if (env.Temp < baseMinTemp || env.Temp > baseMaxTemp) continue;

                Cell cell = new Cell(px, py, true);
                Species.InitPlayerCell(cell);
                if (env.AddCell(cell))
                {
                    AllCells.Add(cell);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                for (int attempt = 0; attempt < 400 && !placed; attempt++)
                {
                    px = Rng.Next(1, size + 1);
                    py = Rng.Next(1, size + 1);
                    Envir env = EnvirData[px, py];
                    if (env.Topography == 0) continue;
                    if (env.Temp < baseMinTemp || env.Temp > baseMaxTemp) continue;

                    Cell cell = new Cell(px, py, true);
                    Species.InitPlayerCell(cell);
                    if (env.AddCell(cell))
                    {
                        AllCells.Add(cell);
                        placed = true;
                    }
                }
            }

            if (!placed)
            {
                for (int attempt = 0; attempt < 200 && !placed; attempt++)
                {
                    px = Rng.Next(1, size + 1);
                    py = Rng.Next(1, size + 1);
                    Envir env = EnvirData[px, py];
                    if (env.Topography == 0) continue;

                    Cell cell = new Cell(px, py, true);
                    Species.InitPlayerCell(cell);
                    if (env.AddCell(cell))
                    {
                        AllCells.Add(cell);
                        placed = true;
                    }
                }
            }
        }
        for (int c = 0; c < SimulationConfig.InitialNPCClusters; c++)
        {
            int clusterX = Rng.Next(50, size - 50);
            int clusterY = Rng.Next(50, size - 50);
            int cAttempts = 0;
            while (EnvirData[clusterX, clusterY].Topography == 0 && cAttempts++ < 200)
            {
                clusterX = Rng.Next(50, size - 50);
                clusterY = Rng.Next(50, size - 50);
            }
            if (EnvirData[clusterX, clusterY].Topography == 0) continue;
            for (int i = 0; i < SimulationConfig.InitialNPCPerCluster; i++)
            {
                int px = clusterX + Rng.Next(-3, 4);
                int py = clusterY + Rng.Next(-3, 4);
                px = Math.Max(1, Math.Min(size, px));
                py = Math.Max(1, Math.Min(size, py));
                if (EnvirData[px, py].Topography == 0) continue;
                Cell cell = new Cell(px, py, false);
                Species.InitNPCCell(cell, Rng);
                if (EnvirData[px, py].AddCell(cell))
                    AllCells.Add(cell);
            }
        }
            ConvertEnvironmentTempToKelvin();
        // 初始化各行为命名空间
        Multiply.Behavior.Init();
        Temperature.Behavior.Init();
        Light.Behavior.Init();
        Death.Behavior.Init();

        aliveCellCount = AllCells.Count;
    }

    public static void StartCalculationThread()
    {
        if (isRunning) return;
        InitWorld();
        isRunning = true;
        isPaused = false;
        simulationThread = new Thread(CalculationLoop);
        simulationThread.Priority = System.Threading.ThreadPriority.AboveNormal;
        simulationThread.IsBackground = true;
        simulationThread.Start();
    }

    public static void StartCalculationThreadFromLoadedWorld()
    {
        if (isRunning) return;
        if (EnvirData == null)
        {
            StartCalculationThread();
            return;
        }

        ChemistrySystem.Init();
        GeneMutationTable.Init();
        EventSystem.Init();
        isRunning = true;
        isPaused = false;
        simulationThread = new Thread(CalculationLoop);
        simulationThread.Priority = System.Threading.ThreadPriority.AboveNormal;
        simulationThread.IsBackground = true;
        simulationThread.Start();
    }

    static bool IsSimulationShutdownException(Exception ex)
    {
        if (ex is ThreadAbortException)
            return true;

        if (ex is AggregateException aggregate)
        {
            foreach (Exception inner in aggregate.Flatten().InnerExceptions)
            {
                if (inner is ThreadAbortException)
                    return true;
            }
        }

        return false;
    }

    private static void CalculationLoop()
    {
        ThreadRng = new Random(Interlocked.Increment(ref _rngSeedCounter));
        timer.Reset();
        timer.Start();
        long stepCount = 0;
        long lastStatsMs = 0;
        long nextStepAtMs = 1000;
        while (isRunning)
        {
            long nowMs = timer.ElapsedMilliseconds;

            if (isPaused)
            {
                nextStepAtMs = nowMs + 1000 / Math.Max(1, speedMultiplier);
                Thread.Sleep(20);
                continue;
            }

            int currentSpeed = Math.Max(1, speedMultiplier);
            long intervalMs = 1000 / currentSpeed;
            if (intervalMs <= 0) intervalMs = 1;

            if (nowMs >= nextStepAtMs)
            {
                try
                {
                    SimulateOneStep();
                }
                catch (Exception ex)
                {
                    if (IsSimulationShutdownException(ex))
                        break;

                    isRunning = false;
                    Debug.LogException(ex);
                    break;
                }
                stepCount++;
                totalSteps++;

                long updatedNowMs = timer.ElapsedMilliseconds;
                nextStepAtMs += intervalMs;
                if (nextStepAtMs < updatedNowMs)
                    nextStepAtMs = updatedNowMs + intervalMs;
            }
            else
            {
                int sleepMs = (int)Math.Min(10, nextStepAtMs - nowMs);
                if (sleepMs > 0)
                    Thread.Sleep(sleepMs);
            }

            nowMs = timer.ElapsedMilliseconds;
            if (nowMs - lastStatsMs >= 1000)
            {
                Interlocked.Exchange(ref stepsPerSecond, stepCount);
                Interlocked.Exchange(ref aliveCellCount, AllCells.Count);
                stepCount = 0;
                lastStatsMs = nowMs;
            }
        }
    }

    private static void SimulateOneStep()
    {
        LightUpdate.Update(EnvirData);
        ChemistrySystem.ApplyEnvironmentReactions(EnvirData);
        EnvironmentDiffusionSystem.Update(EnvirData);
        EventSystem.Update(EnvirData);

        // 不再排序AllCells —— 各行为Pre独立于遍历顺序，Multiply.Apply自行排序缓冲区
        // 原Sort O(N log N) 在100万细胞时约占50%时间，移除后直接翻倍

        Parallel.Invoke(
            () => Multiply.Behavior.Pre(),
            () => Temperature.Behavior.Pre(),
            () => Light.Behavior.Pre(),
            () => Death.Behavior.Pre());
        
        Multiply.Behavior.Apply();
        Temperature.Behavior.Apply();
        Light.Behavior.Apply();
        Death.Behavior.Apply();

        // 能量消耗并行执行
        var allCells = AllCells;
        int count = allCells.Count;
        if (count > 0)
        {
            Parallel.ForEach(Partitioner.Create(0, count), range =>
            {
                if (ThreadRng == null)
                    ThreadRng = new Random(Interlocked.Increment(ref _rngSeedCounter));
                for (int idx = range.Item1; idx < range.Item2; idx++)
                {
                    Cell cell = allCells[idx];
                    if (!cell.alive) continue;
                    cell.energy -= cell.GetTotalEnergyCost();
                    if (cell.energy <= 0) cell.alive = false;
                }
            });
        }

        CleanupDeadCells();
        researchStepCounter++;
        GainResearchPoints();
        DebugTemperatureStats(researchStepCounter);
    }

    private static void DebugTemperatureStats(long step)
    {
        if (!SimulationConfig.DebugTempStatsEnabled || EnvirData == null)
            return;

        int interval = Math.Max(1, SimulationConfig.DebugTempStatsInterval);
        if (step % interval != 0)
            return;

        int size = SimulationConfig.EnvirSize;
        int stride = Math.Max(1, SimulationConfig.DebugTempStatsStride);
        float minTemp = float.MaxValue;
        float maxTemp = float.MinValue;
        int minX = 1, minY = 1, maxX = 1, maxY = 1;

        for (int y = 1; y <= size; y += stride)
        {
            for (int x = 1; x <= size; x += stride)
            {
                float temp = EnvirData[x, y].Temp;
                if (temp < minTemp)
                {
                    minTemp = temp;
                    minX = x;
                    minY = y;
                }
                if (temp > maxTemp)
                {
                    maxTemp = temp;
                    maxX = x;
                    maxY = y;
                }
            }
        }

        float minC = KelvinToCelsius(minTemp);
        float maxC = KelvinToCelsius(maxTemp);
        Debug.Log(string.Format(
            "[DEBUG-TEMP] step={0} minK={1:F2} ({2},{3}) minC={4:F2} maxK={5:F2} ({6},{7}) maxC={8:F2}",
            step, minTemp, minX, minY, minC, maxTemp, maxX, maxY, maxC));
    }

    private static void RefreshEnvironmentClimateIfNeeded(bool force)
    {
        Vector3 currentLightDirection = CellRenderer.GetTerrainLightDirection();
        if (currentLightDirection.sqrMagnitude < 0.0001f)
            currentLightDirection = SimulationConfig.TerrainLightDirection;
        currentLightDirection = currentLightDirection.normalized;

        if (!force && climateInitialized &&
            (currentLightDirection - lastClimateLightDirection).sqrMagnitude <= 0.000001f)
            return;

        UpdateEnvironmentClimate(currentLightDirection);
        lastClimateLightDirection = currentLightDirection;
        climateInitialized = true;
    }

    private static void BuildDistanceToLandField()
    {
        int size = SimulationConfig.EnvirSize;
        int stride = size + 2;
        distanceToLand = new int[stride, stride];
        Queue<int> queue = new Queue<int>();

        for (int x = 1; x <= size; x++)
        {
            for (int y = 1; y <= size; y++)
            {
                int topo = EnvirData[x, y].Topography;
                bool isLandSource = topo == 1 || topo == 2;
                distanceToLand[x, y] = isLandSource ? 0 : -1;
                if (isLandSource)
                    queue.Enqueue(x * stride + y);
            }
        }

        while (queue.Count > 0)
        {
            int packed = queue.Dequeue();
            int x = packed / stride;
            int y = packed % stride;
            int nextDistance = distanceToLand[x, y] + 1;

            int left = x == 1 ? size : x - 1;
            int right = x == size ? 1 : x + 1;
            int down = y == 1 ? size : y - 1;
            int up = y == size ? 1 : y + 1;

            if (distanceToLand[left, y] < 0)
            {
                distanceToLand[left, y] = nextDistance;
                queue.Enqueue(left * stride + y);
            }
            if (distanceToLand[right, y] < 0)
            {
                distanceToLand[right, y] = nextDistance;
                queue.Enqueue(right * stride + y);
            }
            if (distanceToLand[x, down] < 0)
            {
                distanceToLand[x, down] = nextDistance;
                queue.Enqueue(x * stride + down);
            }
            if (distanceToLand[x, up] < 0)
            {
                distanceToLand[x, up] = nextDistance;
                queue.Enqueue(x * stride + up);
            }
        }
    }

    private static void UpdateEnvironmentClimate(Vector3 sunDirection)
    {
        int size = SimulationConfig.EnvirSize;
        float invAltitudeRange = 1f / Mathf.Max(1f, SimulationConfig.AltitudeMax - SimulationConfig.AltitudeMin);
        float[,] temperatureField = new float[size + 2, size + 2];

        Parallel.For(1, size + 1, y =>
        {
            float latitude01 = 1f - Mathf.Abs(((y - 1f) / Mathf.Max(1f, size - 1f)) * 2f - 1f);
            float latitudeLight = Mathf.Lerp(
                SimulationConfig.ClimateLatitudeLightMin,
                SimulationConfig.ClimateLatitudeLightMax,
                latitude01);
            float latitudeBaseTemp = Mathf.Lerp(
                SimulationConfig.ClimateLatitudeTempMin,
                SimulationConfig.ClimateLatitudeTempMax,
                latitude01);

            int yDown = y == 1 ? size : y - 1;
            int yUp = y == size ? 1 : y + 1;

            for (int x = 1; x <= size; x++)
            {
                Envir env = EnvirData[x, y];
                int xLeft = x == 1 ? size : x - 1;
                int xRight = x == size ? 1 : x + 1;

                float centerHeight = env.Height;
                float leftHeight = EnvirData[xLeft, y].Height;
                float rightHeight = EnvirData[xRight, y].Height;
                float downHeight = EnvirData[x, yDown].Height;
                float upHeight = EnvirData[x, yUp].Height;

                float gradientX = (rightHeight - leftHeight) * 0.5f * invAltitudeRange * 10f;
                float gradientY = (upHeight - downHeight) * 0.5f * invAltitudeRange * 10f;
                Vector3 terrainNormal = new Vector3(-gradientX, -gradientY, 1f).normalized;

                float sunExposure = Mathf.Max(0f, Vector3.Dot(terrainNormal, sunDirection));
                float aboveSea01 = Mathf.InverseLerp(SimulationConfig.AltitudeThreshold, SimulationConfig.AltitudeMax, centerHeight);
                bool isWater = env.Topography == 0 || env.Topography == 3 || env.Topography == 4;
                float distanceToNearestLand01 = 0f;
                if (distanceToLand != null)
                    distanceToNearestLand01 = Mathf.Clamp01(distanceToLand[x, y] / SimulationConfig.ClimateOpenWaterDistanceScale);

                float light01 = SimulationConfig.ClimateBaseLight
                    + latitudeLight * SimulationConfig.ClimateLatitudeLightWeight
                    + sunExposure * SimulationConfig.ClimateSunExposureWeight
                    + aboveSea01 * SimulationConfig.ClimateAltitudeLightWeight
                    + sunDirection.z * SimulationConfig.ClimateSunHeightLightWeight;
                light01 = Mathf.Clamp01(light01);
                env.Light = Mathf.RoundToInt(Mathf.Lerp(SimulationConfig.LightMin, SimulationConfig.LightMax, light01));

                float temp = latitudeBaseTemp;
                temp += (env.Light - SimulationConfig.DefaultLight) * SimulationConfig.ClimateLightToTempWeight;
                float heightAboveSea = Mathf.Max(0f, centerHeight - SimulationConfig.AltitudeThreshold);
                float heightStep = Mathf.Max(0.0001f, SimulationConfig.AltitudeTempLapseHeightStep);
                float altitudeCooling = (heightAboveSea / heightStep) * SimulationConfig.AltitudeTempLapsePerStep;
                temp -= altitudeCooling;

                if (isWater)
                {
                    float maritimeTarget = Mathf.Lerp(
                        SimulationConfig.ClimateMaritimeTempMin,
                        SimulationConfig.ClimateMaritimeTempMax,
                        latitude01);
                    maritimeTarget -= distanceToNearestLand01 * SimulationConfig.ClimateOpenWaterCoolingStrength;
                    float maritimeBlend = Mathf.Clamp01(
                        SimulationConfig.ClimateMaritimeBlend
                        + distanceToNearestLand01 * SimulationConfig.ClimateOpenWaterBlendBoost);
                    temp = Mathf.Lerp(temp, maritimeTarget, maritimeBlend);
                }
                else if (env.Topography == 2)
                {
                    float coastalTarget = Mathf.Lerp(
                        SimulationConfig.ClimateBeachTempMin,
                        SimulationConfig.ClimateBeachTempMax,
                        latitude01);
                    temp = Mathf.Lerp(temp, coastalTarget, SimulationConfig.ClimateBeachBlend);
                }
                else
                {
                    float coastalWarmth = (1f - distanceToNearestLand01) * SimulationConfig.ClimateCoastalLandWarmth;
                    temp += coastalWarmth;
                }

                if (centerHeight < SimulationConfig.AltitudeThreshold)
                {
                    float underwaterDepth01 = Mathf.InverseLerp(SimulationConfig.AltitudeThreshold, SimulationConfig.AltitudeMin, centerHeight);
                    float underwaterTarget = Mathf.Lerp(
                        SimulationConfig.ClimateUnderwaterTempMin,
                        SimulationConfig.ClimateUnderwaterTempMax,
                        latitude01);
                    temp = Mathf.Lerp(temp, underwaterTarget, underwaterDepth01 * SimulationConfig.ClimateUnderwaterBlend);
                }

                temperatureField[x, y] = Mathf.Clamp(temp, SimulationConfig.TempMin, SimulationConfig.TempMax);
            }
        });

        int smoothingPasses = Mathf.Max(0, SimulationConfig.ClimateTemperatureSmoothingPasses);
        float smoothingStrength = Mathf.Clamp01(SimulationConfig.ClimateTemperatureSmoothingStrength);
        if (smoothingPasses > 0 && smoothingStrength > 0f)
        {
            float[,] smoothedField = new float[size + 2, size + 2];

            for (int pass = 0; pass < smoothingPasses; pass++)
            {
                Parallel.For(1, size + 1, y =>
                {
                    int yDown = y == 1 ? size : y - 1;
                    int yUp = y == size ? 1 : y + 1;

                    for (int x = 1; x <= size; x++)
                    {
                        int xLeft = x == 1 ? size : x - 1;
                        int xRight = x == size ? 1 : x + 1;

                        float center = temperatureField[x, y];
                        float weightedAverage = (
                            center * 4f
                            + (temperatureField[xLeft, y] + temperatureField[xRight, y] + temperatureField[x, yDown] + temperatureField[x, yUp]) * 2f
                            + temperatureField[xLeft, yDown]
                            + temperatureField[xLeft, yUp]
                            + temperatureField[xRight, yDown]
                            + temperatureField[xRight, yUp]) / 16f;

                        smoothedField[x, y] = Mathf.Lerp(center, weightedAverage, smoothingStrength);
                    }
                });

                float[,] tempSwap = temperatureField;
                temperatureField = smoothedField;
                smoothedField = tempSwap;
            }
        }

        Parallel.For(1, size + 1, y =>
        {
            for (int x = 1; x <= size; x++)
            {
                EnvirData[x, y].Temp = Mathf.RoundToInt(temperatureField[x, y]);
            }
        });
    }

    private static void ConvertEnvironmentTempToKelvin()
    {
        if (EnvirData == null) return;

        int size = SimulationConfig.EnvirSize;
        float offset = SimulationConfig.KelvinOffset;
        for (int y = 1; y <= size; y++)
        {
            for (int x = 1; x <= size; x++)
            {
                EnvirData[x, y].Temp += offset;
            }
        }
    }

    // O(N)清理：前向压缩代替逐个RemoveAt（原为O(N²)最坏情况）
    private static void CleanupDeadCells()
    {
        int writeIdx = 0;
        for (int i = 0; i < AllCells.Count; i++)
        {
            Cell cell = AllCells[i];
            if (cell.alive)
            {
                AllCells[writeIdx++] = cell;
            }
            else
            {
                Envir env = EnvirData[cell.px, cell.py];
                for (int j = 1; j <= env.CellNum; j++)
                {
                    if (env.CellList[j] == cell)
                    {
                        env.RemoveCell(j);
                        break;
                    }
                }
            }
        }
        if (writeIdx < AllCells.Count)
            AllCells.RemoveRange(writeIdx, AllCells.Count - writeIdx);
    }

    public static bool InBounds(int x, int y)
    {
        return x >= 1 && x <= SimulationConfig.EnvirSize && y >= 1 && y <= SimulationConfig.EnvirSize;
    }

    public static int GetNeighborCellCount(int px, int py)
    {
        int count = 0;
        for (int d = 0; d < 8; d++)
        {
            int nx = px + DX[d];
            int ny = py + DY[d];
            if (InBounds(nx, ny))
                count += EnvirData[nx, ny].CellNum;
        }
        return count;
    }

    public static void PauseSimulation() { isPaused = true; }
    public static void ResumeSimulation() { isPaused = false; }
    public static bool IsPaused() { return isPaused; }
    public static Random EnsureThreadRng()
    {
        if (ThreadRng == null)
            ThreadRng = new Random(Interlocked.Increment(ref _rngSeedCounter));
        return ThreadRng;
    }

    public static void SetSpeedMultiplier(int value)
    {
        speedMultiplier = Math.Max(1, Math.Min(10, value));
    }

    public static void StopCalculation()
    {
        isRunning = false;
        simulationThread?.Join(2000);
    }

    public static void GainResearchPoints()
    {
        int baseGain = Math.Max(0, SimulationConfig.ResearchBaseGainPerStep);
        int coverageCells = DeviceSystem.CountResearchCoverageCells();
        int perCell = Math.Max(0, SimulationConfig.ResearchDeviceGainPerCell);
        int totalGain = baseGain + coverageCells * perCell;

        Interlocked.Exchange(ref lastResearchGainAmount, totalGain);
        if (totalGain > 0)
            Interlocked.Add(ref researchPoints, totalGain);
    }

    public static bool TrySpendResearchPoints(int cost)
    {
        if (cost <= 0)
            return false;
        while (true)
        {
            long current = Interlocked.Read(ref researchPoints);
            if (current < cost)
                return false;
            long next = current - cost;
            if (Interlocked.CompareExchange(ref researchPoints, next, current) == current)
                return true;
        }
    }

    public static long GetResearchPoints()
    {
        return Interlocked.Read(ref researchPoints);
    }

    public static void SetResearchPoints(long value)
    {
        Interlocked.Exchange(ref researchPoints, Math.Max(0, value));
    }

    public static long GetResearchStepCounter()
    {
        return Interlocked.Read(ref researchStepCounter);
    }

    public static void SetResearchStepCounter(long value)
    {
        Interlocked.Exchange(ref researchStepCounter, Math.Max(0, value));
    }

    public static int GetLastResearchGainAmount()
    {
        return Interlocked.CompareExchange(ref lastResearchGainAmount, 0, 0);
    }

    public static double GetPlaySeconds()
    {
        return playSeconds;
    }

    public static void SetPlaySeconds(double value)
    {
        playSeconds = Math.Max(0.0, value);
    }

    public static void AddPlaySeconds(double delta)
    {
        if (delta <= 0)
            return;
        playSeconds += delta;
    }

    public static float CelsiusToKelvin(float tempC)
    {
        return tempC + SimulationConfig.KelvinOffset;
    }

    public static float KelvinToCelsius(float tempK)
    {
        return tempK - SimulationConfig.KelvinOffset;
    }

    public static int GetTempUpgradeCost(int nextLevel)
    {
        if (nextLevel <= 0)
            return 0;

        double factor = Math.Pow(SimulationConfig.ResearchTempUpgradeGrowth, nextLevel - 1);
        double cost = SimulationConfig.ResearchTempUpgradeBaseCost * factor;
        return Math.Max(1, (int)Math.Round(cost));
    }

    public static int EncodeTempUpgradeHash(int lowLevel, int highLevel)
    {
        lowLevel = Math.Max(0, Math.Min(SimulationConfig.ResearchTempUpgradeMaxLevel, lowLevel));
        highLevel = Math.Max(0, Math.Min(SimulationConfig.ResearchTempUpgradeMaxLevel, highLevel));
        return (highLevel << 4) | (lowLevel & 0x0F);
    }

    public static void DecodeTempUpgradeHash(int upgradeHash, out int lowLevel, out int highLevel)
    {
        lowLevel = upgradeHash & 0x0F;
        highLevel = (upgradeHash >> 4) & 0x0F;
    }

    public static float GetTempToleranceMinFromUpgradeHash(int upgradeHash)
    {
        DecodeTempUpgradeHash(upgradeHash, out int lowLevel, out _);
        float tempC = SimulationConfig.BaseTempToleranceMin - lowLevel;
        return CelsiusToKelvin(tempC);
    }

    public static float GetTempToleranceMaxFromUpgradeHash(int upgradeHash)
    {
        DecodeTempUpgradeHash(upgradeHash, out _, out int highLevel);
        float tempC = SimulationConfig.BaseTempToleranceMax + highLevel;
        return CelsiusToKelvin(tempC);
    }

    public static bool TryUpgradeTempLow()
    {
        if (TempLowUpgradeLevel >= SimulationConfig.ResearchTempUpgradeMaxLevel)
            return false;

        int nextLevel = TempLowUpgradeLevel + 1;
        int cost = GetTempUpgradeCost(nextLevel);
        if (!TrySpendResearchPoints(cost))
            return false;

        TempLowUpgradeLevel = nextLevel;
        UpdatePlayerTempGeneHash();
        return true;
    }

    public static bool TryUpgradeTempHigh()
    {
        if (TempHighUpgradeLevel >= SimulationConfig.ResearchTempUpgradeMaxLevel)
            return false;

        int nextLevel = TempHighUpgradeLevel + 1;
        int cost = GetTempUpgradeCost(nextLevel);
        if (!TrySpendResearchPoints(cost))
            return false;

        TempHighUpgradeLevel = nextLevel;
        UpdatePlayerTempGeneHash();
        return true;
    }

    private static void UpdatePlayerTempGeneHash()
    {
        int upgradeHash = EncodeTempUpgradeHash(TempLowUpgradeLevel, TempHighUpgradeLevel);
        for (int i = 0; i < AllCells.Count; i++)
        {
            Cell cell = AllCells[i];
            if (!cell.isPlayer)
                continue;

            bool updated = false;
            for (int g = 1; g < cell.MainGeneList.Length; g++)
            {
                if (cell.MainGeneList[g].baseId != 2)
                    continue;

                int energyCost = cell.MainGeneList[g].energyCost;
                cell.MainGeneList[g] = new Gene(2, energyCost, upgradeHash);
                cell.InvalidateEnergyCostCache();
                updated = true;
                break;
            }

            if (updated)
                continue;

            for (int g = 1; g < cell.MainGeneList.Length; g++)
            {
                if (cell.MainGeneList[g].baseId != 0)
                    continue;

                cell.MainGeneList[g] = new Gene(2, 1, upgradeHash);
                cell.InvalidateEnergyCostCache();
                break;
            }
        }
    }

    public static Envir GetEnvir(int x, int y)
    {
        if (!InBounds(x, y)) return null;
        return EnvirData[x, y];
    }
}
