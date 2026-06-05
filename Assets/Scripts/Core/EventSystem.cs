// EventSystem.cs - 随机环境事件：触发条件、活跃实例与每步执行
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Random = System.Random;

/// <summary>全局事件调度：每回合检定触发、执行活跃事件、移除已结束实例。</summary>
public static class EventSystem
{
    public const int TypeLocalWarming = 1;  // 局部升温事件类型 id
    public const int TypeLocalCooling = 2;  // 局部降温事件类型 id

    /// <summary>UI 用活跃事件只读快照。</summary>
    public struct ActiveEventSnapshot
    {
        public int instanceId;
        public int typeId;
        public string title;
        public string detail;
        public bool isFinished;
    }

    delegate bool EventCondition(Random rng);
    delegate ActiveEventInstance EventFactory(Random rng);

    struct EventTypeDefinition
    {
        public int typeId;
        public string displayName;
        public EventCondition condition;
        public EventFactory factory;
    }

    static readonly object activeLock = new object();
    static readonly List<EventTypeDefinition> eventTypes = new List<EventTypeDefinition>();
    static readonly List<ActiveEventInstance> activeEvents = new List<ActiveEventInstance>();
    static int nextInstanceId = 1;
    static bool initialized;

    /// <summary>注册事件类型定义（幂等）。</summary>
    public static void Init()
    {
        if (initialized)
            return;

        eventTypes.Clear();
        RegisterEventType(TypeLocalWarming, "局部升温", RollOnePercentTrigger, CreateLocalWarming);
        RegisterEventType(TypeLocalCooling, "局部降温", RollOnePercentTrigger, CreateLocalCooling);
        initialized = true;
    }

    public static void ResetRuntimeState()
    {
        lock (activeLock)
        {
            activeEvents.Clear();
            nextInstanceId = 1;
        }
    }

    /// <summary>每模拟步：检定新触发、执行活跃事件、移除已结束实例。</summary>
    public static void Update(Envir[,] envirData)
    {
        if (!initialized || envirData == null)
            return;

        // 全程持锁：触发检测 + 执行 + 清理。活跃事件数量通常很小，不是每步主瓶颈。
        // LocalTemperatureEvent 在构造时已预计算影响格列表，Execute 为 O(affectedCells) 而非每回合扫描半径。
        Random rng = SimulationCore.EnsureThreadRng();

        lock (activeLock)
        {
            for (int i = 0; i < eventTypes.Count; i++)
            {
                EventTypeDefinition def = eventTypes[i];
                if (def.condition == null || !def.condition(rng))
                    continue;
                if (def.factory == null)
                    continue;

                ActiveEventInstance instance = def.factory(rng);
                if (instance == null)
                    continue;

                instance.InstanceId = Interlocked.Increment(ref nextInstanceId);
                instance.TypeId = def.typeId;
                activeEvents.Add(instance);
            }

            for (int i = 0; i < activeEvents.Count; i++)
                activeEvents[i].Execute(envirData);

            for (int i = activeEvents.Count - 1; i >= 0; i--)
            {
                if (activeEvents[i].IsFinished)
                    activeEvents.RemoveAt(i);
            }
        }
    }

    public static ActiveEventSnapshot[] GetActiveEventSnapshots()
    {
        lock (activeLock)
        {
            ActiveEventSnapshot[] snapshots = new ActiveEventSnapshot[activeEvents.Count];
            for (int i = 0; i < activeEvents.Count; i++)
                snapshots[i] = activeEvents[i].ToSnapshot();
            return snapshots;
        }
    }

    public static int GetActiveEventCount()
    {
        lock (activeLock)
            return activeEvents.Count;
    }

    static void RegisterEventType(int typeId, string displayName, EventCondition condition, EventFactory factory)
    {
        eventTypes.Add(new EventTypeDefinition
        {
            typeId = typeId,
            displayName = displayName,
            condition = condition,
            factory = factory
        });
    }

    /// <summary>每回合 1% 触发：random % 100 == 0。</summary>
    static bool RollOnePercentTrigger(Random rng)
    {
        return rng.Next(100) == 0;
    }

    static ActiveEventInstance CreateLocalWarming(Random rng)
    {
        PickRandomCenter(rng, out int cx, out int cy);
        return new LocalTemperatureEventInstance(cx, cy, warmingFirst: true);
    }

    static ActiveEventInstance CreateLocalCooling(Random rng)
    {
        PickRandomCenter(rng, out int cx, out int cy);
        return new LocalTemperatureEventInstance(cx, cy, warmingFirst: false);
    }

    static void PickRandomCenter(Random rng, out int cx, out int cy)
    {
        int size = SimulationConfig.EnvirSize;
        cx = rng.Next(1, size + 1);
        cy = rng.Next(1, size + 1);
    }
}

public abstract class ActiveEventInstance
{
    public int InstanceId;
    public int TypeId;
    public bool IsFinished;

    public abstract void Execute(Envir[,] envirData);
    public abstract EventSystem.ActiveEventSnapshot ToSnapshot();
}

sealed class LocalTemperatureEventInstance : ActiveEventInstance
{
    const int PhaseTurns = 50;
    const int TotalTurns = PhaseTurns * 2;
    const int Radius = 20;
    const float TotalDeltaC = 10f;
    static readonly float DeltaPerTurn = TotalDeltaC / PhaseTurns;

    readonly int centerX;
    readonly int centerY;
    readonly bool warmingFirst;
    readonly int[] affectedX;
    readonly int[] affectedY;

    int elapsedTurns;

    public LocalTemperatureEventInstance(int centerX, int centerY, bool warmingFirst)
    {
        this.centerX = centerX;
        this.centerY = centerY;
        this.warmingFirst = warmingFirst;
        // 构造时一次性扫描半径内格点（≈1257 格），避免每回合重复距离判断
        BuildAffectedCells(centerX, centerY, Radius, out affectedX, out affectedY);
    }

    public override void Execute(Envir[,] envirData)
    {
        if (IsFinished || envirData == null)
            return;

        float deltaC = GetCurrentDeltaC();
        float deltaK = deltaC;

        for (int i = 0; i < affectedX.Length; i++)
        {
            Envir env = envirData[affectedX[i], affectedY[i]];
            if (env != null)
                env.Temp += deltaK;
        }

        elapsedTurns++;
        if (elapsedTurns >= TotalTurns)
            IsFinished = true;
    }

    float GetCurrentDeltaC()
    {
        bool firstPhase = elapsedTurns < PhaseTurns;
        if (warmingFirst)
            return firstPhase ? DeltaPerTurn : -DeltaPerTurn;
        return firstPhase ? -DeltaPerTurn : DeltaPerTurn;
    }

    public override EventSystem.ActiveEventSnapshot ToSnapshot()
    {
        string phaseName;
        int phaseTurn = elapsedTurns + 1;
        if (elapsedTurns < PhaseTurns)
            phaseName = warmingFirst ? "升温阶段" : "降温阶段";
        else if (elapsedTurns < TotalTurns)
            phaseName = warmingFirst ? "回落阶段" : "回升阶段";
        else
            phaseName = "已结束";

        int phaseIndex = elapsedTurns < PhaseTurns ? elapsedTurns + 1 : elapsedTurns - PhaseTurns + 1;
        int phaseMax = PhaseTurns;

        string title = warmingFirst ? "局部升温" : "局部降温";
        string detail = string.Format(
            "实例 ID: {0}\n类型 ID: {1}\n中心格: ({2}, {3})\n影响半径: 欧拉距离 {4} 格\n影响格数: {5}\n当前阶段: {6} ({7}/{8})\n总进度: {9}/{10} 回合\n本回合温度变化: {11:+#0.0;-#0.0;0}°C\n总变化幅度: 先{12}10°C，再反向 10°C",
            InstanceId,
            TypeId,
            centerX,
            centerY,
            Radius,
            affectedX.Length,
            phaseName,
            phaseIndex,
            phaseMax,
            Mathf.Min(elapsedTurns, TotalTurns),
            TotalTurns,
            IsFinished ? 0f : GetCurrentDeltaC(),
            warmingFirst ? "升" : "降");

        return new EventSystem.ActiveEventSnapshot
        {
            instanceId = InstanceId,
            typeId = TypeId,
            title = title,
            detail = detail,
            isFinished = IsFinished
        };
    }

    static void BuildAffectedCells(int centerX, int centerY, int radius, out int[] xs, out int[] ys)
    {
        int size = SimulationConfig.EnvirSize;
        int radiusSq = radius * radius;
        List<int> xList = new List<int>();
        List<int> yList = new List<int>();

        int minX = Math.Max(1, centerX - radius);
        int maxX = Math.Min(size, centerX + radius);
        int minY = Math.Max(1, centerY - radius);
        int maxY = Math.Min(size, centerY + radius);

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - centerY;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;
                if (dx * dx + dy * dy <= radiusSq)
                {
                    xList.Add(x);
                    yList.Add(y);
                }
            }
        }

        xs = xList.ToArray();
        ys = yList.ToArray();
    }
}
