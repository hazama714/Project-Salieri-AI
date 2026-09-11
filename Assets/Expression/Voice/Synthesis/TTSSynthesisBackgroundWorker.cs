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
    /// <summary>
    /// Minimal synchronous backend boundary. The cancellation token is a
    /// managed observation only; it does not assert that native work stopped.
    /// Per-synthesis backend resources must be released by the backend before
    /// this call returns or throws.
    /// </summary>
    public interface ITTSSynthesisBackend
    {
        TTSSynthesisBackendResult Synthesize(
            TTSSynthesisWaveRequest request,
            CancellationToken managedCancellation);
    }

    public sealed class TTSSynthesisBackendResult
    {
        public bool Succeeded { get; }
        public TTSSynthesisAudioArtifact AudioArtifact { get; }
        public string ErrorCode { get; }
        public string Diagnostic { get; }

        public TTSSynthesisBackendResult(
            bool succeeded,
            TTSSynthesisAudioArtifact audioArtifact,
            string errorCode,
            string diagnostic)
        {
            Succeeded = succeeded;
            AudioArtifact = audioArtifact;
            ErrorCode = errorCode;
            Diagnostic = diagnostic;
        }
    }

    public enum TTSSynthesisBackgroundWorkerStartDisposition
    {
        Started = 0,
        RejectedInvalidRequest,
        RejectedAlreadyStarted
    }

    public enum TTSSynthesisBackgroundWorkerState
    {
        Created = 0,
        Running,
        Returned
    }

    public enum TTSSynthesisBackgroundWorkOutcome
    {
        Succeeded = 0,
        Failed,
        Cancelled,
        TimedOut
    }

    /// <summary>
    /// Result of background work only. This is not the terminal publication,
    /// admission release, pending promotion, or speech playback result.
    /// </summary>
    public sealed class TTSSynthesisBackgroundWorkResult
    {
        public TTSSynthesisWaveRequest Request { get; }
        public TTSSynthesisBackgroundWorkOutcome Outcome { get; }
        public TTSSynthesisFailureReason FailureReason { get; }
        public TTSSynthesisAudioArtifact AudioArtifact { get; }
        public string NativeErrorCode { get; }
        public string Diagnostic { get; }

        public int CallerThreadId { get; }
        public int WorkerThreadId { get; }
        public int BackendCallThreadId { get; }
        public int DesignatedMainThreadId { get; }

        public DateTime StartedAtUtc { get; }
        public DateTime? NativeReturnedAtUtc { get; }
        public bool ManagedCancellationObserved { get; }
        public bool ExecutionDeadlineExceeded { get; }

        public string WaveId => Request == null ? null : Request.WaveId;
        public string RequestId => Request == null ? null : Request.RequestId;
        public bool BackendReturnObserved => NativeReturnedAtUtc.HasValue;
        public bool IsSuccessful =>
            Outcome == TTSSynthesisBackgroundWorkOutcome.Succeeded
            && FailureReason == TTSSynthesisFailureReason.None
            && AudioArtifact != null
            && AudioArtifact.IsUsable;

        internal TTSSynthesisBackgroundWorkResult(
            TTSSynthesisWaveRequest request,
            TTSSynthesisBackgroundWorkOutcome outcome,
            TTSSynthesisFailureReason failureReason,
            TTSSynthesisAudioArtifact audioArtifact,
            string nativeErrorCode,
            string diagnostic,
            int callerThreadId,
            int workerThreadId,
            int backendCallThreadId,
            int designatedMainThreadId,
            DateTime startedAtUtc,
            DateTime? nativeReturnedAtUtc,
            bool managedCancellationObserved,
            bool executionDeadlineExceeded)
        {
            Request = request;
            Outcome = outcome;
            FailureReason = failureReason;
            AudioArtifact = audioArtifact;
            NativeErrorCode = nativeErrorCode;
            Diagnostic = diagnostic;
            CallerThreadId = callerThreadId;
            WorkerThreadId = workerThreadId;
            BackendCallThreadId = backendCallThreadId;
            DesignatedMainThreadId = designatedMainThreadId;
            StartedAtUtc = startedAtUtc;
            NativeReturnedAtUtc = nativeReturnedAtUtc;
            ManagedCancellationObserved = managedCancellationObserved;
            ExecutionDeadlineExceeded = executionDeadlineExceeded;
        }
    }

    public sealed class TTSSynthesisBackgroundWorkerStartResult
    {
        public TTSSynthesisBackgroundWorkerStartDisposition Disposition { get; }
        public Task<TTSSynthesisBackgroundWorkResult> Completion { get; }

        public bool Started =>
            Disposition == TTSSynthesisBackgroundWorkerStartDisposition.Started;

        internal TTSSynthesisBackgroundWorkerStartResult(
            TTSSynthesisBackgroundWorkerStartDisposition disposition,
            Task<TTSSynthesisBackgroundWorkResult> completion)
        {
            Disposition = disposition;
            Completion = completion;
        }
    }

    /// <summary>
    /// Single-use AR1-B worker. It owns no queue, waiter, admission slot,
    /// terminal publication, release, promotion, playback, or Unity object.
    /// A new worker is created by the later coordinator for each active wave.
    /// </summary>
    public sealed class TTSSynthesisBackgroundWorker
    {
        private readonly object gate = new object();
        private readonly ITTSSynthesisBackend backend;
        private readonly int designatedMainThreadId;

        private bool startClaimed;
        private TTSSynthesisBackgroundWorkerState state;

        public TTSSynthesisBackgroundWorker(
            ITTSSynthesisBackend backend,
            int designatedMainThreadId)
        {
            this.backend = backend
                ?? throw new ArgumentNullException(nameof(backend));
            if (designatedMainThreadId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(designatedMainThreadId));
            }

            this.designatedMainThreadId = designatedMainThreadId;
            state = TTSSynthesisBackgroundWorkerState.Created;
        }

        public TTSSynthesisBackgroundWorkerState State
        {
            get
            {
                lock (gate)
                    return state;
            }
        }

        public bool StartClaimed
        {
            get
            {
                lock (gate)
                    return startClaimed;
            }
        }

        /// <summary>
        /// Claims this worker exactly once and schedules one synchronous
        /// backend call on the managed thread pool. It never waits for a slot.
        /// </summary>
        public TTSSynthesisBackgroundWorkerStartResult TryStart(
            TTSSynthesisWaveRequest request,
            DateTime startedAtUtc,
            Func<DateTime> utcNow,
            CancellationToken managedCancellation)
        {
            if (!IsValid(request) || utcNow == null)
            {
                return Rejected(
                    TTSSynthesisBackgroundWorkerStartDisposition
                        .RejectedInvalidRequest);
            }

            int callerThreadId = Thread.CurrentThread.ManagedThreadId;
            lock (gate)
            {
                if (startClaimed)
                {
                    return Rejected(
                        TTSSynthesisBackgroundWorkerStartDisposition
                            .RejectedAlreadyStarted);
                }

                startClaimed = true;
                state = TTSSynthesisBackgroundWorkerState.Running;
            }

            if (request.CancellationRequested
                || managedCancellation.IsCancellationRequested)
            {
                SetReturned();
                return Started(Task.FromResult(CreatePreStartResult(
                    request,
                    TTSSynthesisBackgroundWorkOutcome.Cancelled,
                    TTSSynthesisFailureReason.Cancelled,
                    "CANCELLED_BEFORE_BACKEND_START",
                    callerThreadId,
                    startedAtUtc,
                    true,
                    false)));
            }

            if (request.ExecutionDeadlineUtc.HasValue
                && startedAtUtc >= request.ExecutionDeadlineUtc.Value)
            {
                SetReturned();
                return Started(Task.FromResult(CreatePreStartResult(
                    request,
                    TTSSynthesisBackgroundWorkOutcome.TimedOut,
                    TTSSynthesisFailureReason.TimedOut,
                    "EXECUTION_DEADLINE_EXCEEDED_BEFORE_START",
                    callerThreadId,
                    startedAtUtc,
                    false,
                    true)));
            }

            Task<TTSSynthesisBackgroundWorkResult> completion = Task.Run(() =>
                Execute(
                    request,
                    callerThreadId,
                    startedAtUtc,
                    utcNow,
                    managedCancellation));
            return Started(completion);
        }

        private TTSSynthesisBackgroundWorkResult Execute(
            TTSSynthesisWaveRequest request,
            int callerThreadId,
            DateTime startedAtUtc,
            Func<DateTime> utcNow,
            CancellationToken managedCancellation)
        {
            int workerThreadId = Thread.CurrentThread.ManagedThreadId;
            int backendThreadId = workerThreadId;
            TTSSynthesisBackendResult backendResult = null;
            Exception backendException = null;

            try
            {
                backendResult = backend.Synthesize(request, managedCancellation);
            }
            catch (Exception exception)
            {
                backendException = exception;
            }

            DateTime nativeReturnedAtUtc = utcNow();
            bool cancellationObserved =
                request.CancellationRequested
                || managedCancellation.IsCancellationRequested;
            bool deadlineExceeded =
                request.ExecutionDeadlineUtc.HasValue
                && nativeReturnedAtUtc >= request.ExecutionDeadlineUtc.Value;

            TTSSynthesisBackgroundWorkResult result;
            if (cancellationObserved)
            {
                result = CreateReturnedResult(
                    request,
                    TTSSynthesisBackgroundWorkOutcome.Cancelled,
                    TTSSynthesisFailureReason.Cancelled,
                    null,
                    "MANAGED_CANCELLATION_OBSERVED_AFTER_RETURN",
                    "Backend returned after managed cancellation.",
                    callerThreadId,
                    workerThreadId,
                    backendThreadId,
                    startedAtUtc,
                    nativeReturnedAtUtc,
                    true,
                    deadlineExceeded);
            }
            else if (deadlineExceeded)
            {
                result = CreateReturnedResult(
                    request,
                    TTSSynthesisBackgroundWorkOutcome.TimedOut,
                    TTSSynthesisFailureReason.TimedOut,
                    null,
                    "EXECUTION_DEADLINE_EXCEEDED_AFTER_RETURN",
                    "Backend returned after the managed execution deadline.",
                    callerThreadId,
                    workerThreadId,
                    backendThreadId,
                    startedAtUtc,
                    nativeReturnedAtUtc,
                    false,
                    true);
            }
            else if (backendException != null)
            {
                result = CreateReturnedResult(
                    request,
                    TTSSynthesisBackgroundWorkOutcome.Failed,
                    TTSSynthesisFailureReason.NativeSynthesisFailed,
                    null,
                    backendException.GetType().Name,
                    backendException.Message,
                    callerThreadId,
                    workerThreadId,
                    backendThreadId,
                    startedAtUtc,
                    nativeReturnedAtUtc,
                    false,
                    false);
            }
            else if (backendResult == null || !backendResult.Succeeded)
            {
                result = CreateReturnedResult(
                    request,
                    TTSSynthesisBackgroundWorkOutcome.Failed,
                    TTSSynthesisFailureReason.NativeSynthesisFailed,
                    null,
                    backendResult == null
                        ? "NULL_BACKEND_RESULT"
                        : backendResult.ErrorCode,
                    backendResult == null
                        ? "Backend returned no result."
                        : backendResult.Diagnostic,
                    callerThreadId,
                    workerThreadId,
                    backendThreadId,
                    startedAtUtc,
                    nativeReturnedAtUtc,
                    false,
                    false);
            }
            else if (backendResult.AudioArtifact == null
                || !backendResult.AudioArtifact.IsUsable)
            {
                result = CreateReturnedResult(
                    request,
                    TTSSynthesisBackgroundWorkOutcome.Failed,
                    TTSSynthesisFailureReason.AudioDataInvalid,
                    null,
                    "INVALID_AUDIO_ARTIFACT",
                    "Backend success did not contain a usable WAV artifact.",
                    callerThreadId,
                    workerThreadId,
                    backendThreadId,
                    startedAtUtc,
                    nativeReturnedAtUtc,
                    false,
                    false);
            }
            else
            {
                result = CreateReturnedResult(
                    request,
                    TTSSynthesisBackgroundWorkOutcome.Succeeded,
                    TTSSynthesisFailureReason.None,
                    backendResult.AudioArtifact,
                    string.Empty,
                    backendResult.Diagnostic,
                    callerThreadId,
                    workerThreadId,
                    backendThreadId,
                    startedAtUtc,
                    nativeReturnedAtUtc,
                    false,
                    false);
            }

            SetReturned();
            return result;
        }

        private TTSSynthesisBackgroundWorkResult CreatePreStartResult(
            TTSSynthesisWaveRequest request,
            TTSSynthesisBackgroundWorkOutcome outcome,
            TTSSynthesisFailureReason failureReason,
            string diagnostic,
            int callerThreadId,
            DateTime startedAtUtc,
            bool cancellationObserved,
            bool deadlineExceeded)
        {
            return new TTSSynthesisBackgroundWorkResult(
                request,
                outcome,
                failureReason,
                null,
                string.Empty,
                diagnostic,
                callerThreadId,
                0,
                0,
                designatedMainThreadId,
                startedAtUtc,
                null,
                cancellationObserved,
                deadlineExceeded);
        }

        private TTSSynthesisBackgroundWorkResult CreateReturnedResult(
            TTSSynthesisWaveRequest request,
            TTSSynthesisBackgroundWorkOutcome outcome,
            TTSSynthesisFailureReason failureReason,
            TTSSynthesisAudioArtifact audioArtifact,
            string nativeErrorCode,
            string diagnostic,
            int callerThreadId,
            int workerThreadId,
            int backendThreadId,
            DateTime startedAtUtc,
            DateTime nativeReturnedAtUtc,
            bool cancellationObserved,
            bool deadlineExceeded)
        {
            return new TTSSynthesisBackgroundWorkResult(
                request,
                outcome,
                failureReason,
                audioArtifact,
                nativeErrorCode,
                diagnostic,
                callerThreadId,
                workerThreadId,
                backendThreadId,
                designatedMainThreadId,
                startedAtUtc,
                nativeReturnedAtUtc,
                cancellationObserved,
                deadlineExceeded);
        }

        private static TTSSynthesisBackgroundWorkerStartResult Started(
            Task<TTSSynthesisBackgroundWorkResult> completion)
        {
            return new TTSSynthesisBackgroundWorkerStartResult(
                TTSSynthesisBackgroundWorkerStartDisposition.Started,
                completion);
        }

        private static TTSSynthesisBackgroundWorkerStartResult Rejected(
            TTSSynthesisBackgroundWorkerStartDisposition disposition)
        {
            return new TTSSynthesisBackgroundWorkerStartResult(disposition, null);
        }

        private void SetReturned()
        {
            lock (gate)
                state = TTSSynthesisBackgroundWorkerState.Returned;
        }

        private static bool IsValid(TTSSynthesisWaveRequest request)
        {
            return request != null
                && !string.IsNullOrWhiteSpace(request.WaveId)
                && !string.IsNullOrWhiteSpace(request.RequestId)
                && !string.IsNullOrWhiteSpace(request.Text)
                && request.StyleId >= 0
                && !string.IsNullOrWhiteSpace(request.OutputFileName);
        }
    }
}
