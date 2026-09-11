// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Input;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif

namespace SalieriAI.Sensors.Audio
{
    internal interface IWindowsDictationBackend : IDisposable
    {
        event Action<string> Hypothesis;
        event Action<string> Result;
        event Action<string> Completed;
        event Action<string> Error;

        bool IsRunning { get; }

        bool TryRequestStart(out string error);
        void CancelPendingStart();
        void Stop();
    }

    internal enum WindowsSTTLifecycleState
    {
        Stopped = 0,
        Starting = 10,
        Running = 20,
        Stopping = 30,
        Shutdown = 40
    }

    /// <summary>
    /// Windows Editor / Standalone implementation of the existing STTService
    /// provider boundary. Unity's DictationRecognizer owns microphone capture
    /// and Windows speech recognition. Final text still enters through the
    /// existing AndroidSTTReceiver compatibility host and UserSpeechRouter.
    /// </summary>
    public sealed class WindowsEditorSTTService : STTService
    {
        internal const double DefaultStartupTimeoutSeconds = 5.0;

        private readonly IWindowsDictationBackend injectedBackend;
        private readonly Func<double> nowSeconds;
        private readonly double startupTimeoutSeconds;
        private IWindowsDictationBackend backend;
        private AndroidSTTReceiver receiver;
        private WindowsSTTLifecycleState lifecycleState;
        private double startupRequestedAtSeconds;
        private double startupDeadlineSeconds;
        private int startupGeneration;
        private int confirmedStartupGeneration = -1;
        private bool beginReported;
        private bool finalReported;

        public STTProviderKind ProviderKind =>
            STTProviderKind.WindowsDictation;

        public UserInputSource ResultSource => UserInputSource.LocalSTT;

        internal WindowsSTTLifecycleState LifecycleState => lifecycleState;

        public WindowsEditorSTTService()
            : this(null, null, DefaultStartupTimeoutSeconds)
        {
        }

        internal WindowsEditorSTTService(
            IWindowsDictationBackend backendForTesting,
            Func<double> nowSecondsForTesting = null,
            double startupTimeoutSecondsForTesting =
                DefaultStartupTimeoutSeconds)
        {
            injectedBackend = backendForTesting;
            nowSeconds = nowSecondsForTesting ??
                (() => UnityEngine.Time.realtimeSinceStartupAsDouble);
            startupTimeoutSeconds = Math.Max(
                0.001,
                startupTimeoutSecondsForTesting);
            lifecycleState = WindowsSTTLifecycleState.Stopped;
        }

        public void Initialize(AndroidSTTReceiver targetReceiver)
        {
            if (receiver != null || backend != null)
                return;

            receiver = targetReceiver;
            lifecycleState = WindowsSTTLifecycleState.Stopped;

            if (receiver == null)
                return;

            try
            {
                backend = injectedBackend ?? CreateRuntimeBackend();
                if (backend == null)
                    return;

                backend.Hypothesis += HandleHypothesis;
                backend.Result += HandleResult;
                backend.Completed += HandleCompleted;
                backend.Error += HandleError;
            }
            catch (Exception ex)
            {
                backend = null;
                receiver.OnSttError(
                    "WINDOWS_STT_INITIALIZE_FAILED: " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        public STTStartResult StartListening()
        {
            if (lifecycleState == WindowsSTTLifecycleState.Shutdown)
            {
                return STTStartResult.Rejected(
                    ProviderKind,
                    "Windows STT service is shut down.");
            }

            if (receiver == null || backend == null)
            {
                return STTStartResult.Rejected(
                    ProviderKind,
                    "Windows DictationRecognizer is unavailable.");
            }

            if (lifecycleState == WindowsSTTLifecycleState.Running)
                return STTStartResult.AlreadyRunning(ProviderKind);

            if (lifecycleState == WindowsSTTLifecycleState.Starting)
                return STTStartResult.ControlAccepted(ProviderKind);

            if (lifecycleState == WindowsSTTLifecycleState.Stopping)
            {
                return STTStartResult.Rejected(
                    ProviderKind,
                    "Windows DictationRecognizer is stopping.");
            }

            // A stale native transition must never be adopted as a new
            // managed cycle. Stop it before beginning a new explicit wave.
            if (backend.IsRunning)
                backend.Stop();

            beginReported = false;
            finalReported = false;

            if (!backend.TryRequestStart(out string error))
            {
                return STTStartResult.Rejected(
                    ProviderKind,
                    string.IsNullOrWhiteSpace(error)
                        ? "Windows DictationRecognizer did not start."
                        : error);
            }

            startupGeneration++;
            startupRequestedAtSeconds = nowSeconds();
            startupDeadlineSeconds =
                startupRequestedAtSeconds + startupTimeoutSeconds;
            lifecycleState = WindowsSTTLifecycleState.Starting;

            UnityEngine.Debug.Log(
                "[STT][WINDOWS] Start requested. State=Starting " +
                "generation=" + startupGeneration +
                " timeoutSeconds=" + startupTimeoutSeconds);

            // Start() acceptance is not proof that the Windows native
            // recognizer has reached Running. UpdateLifecycle or a native
            // callback performs that confirmation later.
            return STTStartResult.ControlAccepted(ProviderKind);
        }

        public void UpdateLifecycle()
        {
            if (backend == null ||
                lifecycleState == WindowsSTTLifecycleState.Shutdown)
            {
                return;
            }

            if (lifecycleState == WindowsSTTLifecycleState.Starting)
            {
                if (backend.IsRunning)
                {
                    ConfirmRunning();
                    return;
                }

                if (nowSeconds() >= startupDeadlineSeconds)
                    FailStartupTimeout();

                return;
            }

            if (lifecycleState == WindowsSTTLifecycleState.Stopped &&
                backend.IsRunning)
            {
                // A late native transition after Stop/timeout is stale. It
                // must not resurrect listening or publish a startup event.
                UnityEngine.Debug.LogWarning(
                    "[STT][WINDOWS] Ignoring stale Running transition " +
                    "after managed Stop.");
                backend.Stop();
            }
        }

        public void StopListening()
        {
            StopCurrentCycle(notifyReceiver: true);
        }

        public void Shutdown()
        {
            if (lifecycleState == WindowsSTTLifecycleState.Shutdown)
                return;

            StopCurrentCycle(notifyReceiver: false);
            lifecycleState = WindowsSTTLifecycleState.Shutdown;

            if (backend != null)
            {
                backend.Hypothesis -= HandleHypothesis;
                backend.Result -= HandleResult;
                backend.Completed -= HandleCompleted;
                backend.Error -= HandleError;
                backend.Dispose();
                backend = null;
            }

            receiver = null;
        }

        private void HandleHypothesis(string text)
        {
            if (!EnsureRunningFromCallback() || receiver == null)
                return;

            if (!beginReported)
            {
                beginReported = true;
                receiver.OnSttBegin(string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(text))
                receiver.OnSttPartial(text);
        }

        private void HandleResult(string text)
        {
            if (!EnsureRunningFromCallback() ||
                finalReported || receiver == null)
                return;

            if (string.IsNullOrWhiteSpace(text))
            {
                HandleError("WINDOWS_STT_EMPTY_RESULT");
                return;
            }

            finalReported = true;
            receiver.OnSttResult(text);
            StopCurrentCycle(notifyReceiver: true);
        }

        private void HandleCompleted(string reason)
        {
            if (lifecycleState != WindowsSTTLifecycleState.Starting &&
                lifecycleState != WindowsSTTLifecycleState.Running)
                return;

            if (string.Equals(
                    reason,
                    "Complete",
                    StringComparison.Ordinal) ||
                string.Equals(
                    reason,
                    "Canceled",
                    StringComparison.Ordinal))
            {
                StopCurrentCycle(notifyReceiver: true);
                return;
            }

            HandleError(
                "WINDOWS_STT_COMPLETED_WITHOUT_RESULT: " +
                (string.IsNullOrWhiteSpace(reason)
                    ? "Unknown"
                    : reason));
        }

        private void HandleError(string error)
        {
            if ((lifecycleState != WindowsSTTLifecycleState.Starting &&
                 lifecycleState != WindowsSTTLifecycleState.Running) ||
                receiver == null)
            {
                return;
            }

            WindowsSTTLifecycleState previous = lifecycleState;
            lifecycleState = WindowsSTTLifecycleState.Stopping;
            if (previous == WindowsSTTLifecycleState.Starting)
                backend?.CancelPendingStart();
            else
                backend?.Stop();
            lifecycleState = WindowsSTTLifecycleState.Stopped;
            receiver.OnSttError(
                string.IsNullOrWhiteSpace(error)
                    ? "WINDOWS_STT_ERROR"
                    : error);
        }

        private void StopCurrentCycle(bool notifyReceiver)
        {
            WindowsSTTLifecycleState previous = lifecycleState;
            bool wasActive =
                previous == WindowsSTTLifecycleState.Starting ||
                previous == WindowsSTTLifecycleState.Running ||
                (backend != null && backend.IsRunning);

            if (backend != null &&
                lifecycleState != WindowsSTTLifecycleState.Shutdown)
            {
                lifecycleState = WindowsSTTLifecycleState.Stopping;
                UnityEngine.Debug.Log(
                    "[STT][WINDOWS] Stop requested. previousState=" +
                    previous);

                if (previous == WindowsSTTLifecycleState.Starting)
                    backend.CancelPendingStart();
                else
                    backend.Stop();

                lifecycleState = WindowsSTTLifecycleState.Stopped;
            }

            if (wasActive && notifyReceiver && receiver != null)
                receiver.OnSttEnd(string.Empty);
        }

        private bool EnsureRunningFromCallback()
        {
            if (lifecycleState == WindowsSTTLifecycleState.Starting)
                ConfirmRunning();

            return lifecycleState == WindowsSTTLifecycleState.Running;
        }

        private void ConfirmRunning()
        {
            if (lifecycleState != WindowsSTTLifecycleState.Starting ||
                confirmedStartupGeneration == startupGeneration)
            {
                return;
            }

            confirmedStartupGeneration = startupGeneration;
            lifecycleState = WindowsSTTLifecycleState.Running;
            double startupMilliseconds = Math.Max(
                0.0,
                (nowSeconds() - startupRequestedAtSeconds) * 1000.0);

            UnityEngine.Debug.Log(
                "[STT][WINDOWS] State=Running startupMs=" +
                startupMilliseconds.ToString("F1") +
                " generation=" + startupGeneration);

            receiver?.OnSttRecognizerStarted(ProviderKind);
        }

        private void FailStartupTimeout()
        {
            if (lifecycleState != WindowsSTTLifecycleState.Starting)
                return;

            UnityEngine.Debug.LogWarning(
                "[STT][WINDOWS] Startup timeout. timeoutSeconds=" +
                startupTimeoutSeconds +
                " generation=" + startupGeneration);

            lifecycleState = WindowsSTTLifecycleState.Stopping;
            backend?.CancelPendingStart();
            lifecycleState = WindowsSTTLifecycleState.Stopped;
            receiver?.OnSttError(
                "WINDOWS_STT_STARTUP_TIMEOUT after " +
                startupTimeoutSeconds + " seconds.");
        }

        private static IWindowsDictationBackend CreateRuntimeBackend()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return new UnityWindowsDictationBackend();
#else
            return null;
#endif
        }
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    internal sealed class UnityWindowsDictationBackend :
        IWindowsDictationBackend
    {
        private DictationRecognizer recognizer;

        public event Action<string> Hypothesis;
        public event Action<string> Result;
        public event Action<string> Completed;
        public event Action<string> Error;

        public bool IsRunning =>
            recognizer != null &&
            recognizer.Status == SpeechSystemStatus.Running;

        internal UnityWindowsDictationBackend()
        {
            CreateRecognizer();
        }

        public bool TryRequestStart(out string error)
        {
            error = string.Empty;
            if (recognizer == null)
            {
                error = "DictationRecognizer is disposed.";
                return false;
            }

            if (IsRunning)
                return true;

            try
            {
                recognizer.Start();

                // DictationRecognizer.Start is an accepted native startup
                // request, not a synchronous proof of Running. The service
                // observes Status on later Unity main-thread lifecycle ticks.
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public void CancelPendingStart()
        {
            // Dispose/recreate invalidates a pending native startup so it
            // cannot transition to Running after the managed cycle stopped.
            DisposeRecognizer();
            CreateRecognizer();
        }

        private void CreateRecognizer()
        {
            recognizer = new DictationRecognizer(
                ConfidenceLevel.Medium,
                DictationTopicConstraint.Dictation);
            recognizer.DictationHypothesis += HandleHypothesis;
            recognizer.DictationResult += HandleResult;
            recognizer.DictationComplete += HandleComplete;
            recognizer.DictationError += HandleError;
        }

        public void Stop()
        {
            if (recognizer == null || !IsRunning)
                return;

            try
            {
                recognizer.Stop();
            }
            catch (Exception)
            {
                // Lifecycle owner still marks the managed cycle stopped.
            }
        }

        public void Dispose()
        {
            DisposeRecognizer();
        }

        private void DisposeRecognizer()
        {
            if (recognizer == null)
                return;

            DictationRecognizer current = recognizer;
            recognizer = null;

            try
            {
                if (current.Status == SpeechSystemStatus.Running)
                    current.Stop();
            }
            catch (Exception)
            {
                // Disposal below remains the authoritative cancellation.
            }

            current.DictationHypothesis -= HandleHypothesis;
            current.DictationResult -= HandleResult;
            current.DictationComplete -= HandleComplete;
            current.DictationError -= HandleError;

            try
            {
                current.Dispose();
            }
            catch (Exception)
            {
                // The managed owner is invalidated even if Windows reports
                // an error while tearing down an in-flight start.
            }
        }

        private void HandleHypothesis(string text)
        {
            Hypothesis?.Invoke(text);
        }

        private void HandleResult(string text, ConfidenceLevel confidence)
        {
            Result?.Invoke(text);
        }

        private void HandleComplete(DictationCompletionCause cause)
        {
            Completed?.Invoke(cause.ToString());
        }

        private void HandleError(string error, int hresult)
        {
            Error?.Invoke(error + " hresult=" + hresult);
        }
    }
#endif
}
