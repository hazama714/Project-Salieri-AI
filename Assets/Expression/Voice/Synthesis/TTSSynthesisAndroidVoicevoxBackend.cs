// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.IO;
using System.Threading;

using SalieriAI.Core.Diagnostics.Performance;

namespace SalieriAI.Expression.Voice.Synthesis
{
    /// <summary>
    /// Android bridge call prepared on the Unity Main Thread and invoked once
    /// by the AR1-B background worker. Implementations own JNI thread attach
    /// balance; they do not own wave admission, completion, or release.
    /// </summary>
    public interface IAndroidVoicevoxSynthesisInvoker
    {
        string SynthesizeToFileOnWorkerThread(
            string text,
            int styleId,
            string outputFileName);
    }

    public sealed class TTSSynthesisAndroidVoicevoxBackendMetrics
    {
        private int callCount;
        private int concurrentCount;
        private int maximumConcurrentCount;
        private int lastBackendThreadId;
        private string lastText;
        private int lastStyleId;
        private string lastOutputFileName;

        public int CallCount => Volatile.Read(ref callCount);
        public int MaximumConcurrentCount =>
            Volatile.Read(ref maximumConcurrentCount);
        public int LastBackendThreadId => Volatile.Read(ref lastBackendThreadId);
        public string LastText => Volatile.Read(ref lastText);
        public int LastStyleId => Volatile.Read(ref lastStyleId);
        public string LastOutputFileName => Volatile.Read(ref lastOutputFileName);

        internal void Enter(TTSSynthesisWaveRequest request)
        {
            Interlocked.Increment(ref callCount);
            Volatile.Write(
                ref lastBackendThreadId,
                Thread.CurrentThread.ManagedThreadId);
            Volatile.Write(ref lastText, request.Text);
            Volatile.Write(ref lastStyleId, request.StyleId);
            Volatile.Write(ref lastOutputFileName, request.OutputFileName);
            int concurrent = Interlocked.Increment(ref concurrentCount);
            UpdateMaximum(concurrent);
        }

        internal void Exit()
        {
            Interlocked.Decrement(ref concurrentCount);
        }

        private void UpdateMaximum(int candidate)
        {
            while (true)
            {
                int current = Volatile.Read(ref maximumConcurrentCount);
                if (candidate <= current)
                    return;
                if (Interlocked.CompareExchange(
                    ref maximumConcurrentCount,
                    candidate,
                    current) == current)
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Production adapter from one immutable synthesis request to the existing
    /// synchronous Android VOICEVOX JNI bridge. It owns no queue, retry,
    /// playback, native deployment, terminal mutation, or artifact deletion.
    /// The existing Kotlin runtime retains per-synthesis native cleanup.
    /// </summary>
    public sealed class TTSSynthesisAndroidVoicevoxBackend : ITTSSynthesisBackend
    {
        private readonly IAndroidVoicevoxSynthesisInvoker invoker;
        private readonly TTSSynthesisAndroidVoicevoxBackendMetrics metrics;

        public TTSSynthesisAndroidVoicevoxBackend(
            IAndroidVoicevoxSynthesisInvoker invoker,
            TTSSynthesisAndroidVoicevoxBackendMetrics metrics)
        {
            this.invoker = invoker
                ?? throw new ArgumentNullException(nameof(invoker));
            this.metrics = metrics
                ?? throw new ArgumentNullException(nameof(metrics));
        }

        public TTSSynthesisBackendResult Synthesize(
            TTSSynthesisWaveRequest request,
            CancellationToken managedCancellation)
        {
            if (request == null)
            {
                return Failure(
                    "ANDROID_VOICEVOX_INVALID_REQUEST",
                    "Synthesis request is null.");
            }

            metrics.Enter(request);
            long synthesisStarted =
                SalieriRuntimePerformanceProbe.BeginTtsSynthesis();
            long wavBytes = 0L;
            try
            {
                string path = invoker.SynthesizeToFileOnWorkerThread(
                    request.Text,
                    request.StyleId,
                    request.OutputFileName);
                if (string.IsNullOrWhiteSpace(path))
                {
                    return Failure(
                        "ANDROID_VOICEVOX_EMPTY_PATH",
                        "Android VOICEVOX returned an empty WAV path.");
                }

                FileInfo file = new FileInfo(path);
                if (!file.Exists)
                {
                    return Failure(
                        "ANDROID_VOICEVOX_WAV_NOT_FOUND",
                        "Android VOICEVOX WAV file does not exist: " + path);
                }

                long byteLength = file.Length;
                wavBytes = byteLength;
                if (byteLength <= 0)
                {
                    return Failure(
                        "ANDROID_VOICEVOX_WAV_EMPTY",
                        "Android VOICEVOX WAV file is empty: " + path);
                }

                return new TTSSynthesisBackendResult(
                    true,
                    new TTSSynthesisAudioArtifact(path, byteLength),
                    string.Empty,
                    "Android VOICEVOX synthesis returned a usable WAV artifact.");
            }
            catch (Exception exception)
            {
                return Failure(
                    "ANDROID_VOICEVOX_EXCEPTION_"
                        + exception.GetType().Name,
                    exception.Message);
            }
            finally
            {
                SalieriRuntimePerformanceProbe.EndTtsSynthesis(
                    synthesisStarted,
                    wavBytes);
                metrics.Exit();
            }
        }

        private static TTSSynthesisBackendResult Failure(
            string errorCode,
            string diagnostic)
        {
            return new TTSSynthesisBackendResult(
                false,
                null,
                errorCode,
                diagnostic);
        }
    }

    public sealed class TTSSynthesisAndroidVoicevoxBackendFactory
        : ITTSSynthesisBackendFactory
    {
        private readonly IAndroidVoicevoxSynthesisInvoker invoker;

        public TTSSynthesisAndroidVoicevoxBackendMetrics Metrics { get; }

        public TTSSynthesisAndroidVoicevoxBackendFactory(
            IAndroidVoicevoxSynthesisInvoker invoker,
            TTSSynthesisAndroidVoicevoxBackendMetrics metrics = null)
        {
            this.invoker = invoker
                ?? throw new ArgumentNullException(nameof(invoker));
            Metrics = metrics
                ?? new TTSSynthesisAndroidVoicevoxBackendMetrics();
        }

        public ITTSSynthesisBackend Create(TTSSynthesisWaveRequest request)
        {
            return new TTSSynthesisAndroidVoicevoxBackend(invoker, Metrics);
        }
    }
}
