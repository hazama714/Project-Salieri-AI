// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Threading;

namespace SalieriAI.Sensors.Audio
{
    internal enum WindowsVoskBackendState
    {
        Uninitialized = 0,
        Initializing = 10,
        Ready = 20,
        Starting = 30,
        Running = 40,
        Stopping = 50,
        ShuttingDown = 60,
        Shutdown = 70,
        Error = 80
    }

    internal enum WindowsVoskBackendEventKind
    {
        RecognizerStarted = 10,
        Begin = 20,
        Partial = 30,
        Final = 40,
        End = 50,
        Error = 60
    }

    internal sealed class WindowsVoskBackendEvent
    {
        internal WindowsVoskBackendEventKind Kind { get; }
        internal long Generation { get; }
        internal string Value { get; }

        internal WindowsVoskBackendEvent(
            WindowsVoskBackendEventKind kind,
            long generation,
            string value)
        {
            Kind = kind;
            Generation = generation;
            Value = value ?? string.Empty;
        }
    }

    internal interface IWindowsVoskBackend : IDisposable
    {
        WindowsVoskBackendState State { get; }
        bool IsRunning { get; }
        int WorkerCount { get; }
        int PcmCapacitySamples { get; }
        WindowsVoskPcmOverflowPolicy OverflowPolicy { get; }

        void Initialize();
        bool TryRequestStart(out long generation, out string error);
        void UpdateMainThread();
        bool TryDequeueEvent(out WindowsVoskBackendEvent backendEvent);
        void StopListening();
        bool Shutdown(TimeSpan maximumDrainTime);
    }

    internal sealed class WindowsVoskBackend : IWindowsVoskBackend
    {
        internal const int DefaultPcmCapacitySamples = 16000 * 4;
        internal const int WorkerReadSamples = 4096;

        private readonly object sync = new object();
        private readonly IWindowsVoskModelInstaller modelInstaller;
        private readonly IWindowsVoskNativeApi nativeApi;
        private readonly IWindowsVoskMicrophoneSource microphone;
        private readonly WindowsVoskPcmBuffer pcmBuffer;
        private readonly ConcurrentQueue<WindowsVoskBackendEvent> events =
            new ConcurrentQueue<WindowsVoskBackendEvent>();
        private readonly AutoResetEvent workerSignal = new AutoResetEvent(false);
        private readonly CancellationTokenSource shutdownCancellation =
            new CancellationTokenSource();

        private Thread worker;
        private WindowsVoskBackendState state =
            WindowsVoskBackendState.Uninitialized;
        private long generationSequence;
        private long activeGeneration = -1L;
        private long endingGeneration = -1L;
        private bool pendingStart;
        private bool startWorkerRequested;
        private bool stopWorkerRequested;
        private bool microphoneStartRequested;
        private bool microphoneStopRequested;
        private bool beginSent;
        private bool finalSent;
        private bool endSent;
        private bool disposed;
        private string lastPartial = string.Empty;

        private IntPtr model;
        private IntPtr recognizer;

        internal WindowsVoskBackend(
            IWindowsVoskModelInstaller modelInstaller,
            IWindowsVoskNativeApi nativeApi,
            IWindowsVoskMicrophoneSource microphone,
            int pcmCapacitySamples = DefaultPcmCapacitySamples)
        {
            this.modelInstaller = modelInstaller ??
                throw new ArgumentNullException(nameof(modelInstaller));
            this.nativeApi = nativeApi ??
                throw new ArgumentNullException(nameof(nativeApi));
            this.microphone = microphone ??
                throw new ArgumentNullException(nameof(microphone));
            pcmBuffer = new WindowsVoskPcmBuffer(pcmCapacitySamples);
        }

        public WindowsVoskBackendState State
        {
            get
            {
                lock (sync)
                    return state;
            }
        }

        public bool IsRunning => State == WindowsVoskBackendState.Running;
        public int WorkerCount
        {
            get
            {
                lock (sync)
                    return worker != null && worker.IsAlive ? 1 : 0;
            }
        }
        public int PcmCapacitySamples => pcmBuffer.CapacitySamples;
        public WindowsVoskPcmOverflowPolicy OverflowPolicy =>
            pcmBuffer.OverflowPolicy;

        public void Initialize()
        {
            lock (sync)
            {
                ThrowIfDisposed();
                if (state != WindowsVoskBackendState.Uninitialized)
                    return;

                state = WindowsVoskBackendState.Initializing;
                worker = new Thread(WorkerMain)
                {
                    IsBackground = true,
                    Name = "Salieri-Windows-Vosk"
                };
                worker.Start();
            }
        }

        public bool TryRequestStart(
            out long generation,
            out string error)
        {
            lock (sync)
            {
                generation = activeGeneration;
                error = string.Empty;

                if (disposed ||
                    state == WindowsVoskBackendState.ShuttingDown ||
                    state == WindowsVoskBackendState.Shutdown)
                {
                    error = "Windows Vosk backend is shut down.";
                    return false;
                }

                if (state == WindowsVoskBackendState.Error)
                {
                    error = "Windows Vosk backend is in Error state.";
                    return false;
                }

                if (state == WindowsVoskBackendState.Starting ||
                    state == WindowsVoskBackendState.Running)
                {
                    generation = activeGeneration;
                    return true;
                }

                if (state == WindowsVoskBackendState.Stopping)
                {
                    error = "Windows Vosk backend is stopping.";
                    return false;
                }

                generation = ++generationSequence;
                activeGeneration = generation;
                ResetCycleState();

                if (state == WindowsVoskBackendState.Initializing)
                {
                    pendingStart = true;
                    return true;
                }

                if (state != WindowsVoskBackendState.Ready)
                {
                    error = "Windows Vosk backend is not ready.";
                    activeGeneration = -1L;
                    return false;
                }

                state = WindowsVoskBackendState.Starting;
                startWorkerRequested = true;
                workerSignal.Set();
                return true;
            }
        }

        public void UpdateMainThread()
        {
            bool shouldStartMicrophone;
            bool shouldStopMicrophone;
            lock (sync)
            {
                shouldStartMicrophone = microphoneStartRequested;
                microphoneStartRequested = false;
                shouldStopMicrophone = microphoneStopRequested;
                microphoneStopRequested = false;
            }

            if (shouldStartMicrophone)
            {
                if (!microphone.TryStart(out string error))
                {
                    FailCycle(
                        "WINDOWS_VOSK_MICROPHONE_START_FAILED: " + error);
                }
            }

            WindowsVoskBackendState currentState = State;
            if (currentState == WindowsVoskBackendState.Starting ||
                currentState == WindowsVoskBackendState.Running)
            {
                int before = pcmBuffer.Count;
                microphone.Pump(pcmBuffer);
                if (pcmBuffer.Count > before)
                    workerSignal.Set();

                lock (sync)
                {
                    if (state == WindowsVoskBackendState.Starting &&
                        microphone.HasProducedSamples)
                    {
                        state = WindowsVoskBackendState.Running;
                        events.Enqueue(new WindowsVoskBackendEvent(
                            WindowsVoskBackendEventKind.RecognizerStarted,
                            activeGeneration,
                            string.Empty));
                        workerSignal.Set();
                    }
                }
            }

            if (shouldStopMicrophone ||
                (State == WindowsVoskBackendState.Stopping &&
                 microphone.IsCapturing))
            {
                microphone.Stop();
                QueueEndOnce();
            }
        }

        public bool TryDequeueEvent(
            out WindowsVoskBackendEvent backendEvent)
        {
            return events.TryDequeue(out backendEvent);
        }

        public void StopListening()
        {
            RequestStop(invalidateGeneration: true);
            microphone.Stop();
            QueueEndOnce();
        }

        public bool Shutdown(TimeSpan maximumDrainTime)
        {
            Thread ownedWorker;
            lock (sync)
            {
                if (state == WindowsVoskBackendState.Shutdown)
                    return true;
                if (disposed && state != WindowsVoskBackendState.ShuttingDown)
                    return true;

                state = WindowsVoskBackendState.ShuttingDown;
                pendingStart = false;
                activeGeneration = -1L;
                stopWorkerRequested = true;
                microphoneStopRequested = false;
                ownedWorker = worker;
            }

            microphone.Stop();
            pcmBuffer.Clear();
            shutdownCancellation.Cancel();
            workerSignal.Set();

            bool drained = ownedWorker == null ||
                !ownedWorker.IsAlive ||
                ownedWorker.Join(maximumDrainTime);
            if (drained)
            {
                lock (sync)
                    state = WindowsVoskBackendState.Shutdown;
            }
            return drained;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            Shutdown(TimeSpan.FromMilliseconds(500));
            disposed = true;
            microphone.Dispose();
            if (worker == null || !worker.IsAlive)
            {
                workerSignal.Dispose();
                shutdownCancellation.Dispose();
            }
        }

        private void WorkerMain()
        {
            try
            {
                WindowsVoskModelInstallResult installed =
                    modelInstaller.InstallAndVerify(
                        shutdownCancellation.Token);
                shutdownCancellation.Token.ThrowIfCancellationRequested();

                nativeApi.SetLogLevel(-1);
                IntPtr createdModel = nativeApi.CreateModel(
                    installed.ModelDirectory);
                if (createdModel == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        "vosk_model_new returned null.");
                }

                lock (sync)
                {
                    if (state == WindowsVoskBackendState.ShuttingDown ||
                        shutdownCancellation.IsCancellationRequested)
                    {
                        nativeApi.FreeModel(createdModel);
                        return;
                    }

                    model = createdModel;
                    state = pendingStart
                        ? WindowsVoskBackendState.Starting
                        : WindowsVoskBackendState.Ready;
                    if (pendingStart)
                    {
                        pendingStart = false;
                        startWorkerRequested = true;
                    }
                }

                // A start admitted while model initialization was running
                // must wake the same worker after ownership is committed.
                // AutoResetEvent retains this signal until the first wait.
                if (startWorkerRequested)
                    workerSignal.Set();

                while (!shutdownCancellation.IsCancellationRequested)
                {
                    workerSignal.WaitOne();
                    if (shutdownCancellation.IsCancellationRequested)
                        break;

                    ProcessWorkerCommands();
                    ProcessAvailablePcm();
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown owns this terminal path.
            }
            catch (Exception ex)
            {
                lock (sync)
                {
                    if (state != WindowsVoskBackendState.ShuttingDown)
                        state = WindowsVoskBackendState.Error;
                }
                events.Enqueue(new WindowsVoskBackendEvent(
                    WindowsVoskBackendEventKind.Error,
                    activeGeneration,
                    "WINDOWS_VOSK_INITIALIZE_FAILED: " +
                    ex.GetType().Name + ": " + ex.Message));
            }
            finally
            {
                FreeRecognizerOnWorker();
                if (model != IntPtr.Zero)
                {
                    nativeApi.FreeModel(model);
                    model = IntPtr.Zero;
                }
                lock (sync)
                    state = WindowsVoskBackendState.Shutdown;
            }
        }

        private void ProcessWorkerCommands()
        {
            bool shouldStop;
            bool shouldStart;
            lock (sync)
            {
                shouldStop = stopWorkerRequested;
                stopWorkerRequested = false;
                shouldStart = startWorkerRequested;
                startWorkerRequested = false;
            }

            if (shouldStop)
            {
                FreeRecognizerOnWorker();
                pcmBuffer.Clear();
                lock (sync)
                {
                    if (state != WindowsVoskBackendState.ShuttingDown &&
                        state != WindowsVoskBackendState.Shutdown &&
                        state != WindowsVoskBackendState.Error)
                    {
                        state = WindowsVoskBackendState.Ready;
                    }
                }
            }

            if (!shouldStart)
                return;

            FreeRecognizerOnWorker();
            IntPtr created = nativeApi.CreateRecognizer(
                model,
                WindowsVoskPcmConverter.TargetSampleRate);
            if (created == IntPtr.Zero)
            {
                FailCycle("WINDOWS_VOSK_RECOGNIZER_CREATE_FAILED");
                return;
            }

            recognizer = created;
            lock (sync)
                microphoneStartRequested = true;
        }

        private void ProcessAvailablePcm()
        {
            if (State != WindowsVoskBackendState.Running ||
                recognizer == IntPtr.Zero)
            {
                return;
            }

            short[] chunk = new short[WorkerReadSamples];
            while (State == WindowsVoskBackendState.Running &&
                   pcmBuffer.TryRead(chunk, out int count))
            {
                long generation;
                lock (sync)
                    generation = activeGeneration;

                int accepted = nativeApi.AcceptWaveform(
                    recognizer,
                    chunk,
                    count);
                if (accepted < 0)
                {
                    FailCycle("WINDOWS_VOSK_ACCEPT_WAVEFORM_FAILED");
                    return;
                }

                if (accepted > 0)
                {
                    string text = WindowsVoskJsonText.Extract(
                        nativeApi.GetResult(recognizer),
                        "text");
                    CompleteFinal(generation, text);
                    return;
                }

                string partial = WindowsVoskJsonText.Extract(
                    nativeApi.GetPartialResult(recognizer),
                    "partial");
                if (!string.IsNullOrWhiteSpace(partial) &&
                    !string.Equals(
                        partial,
                        lastPartial,
                        StringComparison.Ordinal))
                {
                    QueueBeginOnce(generation);
                    lastPartial = partial;
                    events.Enqueue(new WindowsVoskBackendEvent(
                        WindowsVoskBackendEventKind.Partial,
                        generation,
                        partial));
                }
            }
        }

        private void CompleteFinal(long generation, string text)
        {
            bool accepted = false;
            lock (sync)
            {
                if (state == WindowsVoskBackendState.Running &&
                    generation == activeGeneration &&
                    !finalSent &&
                    !string.IsNullOrWhiteSpace(text))
                {
                    finalSent = true;
                    endingGeneration = generation;
                    state = WindowsVoskBackendState.Stopping;
                    stopWorkerRequested = true;
                    microphoneStopRequested = true;
                    accepted = true;
                }
            }

            if (!accepted)
            {
                if (string.IsNullOrWhiteSpace(text))
                    FailCycle("WINDOWS_VOSK_EMPTY_FINAL");
                return;
            }

            QueueBeginOnce(generation);
            events.Enqueue(new WindowsVoskBackendEvent(
                WindowsVoskBackendEventKind.Final,
                generation,
                text));
            workerSignal.Set();
        }

        private void FailCycle(string error)
        {
            long generation;
            lock (sync)
            {
                if (state == WindowsVoskBackendState.ShuttingDown ||
                    state == WindowsVoskBackendState.Shutdown)
                {
                    return;
                }

                generation = activeGeneration;
                endingGeneration = generation;
                state = WindowsVoskBackendState.Stopping;
                stopWorkerRequested = true;
                microphoneStopRequested = true;
            }

            events.Enqueue(new WindowsVoskBackendEvent(
                WindowsVoskBackendEventKind.Error,
                generation,
                error));
            workerSignal.Set();
        }

        private void RequestStop(bool invalidateGeneration)
        {
            lock (sync)
            {
                if (state != WindowsVoskBackendState.Starting &&
                    state != WindowsVoskBackendState.Running &&
                    state != WindowsVoskBackendState.Stopping)
                {
                    pendingStart = false;
                    return;
                }

                endingGeneration = activeGeneration;
                state = WindowsVoskBackendState.Stopping;
                stopWorkerRequested = true;
                microphoneStopRequested = true;
                pendingStart = false;
                if (invalidateGeneration)
                    activeGeneration = -1L;
            }
            pcmBuffer.Clear();
            workerSignal.Set();
        }

        private void QueueBeginOnce(long generation)
        {
            lock (sync)
            {
                if (beginSent || generation != activeGeneration)
                    return;
                beginSent = true;
            }
            events.Enqueue(new WindowsVoskBackendEvent(
                WindowsVoskBackendEventKind.Begin,
                generation,
                string.Empty));
        }

        private void QueueEndOnce()
        {
            long generation;
            lock (sync)
            {
                if (endSent || endingGeneration < 0L)
                    return;
                endSent = true;
                generation = endingGeneration;
            }
            events.Enqueue(new WindowsVoskBackendEvent(
                WindowsVoskBackendEventKind.End,
                generation,
                string.Empty));
        }

        private void FreeRecognizerOnWorker()
        {
            IntPtr owned = recognizer;
            recognizer = IntPtr.Zero;
            if (owned != IntPtr.Zero)
                nativeApi.FreeRecognizer(owned);
        }

        private void ResetCycleState()
        {
            pcmBuffer.Clear();
            beginSent = false;
            finalSent = false;
            endSent = false;
            endingGeneration = -1L;
            lastPartial = string.Empty;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(WindowsVoskBackend));
        }
    }

    internal static class WindowsVoskJsonText
    {
        internal static string Extract(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return string.Empty;

            string token = "\"" + key + "\"";
            int index = json.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return string.Empty;

            index = json.IndexOf(':', index + token.Length);
            if (index < 0)
                return string.Empty;
            index++;
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
            if (index >= json.Length || json[index] != '"')
                return string.Empty;
            index++;

            var builder = new StringBuilder();
            while (index < json.Length)
            {
                char value = json[index++];
                if (value == '"')
                    return builder.ToString().Trim();
                if (value != '\\')
                {
                    builder.Append(value);
                    continue;
                }
                if (index >= json.Length)
                    return string.Empty;

                char escaped = json[index++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length)
                            return string.Empty;
                        string hex = json.Substring(index, 4);
                        if (!ushort.TryParse(
                                hex,
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture,
                                out ushort code))
                        {
                            return string.Empty;
                        }
                        builder.Append((char)code);
                        index += 4;
                        break;
                    default:
                        return string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
