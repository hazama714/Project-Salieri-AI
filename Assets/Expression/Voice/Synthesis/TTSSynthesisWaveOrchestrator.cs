// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SalieriAI.Expression.Voice.Synthesis
{
    public enum TTSSynthesisOrchestratorLifecycle
    {
        Running = 0,
        ShutdownRequested,
        Draining,
        ShutdownComplete,
        Disposed
    }

    public enum TTSSynthesisShutdownRequestDisposition
    {
        ShutdownStarted = 0,
        ShutdownCompleted,
        AlreadyRequested,
        RejectedDisposed
    }

    public sealed class TTSSynthesisShutdownRequestResult
    {
        public TTSSynthesisShutdownRequestDisposition Disposition { get; }
        public TTSSynthesisOrchestratorLifecycle Lifecycle { get; }
        public bool AdmissionClosed { get; }
        public bool PendingCancelledAndReleased { get; }
        public bool ActiveDrainRequired { get; }
        public string Diagnostic { get; }

        internal TTSSynthesisShutdownRequestResult(
            TTSSynthesisShutdownRequestDisposition disposition,
            TTSSynthesisOrchestratorLifecycle lifecycle,
            bool admissionClosed,
            bool pendingCancelledAndReleased,
            bool activeDrainRequired,
            string diagnostic)
        {
            Disposition = disposition;
            Lifecycle = lifecycle;
            AdmissionClosed = admissionClosed;
            PendingCancelledAndReleased = pendingCancelledAndReleased;
            ActiveDrainRequired = activeDrainRequired;
            Diagnostic = diagnostic;
        }
    }

    public interface ITTSSynthesisBackendFactory
    {
        ITTSSynthesisBackend Create(TTSSynthesisWaveRequest request);
    }

    public enum TTSSynthesisOrchestrationCycleDisposition
    {
        CompletedAndReleased = 0,
        NotReady,
        RejectedWrongThread,
        RejectedNoActiveWave,
        RejectedStaleResult,
        RejectedCompletion
    }

    public sealed class TTSSynthesisOrchestrationCycleResult
    {
        public TTSSynthesisOrchestrationCycleDisposition Disposition { get; }
        public string WaveId { get; }
        public string RequestId { get; }
        public TTSSynthesisCompletionDispatchResult Terminal { get; }
        public TTSSynthesisCompletionDispatchResult Release { get; }
        public bool PromotedWorkerStarted { get; }
        public string PromotedWaveId { get; }
        public string Diagnostic { get; }

        public bool Completed =>
            Disposition
                == TTSSynthesisOrchestrationCycleDisposition.CompletedAndReleased;

        internal TTSSynthesisOrchestrationCycleResult(
            TTSSynthesisOrchestrationCycleDisposition disposition,
            string waveId,
            string requestId,
            TTSSynthesisCompletionDispatchResult terminal,
            TTSSynthesisCompletionDispatchResult release,
            bool promotedWorkerStarted,
            string promotedWaveId,
            string diagnostic)
        {
            Disposition = disposition;
            WaveId = waveId;
            RequestId = requestId;
            Terminal = terminal;
            Release = release;
            PromotedWorkerStarted = promotedWorkerStarted;
            PromotedWaveId = promotedWaveId;
            Diagnostic = diagnostic;
        }
    }

    /// <summary>
    /// AR1-B-only composition of Admission, Background Worker, and Main Thread
    /// Completion. It owns no extra queue, playback, publication, or native
    /// implementation. Call PumpCompletedActive on the designated Main Thread.
    /// </summary>
    public sealed class TTSSynthesisWaveOrchestrator
    {
        private sealed class WaveContext
        {
            public TTSSynthesisWaveRequest Request { get; }
            public CancellationToken ManagedCancellation { get; }
            public TTSSynthesisBackgroundWorker Worker { get; set; }
            public TTSSynthesisBackgroundWorkerStartResult WorkerStart { get; set; }
            public TTSSynthesisCompletionDispatcher Completion { get; set; }
            public bool WorkerStarted { get; set; }

            public WaveContext(
                TTSSynthesisWaveRequest request,
                CancellationToken managedCancellation)
            {
                Request = request;
                ManagedCancellation = managedCancellation;
            }
        }

        private readonly object gate = new object();
        private readonly TTSSynthesisWaveAdmission admission;
        private readonly ITTSSynthesisBackendFactory backendFactory;
        private readonly Func<DateTime> utcNow;
        private readonly Action<string, string, TTSSynthesisAudioArtifact>
            artifactHandoff;
        private readonly Action<string, string, TTSSynthesisAudioArtifact>
            shutdownArtifactCleanupRequired;
        private readonly int designatedMainThreadId;

        private WaveContext active;
        private WaveContext pending;
        private int workerStartCount;
        private int terminalCount;
        private int artifactHandoffCount;
        private int releaseCount;
        private int promotionCount;
        private int maximumActiveCount;
        private int maximumPendingCount;
        private TTSSynthesisOrchestratorLifecycle lifecycle;
        private int admissionCloseCount;
        private int pendingShutdownCancelCount;
        private int pendingShutdownReleaseCount;
        private int shutdownCompletionCount;
        private int shutdownArtifactCleanupRequiredCount;
        private int disposeCount;
        private TTSSynthesisWaveResult lastPendingShutdownResult;

        public TTSSynthesisWaveOrchestrator(
            TTSSynthesisWaveAdmission admission,
            ITTSSynthesisBackendFactory backendFactory,
            Func<DateTime> utcNow,
            int designatedMainThreadId,
            Action<string, string, TTSSynthesisAudioArtifact> artifactHandoff = null,
            Action<string, string, TTSSynthesisAudioArtifact>
                shutdownArtifactCleanupRequired = null)
        {
            this.admission = admission
                ?? throw new ArgumentNullException(nameof(admission));
            this.backendFactory = backendFactory
                ?? throw new ArgumentNullException(nameof(backendFactory));
            this.utcNow = utcNow
                ?? throw new ArgumentNullException(nameof(utcNow));
            if (designatedMainThreadId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(designatedMainThreadId));
            }

            this.designatedMainThreadId = designatedMainThreadId;
            this.artifactHandoff = artifactHandoff;
            this.shutdownArtifactCleanupRequired =
                shutdownArtifactCleanupRequired;
            lifecycle = TTSSynthesisOrchestratorLifecycle.Running;
        }

        public TTSSynthesisWaveAdmission Admission => admission;
        public int DesignatedMainThreadId => designatedMainThreadId;

        public TTSSynthesisOrchestratorLifecycle Lifecycle
        {
            get { lock (gate) return lifecycle; }
        }

        public TTSSynthesisWaveRequest ActiveRequest
        {
            get { lock (gate) return active == null ? null : active.Request; }
        }

        public TTSSynthesisWaveRequest PendingRequest
        {
            get { lock (gate) return pending == null ? null : pending.Request; }
        }

        public Task<TTSSynthesisBackgroundWorkResult> ActiveCompletionTask
        {
            get
            {
                lock (gate)
                {
                    return active == null || active.WorkerStart == null
                        ? null
                        : active.WorkerStart.Completion;
                }
            }
        }

        public int WorkerStartCount
        {
            get { lock (gate) return workerStartCount; }
        }

        public int TerminalCount
        {
            get { lock (gate) return terminalCount; }
        }

        public int ArtifactHandoffCount
        {
            get { lock (gate) return artifactHandoffCount; }
        }

        public int ReleaseCount
        {
            get { lock (gate) return releaseCount; }
        }

        public int PromotionCount
        {
            get { lock (gate) return promotionCount; }
        }

        public int MaximumActiveCount
        {
            get { lock (gate) return maximumActiveCount; }
        }

        public int MaximumPendingCount
        {
            get { lock (gate) return maximumPendingCount; }
        }

        public int AdmissionCloseCount
        {
            get { lock (gate) return admissionCloseCount; }
        }

        public int PendingShutdownCancelCount
        {
            get { lock (gate) return pendingShutdownCancelCount; }
        }

        public int PendingShutdownReleaseCount
        {
            get { lock (gate) return pendingShutdownReleaseCount; }
        }

        public int ShutdownCompletionCount
        {
            get { lock (gate) return shutdownCompletionCount; }
        }

        public int ShutdownArtifactCleanupRequiredCount
        {
            get { lock (gate) return shutdownArtifactCleanupRequiredCount; }
        }

        public int DisposeCount
        {
            get { lock (gate) return disposeCount; }
        }

        public TTSSynthesisWaveResult LastPendingShutdownResult
        {
            get { lock (gate) return lastPendingShutdownResult; }
        }

        public TTSSynthesisAdmissionResult Submit(
            TTSSynthesisWaveRequest request,
            DateTime admittedAtUtc,
            CancellationToken managedCancellation)
        {
            lock (gate)
            {
                if (lifecycle == TTSSynthesisOrchestratorLifecycle.Disposed
                    || lifecycle
                        == TTSSynthesisOrchestratorLifecycle.ShutdownComplete)
                {
                    return TTSSynthesisAdmissionResult.RejectedDisposed;
                }

                if (lifecycle != TTSSynthesisOrchestratorLifecycle.Running)
                    return TTSSynthesisAdmissionResult.RejectedShuttingDown;

                TTSSynthesisAdmissionResult result =
                    admission.Admit(request, admittedAtUtc);
                if (result == TTSSynthesisAdmissionResult.AcceptedActive)
                {
                    active = new WaveContext(request, managedCancellation);
                    UpdateBoundsLocked();
                    StartActiveLocked();
                }
                else if (result == TTSSynthesisAdmissionResult.AcceptedPending)
                {
                    pending = new WaveContext(request, managedCancellation);
                    UpdateBoundsLocked();
                }

                return result;
            }
        }

        /// <summary>
        /// Closes admission immediately and starts a non-blocking drain. A
        /// pending wave is terminal-cancelled and released without starting a
        /// backend. An active backend is never stopped or released here.
        /// </summary>
        public TTSSynthesisShutdownRequestResult RequestShutdown(
            DateTime requestedAtUtc)
        {
            lock (gate)
            {
                if (lifecycle == TTSSynthesisOrchestratorLifecycle.Disposed)
                {
                    return ShutdownResultLocked(
                        TTSSynthesisShutdownRequestDisposition.RejectedDisposed,
                        false,
                        false,
                        false,
                        "Orchestrator is disposed.");
                }

                if (lifecycle != TTSSynthesisOrchestratorLifecycle.Running)
                {
                    return ShutdownResultLocked(
                        TTSSynthesisShutdownRequestDisposition.AlreadyRequested,
                        false,
                        false,
                        active != null,
                        "Shutdown was already requested.");
                }

                lifecycle = TTSSynthesisOrchestratorLifecycle.ShutdownRequested;
                admission.RequestShutdown();
                admissionCloseCount++;

                bool pendingCancelled = false;
                if (pending != null)
                {
                    TTSSynthesisWaveResult cancelled;
                    pendingCancelled = admission.TryCancelPending(
                        pending.Request.WaveId,
                        pending.Request.RequestId,
                        requestedAtUtc,
                        out cancelled);
                    if (!pendingCancelled)
                    {
                        throw new InvalidOperationException(
                            "Admission rejected shutdown cancellation of the "
                            + "matching pending wave.");
                    }

                    lastPendingShutdownResult = cancelled;
                    pending = null;
                    pendingShutdownCancelCount++;
                    pendingShutdownReleaseCount++;
                }

                if (active == null)
                {
                    CompleteShutdownLocked();
                    return ShutdownResultLocked(
                        TTSSynthesisShutdownRequestDisposition.ShutdownCompleted,
                        true,
                        pendingCancelled,
                        false,
                        "Idle shutdown completed without a drain.");
                }

                lifecycle = TTSSynthesisOrchestratorLifecycle.Draining;
                return ShutdownResultLocked(
                    TTSSynthesisShutdownRequestDisposition.ShutdownStarted,
                    true,
                    pendingCancelled,
                    true,
                    "Admission closed; active backend is draining asynchronously.");
            }
        }

        /// <summary>
        /// Disposes only after shutdown has fully completed. It never waits
        /// for a backend and never forces slot cleanup.
        /// </summary>
        public bool TryDispose()
        {
            lock (gate)
            {
                if (lifecycle == TTSSynthesisOrchestratorLifecycle.Disposed)
                    return false;

                if (lifecycle
                    != TTSSynthesisOrchestratorLifecycle.ShutdownComplete)
                {
                    return false;
                }

                admission.Dispose();
                lifecycle = TTSSynthesisOrchestratorLifecycle.Disposed;
                disposeCount++;
                return true;
            }
        }

        /// <summary>
        /// Duplicate start triggers are explicitly rejected. Submission and
        /// release-promotion are the only automatic start boundaries.
        /// </summary>
        public bool TryStartCurrentActive()
        {
            lock (gate)
                return StartActiveLocked();
        }

        /// <summary>
        /// Non-blocking Main Thread pump. Worker return alone never mutates
        /// admission and never starts the pending wave.
        /// </summary>
        public TTSSynthesisOrchestrationCycleResult PumpCompletedActive(
            DateTime terminalAtUtc,
            DateTime releasedAtUtc)
        {
            int callerThreadId = Thread.CurrentThread.ManagedThreadId;
            if (callerThreadId != designatedMainThreadId)
            {
                return CycleReject(
                    TTSSynthesisOrchestrationCycleDisposition.RejectedWrongThread,
                    null,
                    "Pump must run on the designated Main Thread.");
            }

            lock (gate)
            {
                if (active == null
                    || active.WorkerStart == null
                    || active.WorkerStart.Completion == null)
                {
                    return CycleRejectLocked(
                        TTSSynthesisOrchestrationCycleDisposition
                            .RejectedNoActiveWave,
                        null,
                        "No active worker exists.");
                }

                if (!active.WorkerStart.Completion.IsCompleted)
                {
                    return CycleRejectLocked(
                        TTSSynthesisOrchestrationCycleDisposition.NotReady,
                        null,
                        "Active background work has not returned.");
                }

                TTSSynthesisBackgroundWorkResult workerResult =
                    active.WorkerStart.Completion.GetAwaiter().GetResult();
                return CompleteWorkerResultLocked(
                    workerResult,
                    terminalAtUtc,
                    releasedAtUtc);
            }
        }

        /// <summary>
        /// Explicit callback form used to reject duplicate or stale results.
        /// It has the same designated Main Thread requirement as Pump.
        /// </summary>
        public TTSSynthesisOrchestrationCycleResult CompleteWorkerResult(
            TTSSynthesisBackgroundWorkResult workerResult,
            DateTime terminalAtUtc,
            DateTime releasedAtUtc)
        {
            if (Thread.CurrentThread.ManagedThreadId != designatedMainThreadId)
            {
                return CycleReject(
                    TTSSynthesisOrchestrationCycleDisposition.RejectedWrongThread,
                    workerResult,
                    "Completion must run on the designated Main Thread.");
            }

            lock (gate)
                return CompleteWorkerResultLocked(
                    workerResult,
                    terminalAtUtc,
                    releasedAtUtc);
        }

        private TTSSynthesisOrchestrationCycleResult CompleteWorkerResultLocked(
            TTSSynthesisBackgroundWorkResult workerResult,
            DateTime terminalAtUtc,
            DateTime releasedAtUtc)
        {
            if (active == null || workerResult == null)
            {
                return CycleRejectLocked(
                    TTSSynthesisOrchestrationCycleDisposition.RejectedNoActiveWave,
                    workerResult,
                    "No active wave can accept the result.");
            }

            if (!ReferenceEquals(active.Request, workerResult.Request)
                || !IdentityMatches(active.Request, workerResult))
            {
                return CycleRejectLocked(
                    TTSSynthesisOrchestrationCycleDisposition.RejectedStaleResult,
                    workerResult,
                    "Worker result does not match the active wave.");
            }

            TTSSynthesisCompletionDispatchResult terminal =
                active.Completion.ApplyTerminal(
                    admission,
                    workerResult,
                    terminalAtUtc);
            if (!terminal.Accepted)
            {
                return CycleRejectLocked(
                    TTSSynthesisOrchestrationCycleDisposition.RejectedCompletion,
                    workerResult,
                    terminal.Diagnostic);
            }

            terminalCount++;
            bool suppressArtifactHandoff =
                lifecycle != TTSSynthesisOrchestratorLifecycle.Running
                && terminal.WasArtifactHandedOff;
            if (suppressArtifactHandoff)
            {
                shutdownArtifactCleanupRequiredCount++;
                shutdownArtifactCleanupRequired?.Invoke(
                    terminal.WaveId,
                    terminal.RequestId,
                    terminal.AudioArtifact);
                terminal = SuppressArtifactHandoff(terminal);
            }
            else if (terminal.WasArtifactHandedOff)
            {
                artifactHandoffCount++;
                artifactHandoff?.Invoke(
                    terminal.WaveId,
                    terminal.RequestId,
                    terminal.AudioArtifact);
            }

            TTSSynthesisCompletionDispatchResult release =
                active.Completion.Release(admission, releasedAtUtc);
            if (!release.Accepted)
            {
                return CycleRejectLocked(
                    TTSSynthesisOrchestrationCycleDisposition.RejectedCompletion,
                    workerResult,
                    release.Diagnostic);
            }

            releaseCount++;
            string completedWaveId = active.Request.WaveId;
            string completedRequestId = active.Request.RequestId;
            active = null;

            bool promotedWorkerStarted = false;
            string promotedWaveId = null;
            if (release.WasPendingPromoted)
            {
                if (lifecycle != TTSSynthesisOrchestratorLifecycle.Running)
                {
                    throw new InvalidOperationException(
                        "Admission promoted a pending wave after shutdown close.");
                }

                promotionCount++;
                if (pending == null
                    || admission.ActiveRequest == null
                    || !ReferenceEquals(
                        pending.Request,
                        admission.ActiveRequest))
                {
                    return new TTSSynthesisOrchestrationCycleResult(
                        TTSSynthesisOrchestrationCycleDisposition
                            .RejectedCompletion,
                        completedWaveId,
                        completedRequestId,
                        terminal,
                        release,
                        false,
                        null,
                        "Promoted admission request has no matching context.");
                }

                active = pending;
                pending = null;
                promotedWaveId = active.Request.WaveId;
                UpdateBoundsLocked();
                promotedWorkerStarted = StartActiveLocked();
            }

            if (lifecycle == TTSSynthesisOrchestratorLifecycle.Draining
                || lifecycle
                    == TTSSynthesisOrchestratorLifecycle.ShutdownRequested)
            {
                CompleteShutdownLocked();
            }

            return new TTSSynthesisOrchestrationCycleResult(
                TTSSynthesisOrchestrationCycleDisposition.CompletedAndReleased,
                completedWaveId,
                completedRequestId,
                terminal,
                release,
                promotedWorkerStarted,
                promotedWaveId,
                promotedWorkerStarted
                    ? "Wave completed; pending wave promoted and started."
                    : "Wave completed and released.");
        }

        private bool StartActiveLocked()
        {
            if (lifecycle != TTSSynthesisOrchestratorLifecycle.Running
                || active == null
                || active.WorkerStarted)
                return false;

            ITTSSynthesisBackend backend = backendFactory.Create(active.Request);
            if (backend == null)
                throw new InvalidOperationException("Backend factory returned null.");

            active.Worker = new TTSSynthesisBackgroundWorker(
                backend,
                designatedMainThreadId);
            active.Completion = new TTSSynthesisCompletionDispatcher(
                designatedMainThreadId);
            active.WorkerStart = active.Worker.TryStart(
                active.Request,
                utcNow(),
                utcNow,
                active.ManagedCancellation);
            if (!active.WorkerStart.Started)
            {
                throw new InvalidOperationException(
                    "Active wave worker start was rejected: "
                    + active.WorkerStart.Disposition);
            }

            active.WorkerStarted = true;
            workerStartCount++;
            return true;
        }

        private void UpdateBoundsLocked()
        {
            int activeCount = active == null ? 0 : 1;
            int pendingCount = pending == null ? 0 : 1;
            if (activeCount > maximumActiveCount)
                maximumActiveCount = activeCount;
            if (pendingCount > maximumPendingCount)
                maximumPendingCount = pendingCount;
        }

        private TTSSynthesisOrchestrationCycleResult CycleReject(
            TTSSynthesisOrchestrationCycleDisposition disposition,
            TTSSynthesisBackgroundWorkResult workerResult,
            string diagnostic)
        {
            lock (gate)
                return CycleRejectLocked(disposition, workerResult, diagnostic);
        }

        private TTSSynthesisOrchestrationCycleResult CycleRejectLocked(
            TTSSynthesisOrchestrationCycleDisposition disposition,
            TTSSynthesisBackgroundWorkResult workerResult,
            string diagnostic)
        {
            return new TTSSynthesisOrchestrationCycleResult(
                disposition,
                workerResult == null ? null : workerResult.WaveId,
                workerResult == null ? null : workerResult.RequestId,
                null,
                null,
                false,
                null,
                diagnostic);
        }

        private static bool IdentityMatches(
            TTSSynthesisWaveRequest request,
            TTSSynthesisBackgroundWorkResult result)
        {
            return string.Equals(
                    request.WaveId,
                    result.WaveId,
                    StringComparison.Ordinal)
                && string.Equals(
                    request.RequestId,
                    result.RequestId,
                    StringComparison.Ordinal);
        }

        private void CompleteShutdownLocked()
        {
            if (lifecycle == TTSSynthesisOrchestratorLifecycle.ShutdownComplete
                || lifecycle == TTSSynthesisOrchestratorLifecycle.Disposed)
            {
                return;
            }

            if (active != null || pending != null)
            {
                throw new InvalidOperationException(
                    "Shutdown cannot complete while a wave slot remains owned.");
            }

            lifecycle = TTSSynthesisOrchestratorLifecycle.ShutdownComplete;
            shutdownCompletionCount++;
        }

        private TTSSynthesisShutdownRequestResult ShutdownResultLocked(
            TTSSynthesisShutdownRequestDisposition disposition,
            bool admissionClosed,
            bool pendingCancelledAndReleased,
            bool activeDrainRequired,
            string diagnostic)
        {
            return new TTSSynthesisShutdownRequestResult(
                disposition,
                lifecycle,
                admissionClosed,
                pendingCancelledAndReleased,
                activeDrainRequired,
                diagnostic);
        }

        private static TTSSynthesisCompletionDispatchResult
            SuppressArtifactHandoff(
                TTSSynthesisCompletionDispatchResult terminal)
        {
            return new TTSSynthesisCompletionDispatchResult(
                terminal.Disposition,
                terminal.WaveId,
                terminal.RequestId,
                terminal.FinalState,
                terminal.WaveResult,
                null,
                terminal.WasTerminalApplied,
                false,
                terminal.WasReleased,
                terminal.WasPendingPromoted,
                terminal.PromotedWaveId,
                "Terminal applied; shutdown suppressed normal artifact handoff.",
                terminal.WorkerThreadId,
                terminal.BackendCallThreadId,
                terminal.CompletionCallerThreadId,
                terminal.DesignatedMainThreadId);
        }
    }
}
