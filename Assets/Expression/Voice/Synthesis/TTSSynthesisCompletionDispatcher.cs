// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Expression.Voice.Synthesis
{
    public enum TTSSynthesisCompletionDispatchDisposition
    {
        TerminalApplied = 0,
        Released,
        RejectedWrongThread,
        RejectedInvalidInput,
        RejectedStaleResult,
        RejectedInvalidState,
        RejectedAlreadyCompleted
    }

    /// <summary>
    /// Pure diagnostic result for one completion mutation. Artifact handoff
    /// is a correlated reference transfer, not speech publication or playback.
    /// </summary>
    public sealed class TTSSynthesisCompletionDispatchResult
    {
        public TTSSynthesisCompletionDispatchDisposition Disposition { get; }
        public string WaveId { get; }
        public string RequestId { get; }
        public TTSSynthesisWaveState? FinalState { get; }
        public TTSSynthesisWaveResult WaveResult { get; }
        public TTSSynthesisAudioArtifact AudioArtifact { get; }
        public bool WasTerminalApplied { get; }
        public bool WasArtifactHandedOff { get; }
        public bool WasReleased { get; }
        public bool WasPendingPromoted { get; }
        public string PromotedWaveId { get; }
        public string Diagnostic { get; }
        public int WorkerThreadId { get; }
        public int BackendCallThreadId { get; }
        public int CompletionCallerThreadId { get; }
        public int DesignatedMainThreadId { get; }

        public bool Accepted =>
            Disposition == TTSSynthesisCompletionDispatchDisposition.TerminalApplied
            || Disposition == TTSSynthesisCompletionDispatchDisposition.Released;

        internal TTSSynthesisCompletionDispatchResult(
            TTSSynthesisCompletionDispatchDisposition disposition,
            string waveId,
            string requestId,
            TTSSynthesisWaveState? finalState,
            TTSSynthesisWaveResult waveResult,
            TTSSynthesisAudioArtifact audioArtifact,
            bool wasTerminalApplied,
            bool wasArtifactHandedOff,
            bool wasReleased,
            bool wasPendingPromoted,
            string promotedWaveId,
            string diagnostic,
            int workerThreadId,
            int backendCallThreadId,
            int completionCallerThreadId,
            int designatedMainThreadId)
        {
            Disposition = disposition;
            WaveId = waveId;
            RequestId = requestId;
            FinalState = finalState;
            WaveResult = waveResult;
            AudioArtifact = audioArtifact;
            WasTerminalApplied = wasTerminalApplied;
            WasArtifactHandedOff = wasArtifactHandedOff;
            WasReleased = wasReleased;
            WasPendingPromoted = wasPendingPromoted;
            PromotedWaveId = promotedWaveId;
            Diagnostic = diagnostic;
            WorkerThreadId = workerThreadId;
            BackendCallThreadId = backendCallThreadId;
            CompletionCallerThreadId = completionCallerThreadId;
            DesignatedMainThreadId = designatedMainThreadId;
        }
    }

    /// <summary>
    /// Single-wave AR1-B Main Thread completion boundary. ApplyTerminal and
    /// Release are deliberately separate calls so Terminal != Released remains
    /// observable. This class never plays, publishes, or deletes the WAV file.
    /// The handed-off artifact remains owned by the later playback boundary.
    /// </summary>
    public sealed class TTSSynthesisCompletionDispatcher
    {
        private readonly object gate = new object();
        private readonly int designatedMainThreadId;

        private bool terminalApplied;
        private bool released;
        private int terminalApplyCount;
        private int artifactHandoffCount;
        private int releaseCount;
        private int promotionCount;
        private TTSSynthesisWaveResult terminalResult;
        private TTSSynthesisWaveResult releasedResult;
        private TTSSynthesisAudioArtifact handedOffArtifact;
        private int completedWorkerThreadId;
        private int completedBackendThreadId;

        public TTSSynthesisCompletionDispatcher(int designatedMainThreadId)
        {
            if (designatedMainThreadId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(designatedMainThreadId));
            }

            this.designatedMainThreadId = designatedMainThreadId;
        }

        public int DesignatedMainThreadId => designatedMainThreadId;

        public bool TerminalApplied
        {
            get { lock (gate) return terminalApplied; }
        }

        public bool Released
        {
            get { lock (gate) return released; }
        }

        public TTSSynthesisWaveResult TerminalResult
        {
            get { lock (gate) return terminalResult; }
        }

        public TTSSynthesisWaveResult ReleasedResult
        {
            get { lock (gate) return releasedResult; }
        }

        public TTSSynthesisAudioArtifact HandedOffArtifact
        {
            get { lock (gate) return handedOffArtifact; }
        }

        public int TerminalApplyCount
        {
            get { lock (gate) return terminalApplyCount; }
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

        /// <summary>
        /// Applies one worker result to the current active wave. It must be
        /// called on the explicitly designated Main Thread.
        /// </summary>
        public TTSSynthesisCompletionDispatchResult ApplyTerminal(
            TTSSynthesisWaveAdmission admission,
            TTSSynthesisBackgroundWorkResult workerResult,
            DateTime terminalAtUtc)
        {
            int callerThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (callerThreadId != designatedMainThreadId)
            {
                return Reject(
                    TTSSynthesisCompletionDispatchDisposition.RejectedWrongThread,
                    workerResult,
                    "Completion mutation must run on the designated Main Thread.",
                    callerThreadId);
            }

            if (admission == null
                || workerResult == null
                || workerResult.Request == null)
            {
                return Reject(
                    TTSSynthesisCompletionDispatchDisposition.RejectedInvalidInput,
                    workerResult,
                    "Admission and worker result are required.",
                    callerThreadId);
            }

            lock (gate)
            {
                if (terminalApplied)
                {
                    return RejectLocked(
                        TTSSynthesisCompletionDispatchDisposition
                            .RejectedAlreadyCompleted,
                        workerResult,
                        "Terminal was already applied.",
                        callerThreadId);
                }

                TTSSynthesisWaveRequest activeRequest = admission.ActiveRequest;
                if (activeRequest == null
                    || !ReferenceEquals(activeRequest, workerResult.Request)
                    || !IdentityMatches(activeRequest, workerResult))
                {
                    return RejectLocked(
                        TTSSynthesisCompletionDispatchDisposition.RejectedStaleResult,
                        workerResult,
                        "Worker result does not match the current active snapshot.",
                        callerThreadId);
                }

                TTSSynthesisWaveResult mapped = MapTerminal(
                    workerResult,
                    terminalAtUtc);
                if (!admission.TryRecordActiveTerminal(mapped))
                {
                    return RejectLocked(
                        TTSSynthesisCompletionDispatchDisposition.RejectedInvalidState,
                        workerResult,
                        "Admission rejected the terminal transition.",
                        callerThreadId);
                }

                terminalApplied = true;
                terminalApplyCount++;
                terminalResult = mapped;
                completedWorkerThreadId = workerResult.WorkerThreadId;
                completedBackendThreadId = workerResult.BackendCallThreadId;

                bool handoff =
                    mapped.TerminalState == TTSSynthesisWaveState.Succeeded
                    && mapped.AudioArtifact != null
                    && mapped.AudioArtifact.IsUsable;
                if (handoff)
                {
                    artifactHandoffCount++;
                    handedOffArtifact = mapped.AudioArtifact;
                }

                return new TTSSynthesisCompletionDispatchResult(
                    TTSSynthesisCompletionDispatchDisposition.TerminalApplied,
                    mapped.WaveId,
                    mapped.RequestId,
                    mapped.TerminalState,
                    mapped,
                    handoff ? mapped.AudioArtifact : null,
                    true,
                    handoff,
                    false,
                    false,
                    null,
                    handoff
                        ? "Terminal applied; WAV artifact handed off."
                        : "Terminal applied without playback artifact handoff.",
                    workerResult.WorkerThreadId,
                    workerResult.BackendCallThreadId,
                    callerThreadId,
                    designatedMainThreadId);
            }
        }

        /// <summary>
        /// Releases the previously terminal active wave. Only this call may
        /// allow the admission owner to promote its one FIFO pending request.
        /// </summary>
        public TTSSynthesisCompletionDispatchResult Release(
            TTSSynthesisWaveAdmission admission,
            DateTime releasedAtUtc)
        {
            int callerThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (callerThreadId != designatedMainThreadId)
            {
                return Reject(
                    TTSSynthesisCompletionDispatchDisposition.RejectedWrongThread,
                    null,
                    "Release must run on the designated Main Thread.",
                    callerThreadId);
            }

            if (admission == null)
            {
                return Reject(
                    TTSSynthesisCompletionDispatchDisposition.RejectedInvalidInput,
                    null,
                    "Admission is required.",
                    callerThreadId);
            }

            lock (gate)
            {
                if (released)
                {
                    return RejectLocked(
                        TTSSynthesisCompletionDispatchDisposition
                            .RejectedAlreadyCompleted,
                        null,
                        "Release was already applied.",
                        callerThreadId);
                }

                if (!terminalApplied || terminalResult == null)
                {
                    return RejectLocked(
                        TTSSynthesisCompletionDispatchDisposition.RejectedInvalidState,
                        null,
                        "Terminal must be applied before release.",
                        callerThreadId);
                }

                TTSSynthesisWaveResult releasedWave;
                TTSSynthesisWaveRequest promotedRequest;
                if (!admission.TryReleaseActive(
                    releasedAtUtc,
                    out releasedWave,
                    out promotedRequest))
                {
                    return RejectLocked(
                        TTSSynthesisCompletionDispatchDisposition.RejectedInvalidState,
                        null,
                        "Admission rejected release.",
                        callerThreadId);
                }

                released = true;
                releaseCount++;
                releasedResult = releasedWave;
                if (promotedRequest != null)
                {
                    promotionCount++;
                }

                return new TTSSynthesisCompletionDispatchResult(
                    TTSSynthesisCompletionDispatchDisposition.Released,
                    releasedWave.WaveId,
                    releasedWave.RequestId,
                    releasedWave.TerminalState,
                    releasedWave,
                    null,
                    false,
                    false,
                    true,
                    promotedRequest != null,
                    promotedRequest == null ? null : promotedRequest.WaveId,
                    promotedRequest == null
                        ? "Released; no pending wave was present."
                        : "Released; one FIFO pending wave was promoted.",
                    completedWorkerThreadId,
                    completedBackendThreadId,
                    callerThreadId,
                    designatedMainThreadId);
            }
        }

        private TTSSynthesisWaveResult MapTerminal(
            TTSSynthesisBackgroundWorkResult workerResult,
            DateTime terminalAtUtc)
        {
            TTSSynthesisWaveState terminalState;
            TTSSynthesisFailureReason failureReason;
            TTSSynthesisAudioArtifact artifact = null;

            switch (workerResult.Outcome)
            {
                case TTSSynthesisBackgroundWorkOutcome.Succeeded:
                    if (workerResult.IsSuccessful)
                    {
                        terminalState = TTSSynthesisWaveState.Succeeded;
                        failureReason = TTSSynthesisFailureReason.None;
                        artifact = workerResult.AudioArtifact;
                    }
                    else
                    {
                        terminalState = TTSSynthesisWaveState.Failed;
                        failureReason = TTSSynthesisFailureReason.AudioDataInvalid;
                    }
                    break;

                case TTSSynthesisBackgroundWorkOutcome.Cancelled:
                    terminalState = TTSSynthesisWaveState.Cancelled;
                    failureReason = TTSSynthesisFailureReason.Cancelled;
                    break;

                case TTSSynthesisBackgroundWorkOutcome.TimedOut:
                    terminalState = TTSSynthesisWaveState.TimedOut;
                    failureReason = TTSSynthesisFailureReason.TimedOut;
                    break;

                default:
                    terminalState = TTSSynthesisWaveState.Failed;
                    failureReason = workerResult.FailureReason
                        == TTSSynthesisFailureReason.None
                        ? TTSSynthesisFailureReason.Unknown
                        : workerResult.FailureReason;
                    break;
            }

            return new TTSSynthesisWaveResult(
                workerResult.WaveId,
                workerResult.RequestId,
                terminalState,
                failureReason,
                workerResult.NativeErrorCode,
                workerResult.Diagnostic,
                artifact,
                workerResult.Request.RequestedAtUtc,
                null,
                workerResult.StartedAtUtc,
                workerResult.NativeReturnedAtUtc,
                terminalAtUtc,
                null,
                workerResult.ManagedCancellationObserved,
                workerResult.ExecutionDeadlineExceeded);
        }

        private TTSSynthesisCompletionDispatchResult Reject(
            TTSSynthesisCompletionDispatchDisposition disposition,
            TTSSynthesisBackgroundWorkResult workerResult,
            string diagnostic,
            int callerThreadId)
        {
            lock (gate)
            {
                return RejectLocked(
                    disposition,
                    workerResult,
                    diagnostic,
                    callerThreadId);
            }
        }

        private TTSSynthesisCompletionDispatchResult RejectLocked(
            TTSSynthesisCompletionDispatchDisposition disposition,
            TTSSynthesisBackgroundWorkResult workerResult,
            string diagnostic,
            int callerThreadId)
        {
            string waveId = workerResult == null
                ? terminalResult == null ? null : terminalResult.WaveId
                : workerResult.WaveId;
            string requestId = workerResult == null
                ? terminalResult == null ? null : terminalResult.RequestId
                : workerResult.RequestId;

            return new TTSSynthesisCompletionDispatchResult(
                disposition,
                waveId,
                requestId,
                terminalResult == null
                    ? (TTSSynthesisWaveState?)null
                    : terminalResult.TerminalState,
                null,
                null,
                false,
                false,
                false,
                false,
                null,
                diagnostic,
                workerResult == null
                    ? completedWorkerThreadId
                    : workerResult.WorkerThreadId,
                workerResult == null
                    ? completedBackendThreadId
                    : workerResult.BackendCallThreadId,
                callerThreadId,
                designatedMainThreadId);
        }

        private static bool IdentityMatches(
            TTSSynthesisWaveRequest activeRequest,
            TTSSynthesisBackgroundWorkResult workerResult)
        {
            return string.Equals(
                    activeRequest.WaveId,
                    workerResult.WaveId,
                    StringComparison.Ordinal)
                && string.Equals(
                    activeRequest.RequestId,
                    workerResult.RequestId,
                    StringComparison.Ordinal);
        }
    }
}
