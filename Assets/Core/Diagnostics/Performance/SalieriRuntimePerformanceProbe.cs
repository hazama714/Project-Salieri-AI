// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

using UnityEngine;
using UnityEngine.Profiling;

namespace SalieriAI.Core.Diagnostics.Performance
{
    public enum SalieriRuntimePerformanceMetric
    {
        HaarImagePreparation = 0,
        HaarDetection,
        YoloPreprocess,
        YoloInference,
        YoloPostprocess,
        YoloTotal,
        ByteTrack,
        AnimatorIk,
        VirtualBody,
        LlmGenerate,
        TtsSynthesis,
        TtsWavRead,
        AudioClipCreate,
        BluetoothConnect,
        BluetoothWrite,
        BluetoothFlush,
        Count
    }

    /// <summary>
    /// Development-build-only aggregate performance observation boundary.
    /// It owns no runtime scheduling, admission, frequency or resource policy.
    /// Calls are no-ops unless the development probe has bootstrapped.
    /// </summary>
    public static class SalieriRuntimePerformanceProbe
    {
        private static readonly long[] Calls =
            new long[(int)SalieriRuntimePerformanceMetric.Count];
        private static readonly long[] TotalTicks =
            new long[(int)SalieriRuntimePerformanceMetric.Count];
        private static readonly long[] MaximumTicks =
            new long[(int)SalieriRuntimePerformanceMetric.Count];
        private static readonly long[] ValueATotal =
            new long[(int)SalieriRuntimePerformanceMetric.Count];
        private static readonly long[] ValueBTotal =
            new long[(int)SalieriRuntimePerformanceMetric.Count];

        private static int enabled;
        private static int llmActive;
        private static int ttsActive;
        private static long cameraObservations;
        private static long uniqueCameraFrames;

        public static bool Enabled => Volatile.Read(ref enabled) != 0;
        public static bool IsLlmActive => Volatile.Read(ref llmActive) > 0;
        public static bool IsTtsActive => Volatile.Read(ref ttsActive) > 0;

        internal static void SetEnabled(bool value)
        {
            Volatile.Write(ref enabled, value ? 1 : 0);
        }

        public static long Timestamp()
        {
            return Enabled ? Stopwatch.GetTimestamp() : 0L;
        }

        public static void RecordDuration(
            SalieriRuntimePerformanceMetric metric,
            long startedTimestamp,
            long valueA = 0L,
            long valueB = 0L)
        {
            if (!Enabled || startedTimestamp <= 0L)
                return;

            int index = (int)metric;
            if (index < 0 || index >= (int)SalieriRuntimePerformanceMetric.Count)
                return;

            long elapsed = Math.Max(0L, Stopwatch.GetTimestamp() - startedTimestamp);
            Interlocked.Increment(ref Calls[index]);
            Interlocked.Add(ref TotalTicks[index], elapsed);
            Interlocked.Add(ref ValueATotal[index], valueA);
            Interlocked.Add(ref ValueBTotal[index], valueB);
            UpdateMaximum(ref MaximumTicks[index], elapsed);
        }

        public static void RecordCameraObservation(bool uniqueCameraFrame)
        {
            if (!Enabled)
                return;

            Interlocked.Increment(ref cameraObservations);
            if (uniqueCameraFrame)
                Interlocked.Increment(ref uniqueCameraFrames);
        }

        public static long BeginLlmGenerate()
        {
            if (!Enabled)
                return 0L;

            Interlocked.Increment(ref llmActive);
            return Stopwatch.GetTimestamp();
        }

        public static void EndLlmGenerate(long startedTimestamp)
        {
            if (!Enabled || startedTimestamp <= 0L)
                return;

            RecordDuration(
                SalieriRuntimePerformanceMetric.LlmGenerate,
                startedTimestamp);
            DecrementNonNegative(ref llmActive);
        }

        public static long BeginTtsSynthesis()
        {
            if (!Enabled)
                return 0L;

            Interlocked.Increment(ref ttsActive);
            return Stopwatch.GetTimestamp();
        }

        public static void EndTtsSynthesis(long startedTimestamp, long wavBytes)
        {
            if (!Enabled || startedTimestamp <= 0L)
                return;

            RecordDuration(
                SalieriRuntimePerformanceMetric.TtsSynthesis,
                startedTimestamp,
                wavBytes);
            DecrementNonNegative(ref ttsActive);
        }

        internal static ProbeWindowSnapshot TakeWindowSnapshot()
        {
            var metrics = new ProbeMetricSnapshot[
                (int)SalieriRuntimePerformanceMetric.Count];

            for (int i = 0; i < metrics.Length; i++)
            {
                metrics[i] = new ProbeMetricSnapshot(
                    Interlocked.Exchange(ref Calls[i], 0L),
                    Interlocked.Exchange(ref TotalTicks[i], 0L),
                    Interlocked.Exchange(ref MaximumTicks[i], 0L),
                    Interlocked.Exchange(ref ValueATotal[i], 0L),
                    Interlocked.Exchange(ref ValueBTotal[i], 0L));
            }

            return new ProbeWindowSnapshot(
                metrics,
                Interlocked.Exchange(ref cameraObservations, 0L),
                Interlocked.Exchange(ref uniqueCameraFrames, 0L));
        }

        internal static double TicksToMilliseconds(long ticks)
        {
            return ticks <= 0L
                ? 0.0
                : ticks * 1000.0 / Stopwatch.Frequency;
        }

        private static void UpdateMaximum(ref long target, long candidate)
        {
            while (true)
            {
                long current = Volatile.Read(ref target);
                if (candidate <= current)
                    return;
                if (Interlocked.CompareExchange(ref target, candidate, current) == current)
                    return;
            }
        }

        private static void DecrementNonNegative(ref int target)
        {
            while (true)
            {
                int current = Volatile.Read(ref target);
                if (current <= 0)
                    return;
                if (Interlocked.CompareExchange(ref target, current - 1, current) == current)
                    return;
            }
        }
    }

    internal readonly struct ProbeMetricSnapshot
    {
        internal readonly long Calls;
        internal readonly long TotalTicks;
        internal readonly long MaximumTicks;
        internal readonly long ValueATotal;
        internal readonly long ValueBTotal;

        internal ProbeMetricSnapshot(
            long calls,
            long totalTicks,
            long maximumTicks,
            long valueATotal,
            long valueBTotal)
        {
            Calls = calls;
            TotalTicks = totalTicks;
            MaximumTicks = maximumTicks;
            ValueATotal = valueATotal;
            ValueBTotal = valueBTotal;
        }
    }

    internal sealed class ProbeWindowSnapshot
    {
        internal readonly ProbeMetricSnapshot[] Metrics;
        internal readonly long CameraObservations;
        internal readonly long UniqueCameraFrames;

        internal ProbeWindowSnapshot(
            ProbeMetricSnapshot[] metrics,
            long cameraObservations,
            long uniqueCameraFrames)
        {
            Metrics = metrics;
            CameraObservations = cameraObservations;
            UniqueCameraFrames = uniqueCameraFrames;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class SalieriRuntimePerformanceProbeHost : MonoBehaviour
    {
        private const int FrameCapacity = 4096;
        private const float SummaryIntervalSeconds = 30f;

        private readonly FrameWindow allFrames = new FrameWindow(FrameCapacity);
        private readonly FrameWindow idleFrames = new FrameWindow(FrameCapacity);
        private readonly FrameWindow llmFrames = new FrameWindow(FrameCapacity);
        private readonly FrameWindow ttsFrames = new FrameWindow(FrameCapacity);
        private readonly FrameWindow combinedFrames = new FrameWindow(FrameCapacity);

        private float nextSummaryTime;
        private int windowId;
        private int initialGc0;
        private int initialGc1;
        private int initialGc2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (FindObjectOfType<SalieriRuntimePerformanceProbeHost>() != null)
                return;

            var hostObject = new GameObject("SalieriRuntimePerformanceProbe");
            DontDestroyOnLoad(hostObject);
            hostObject.hideFlags = HideFlags.DontSave;
            hostObject.AddComponent<SalieriRuntimePerformanceProbeHost>();
#endif
        }

        private void Awake()
        {
            SalieriRuntimePerformanceProbe.SetEnabled(true);
            initialGc0 = GC.CollectionCount(0);
            initialGc1 = GC.CollectionCount(1);
            initialGc2 = GC.CollectionCount(2);
            nextSummaryTime = Time.realtimeSinceStartup + SummaryIntervalSeconds;
            UnityEngine.Debug.Log(
                "[SALIERI-PERF][START] intervalSec=30 frameBudgetMs=33.3 "
                + "capacity=" + FrameCapacity
                + " development=" + UnityEngine.Debug.isDebugBuild);
        }

        private void Update()
        {
            double frameMilliseconds = Time.unscaledDeltaTime * 1000.0;
            allFrames.Add(frameMilliseconds);

            bool llm = SalieriRuntimePerformanceProbe.IsLlmActive;
            bool tts = SalieriRuntimePerformanceProbe.IsTtsActive;
            if (!llm && !tts)
                idleFrames.Add(frameMilliseconds);
            if (llm)
                llmFrames.Add(frameMilliseconds);
            if (tts)
                ttsFrames.Add(frameMilliseconds);
            if (llm && tts)
                combinedFrames.Add(frameMilliseconds);

            if (Time.realtimeSinceStartup >= nextSummaryTime)
            {
                EmitSummary("interval");
                nextSummaryTime = Time.realtimeSinceStartup + SummaryIntervalSeconds;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                EmitSummary("pause");
        }

        private void OnDestroy()
        {
            EmitSummary("destroy");
            SalieriRuntimePerformanceProbe.SetEnabled(false);
        }

        private void EmitSummary(string reason)
        {
            if (!SalieriRuntimePerformanceProbe.Enabled || allFrames.Count == 0)
                return;

            windowId++;
            FrameStatistics all = allFrames.TakeAndReset();
            FrameStatistics idle = idleFrames.TakeAndReset();
            FrameStatistics llm = llmFrames.TakeAndReset();
            FrameStatistics tts = ttsFrames.TakeAndReset();
            FrameStatistics both = combinedFrames.TakeAndReset();
            ProbeWindowSnapshot snapshot =
                SalieriRuntimePerformanceProbe.TakeWindowSnapshot();

            UnityEngine.Debug.Log(
                "[SALIERI-PERF][FRAME] window=" + windowId
                + " reason=" + reason
                + " all=" + all.Format()
                + " idle=" + idle.Format()
                + " llm=" + llm.Format()
                + " tts=" + tts.Format()
                + " both=" + both.Format());

            UnityEngine.Debug.Log(
                "[SALIERI-PERF][VISION] window=" + windowId
                + " cameraObs=" + snapshot.CameraObservations
                + " uniqueCamera=" + snapshot.UniqueCameraFrames
                + " haarPrep=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.HaarImagePreparation)
                + " haarDetect=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.HaarDetection)
                + " yoloPre=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.YoloPreprocess)
                + " yoloInfer=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.YoloInference)
                + " yoloPost=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.YoloPostprocess)
                + " yoloTotal=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.YoloTotal)
                + " byteTrack=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.ByteTrack));

            UnityEngine.Debug.Log(
                "[SALIERI-PERF][RUNTIME] window=" + windowId
                + " animatorIk=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.AnimatorIk)
                + " virtualBody=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.VirtualBody)
                + " llmGenerate=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.LlmGenerate)
                + " ttsSynthesis=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.TtsSynthesis)
                + " wavRead=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.TtsWavRead)
                + " audioClip=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.AudioClipCreate)
                + " btConnect=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.BluetoothConnect)
                + " btWrite=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.BluetoothWrite)
                + " btFlush=" + FormatMetric(snapshot, SalieriRuntimePerformanceMetric.BluetoothFlush));

            long managed = GC.GetTotalMemory(false);
            long monoUsed = Profiler.GetMonoUsedSizeLong();
            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            long totalReserved = Profiler.GetTotalReservedMemoryLong();
            UnityEngine.Debug.Log(
                "[SALIERI-PERF][MEMORY] window=" + windowId
                + " managedBytes=" + managed
                + " monoUsedBytes=" + monoUsed
                + " totalAllocatedBytes=" + totalAllocated
                + " totalReservedBytes=" + totalReserved
                + " gc0Delta=" + (GC.CollectionCount(0) - initialGc0)
                + " gc1Delta=" + (GC.CollectionCount(1) - initialGc1)
                + " gc2Delta=" + (GC.CollectionCount(2) - initialGc2));
        }

        private static string FormatMetric(
            ProbeWindowSnapshot snapshot,
            SalieriRuntimePerformanceMetric metric)
        {
            ProbeMetricSnapshot value = snapshot.Metrics[(int)metric];
            double average = value.Calls <= 0
                ? 0.0
                : SalieriRuntimePerformanceProbe.TicksToMilliseconds(value.TotalTicks)
                    / value.Calls;
            double maximum =
                SalieriRuntimePerformanceProbe.TicksToMilliseconds(value.MaximumTicks);
            return value.Calls + "/" + Format(average) + "/" + Format(maximum)
                + "/" + value.ValueATotal + "/" + value.ValueBTotal;
        }

        private static string Format(double value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private sealed class FrameWindow
        {
            private readonly double[] samples;
            private int count;
            private int next;

            internal int Count => count;

            internal FrameWindow(int capacity)
            {
                samples = new double[capacity];
            }

            internal void Add(double value)
            {
                samples[next] = value;
                next = (next + 1) % samples.Length;
                if (count < samples.Length)
                    count++;
            }

            internal FrameStatistics TakeAndReset()
            {
                if (count == 0)
                    return FrameStatistics.Empty;

                var ordered = new double[count];
                int start = count == samples.Length ? next : 0;
                double sum = 0.0;
                double maximum = 0.0;
                int over33 = 0;
                int over50 = 0;
                int over100 = 0;
                for (int i = 0; i < count; i++)
                {
                    double value = samples[(start + i) % samples.Length];
                    ordered[i] = value;
                    sum += value;
                    maximum = Math.Max(maximum, value);
                    if (value > 33.3) over33++;
                    if (value > 50.0) over50++;
                    if (value > 100.0) over100++;
                }

                Array.Sort(ordered);
                var result = new FrameStatistics(
                    count,
                    sum / count,
                    Percentile(ordered, 0.50),
                    Percentile(ordered, 0.95),
                    Percentile(ordered, 0.99),
                    maximum,
                    over33,
                    over50,
                    over100);
                count = 0;
                next = 0;
                return result;
            }

            private static double Percentile(double[] ordered, double percentile)
            {
                if (ordered == null || ordered.Length == 0)
                    return 0.0;
                int index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
                index = Math.Max(0, Math.Min(ordered.Length - 1, index));
                return ordered[index];
            }
        }

        private readonly struct FrameStatistics
        {
            internal static readonly FrameStatistics Empty =
                new FrameStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0);

            private readonly int count;
            private readonly double average;
            private readonly double median;
            private readonly double p95;
            private readonly double p99;
            private readonly double maximum;
            private readonly int over33;
            private readonly int over50;
            private readonly int over100;

            internal FrameStatistics(
                int count,
                double average,
                double median,
                double p95,
                double p99,
                double maximum,
                int over33,
                int over50,
                int over100)
            {
                this.count = count;
                this.average = average;
                this.median = median;
                this.p95 = p95;
                this.p99 = p99;
                this.maximum = maximum;
                this.over33 = over33;
                this.over50 = over50;
                this.over100 = over100;
            }

            internal string Format()
            {
                return count + "/" +
                    SalieriRuntimePerformanceProbeHost.Format(average) + "/" +
                    SalieriRuntimePerformanceProbeHost.Format(median) + "/" +
                    SalieriRuntimePerformanceProbeHost.Format(p95) + "/" +
                    SalieriRuntimePerformanceProbeHost.Format(p99) + "/" +
                    SalieriRuntimePerformanceProbeHost.Format(maximum) + "/" +
                    over33 + "/" + over50 + "/" + over100;
            }
        }
    }
}
