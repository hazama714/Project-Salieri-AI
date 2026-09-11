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
    /// <summary>
    /// Immutable snapshot of every value used by one Android VOICEVOX
    /// synthesis request. The future worker must not read mutable host or
    /// Inspector fields after admission.
    /// </summary>
    public sealed class TTSSynthesisWaveRequest
    {
        public string WaveId { get; }
        public string RequestId { get; }
        public string Text { get; }
        public int StyleId { get; }
        public string OutputFileName { get; }
        public DateTime RequestedAtUtc { get; }
        public DateTime? AdmissionDeadlineUtc { get; }
        public DateTime? ExecutionDeadlineUtc { get; }

        /// <summary>
        /// Captures cancellation already requested at submission. It is a
        /// managed intent and never asserts that native synthesis stopped.
        /// </summary>
        public bool CancellationRequested { get; }

        public bool HasAdmissionDeadline => AdmissionDeadlineUtc.HasValue;
        public bool HasExecutionDeadline => ExecutionDeadlineUtc.HasValue;

        public TTSSynthesisWaveRequest(
            string waveId,
            string requestId,
            string text,
            int styleId,
            string outputFileName,
            DateTime requestedAtUtc,
            DateTime? admissionDeadlineUtc,
            DateTime? executionDeadlineUtc,
            bool cancellationRequested = false)
        {
            WaveId = waveId;
            RequestId = requestId;
            Text = text;
            StyleId = styleId;
            OutputFileName = outputFileName;
            RequestedAtUtc = requestedAtUtc;
            AdmissionDeadlineUtc = admissionDeadlineUtc;
            ExecutionDeadlineUtc = executionDeadlineUtc;
            CancellationRequested = cancellationRequested;
        }
    }

    /// <summary>
    /// Lifecycle states for one TTS synthesis wave. Released describes
    /// resource lifecycle only and is deliberately not a terminal outcome.
    /// </summary>
    public enum TTSSynthesisWaveState
    {
        Created = 0,
        Accepted,
        Pending,
        Running,
        CancellationRequestedDraining,
        DeadlineExceededDraining,
        Succeeded,
        Failed,
        TimedOut,
        Cancelled,
        Released
    }

    public static class TTSSynthesisWaveStateSemantics
    {
        public static bool IsTerminal(TTSSynthesisWaveState state)
        {
            return state == TTSSynthesisWaveState.Succeeded
                || state == TTSSynthesisWaveState.Failed
                || state == TTSSynthesisWaveState.TimedOut
                || state == TTSSynthesisWaveState.Cancelled;
        }

        public static bool IsDraining(TTSSynthesisWaveState state)
        {
            return state == TTSSynthesisWaveState.CancellationRequestedDraining
                || state == TTSSynthesisWaveState.DeadlineExceededDraining;
        }
    }

    public enum TTSSynthesisFailureReason
    {
        None = 0,
        InvalidRequest,
        ResourceUnavailable,
        NativeSynthesisFailed,
        AudioDataInvalid,
        TimedOut,
        Cancelled,
        Shutdown,
        Unknown
    }

    /// <summary>
    /// Immediate decision vocabulary for the bounded admission service that
    /// will be implemented in Wave 2. This enum does not mutate host state.
    /// </summary>
    public enum TTSSynthesisAdmissionResult
    {
        AcceptedActive = 0,
        AcceptedPending,
        RejectedQueueFull,
        RejectedInvalidRequest,
        RejectedShuttingDown,
        RejectedDisposed
    }

    public enum TTSSynthesisQueueDiscipline
    {
        BoundedFifo = 0
    }

    public enum TTSSynthesisOverflowBehavior
    {
        RejectIncoming = 0
    }

    /// <summary>
    /// Pure queue policy. AR1-B begins with one active request and one FIFO
    /// pending request. Overflow rejects the incoming request; no pending
    /// request is silently replaced.
    /// </summary>
    public sealed class TTSSynthesisPendingPolicy
    {
        public int ActiveLimit { get; }
        public int PendingLimit { get; }
        public TTSSynthesisQueueDiscipline QueueDiscipline { get; }
        public TTSSynthesisOverflowBehavior OverflowBehavior { get; }

        public bool ReplacesPendingRequest => false;

        public TTSSynthesisPendingPolicy(
            int activeLimit,
            int pendingLimit,
            TTSSynthesisQueueDiscipline queueDiscipline,
            TTSSynthesisOverflowBehavior overflowBehavior)
        {
            if (activeLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(activeLimit),
                    activeLimit,
                    "Active limit must be positive.");
            }

            if (pendingLimit < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pendingLimit),
                    pendingLimit,
                    "Pending limit cannot be negative.");
            }

            ActiveLimit = activeLimit;
            PendingLimit = pendingLimit;
            QueueDiscipline = queueDiscipline;
            OverflowBehavior = overflowBehavior;
        }
    }

    /// <summary>
    /// Unity-independent audio artifact produced by synthesis. A file path is
    /// the current Android VOICEVOX return format; AudioClip and AudioSource
    /// belong to the later speech presentation boundary.
    /// </summary>
    public sealed class TTSSynthesisAudioArtifact
    {
        public string WavFilePath { get; }
        public long ByteLength { get; }

        public bool IsUsable =>
            !string.IsNullOrWhiteSpace(WavFilePath) && ByteLength > 0;

        public TTSSynthesisAudioArtifact(
            string wavFilePath,
            long byteLength)
        {
            WavFilePath = wavFilePath;
            ByteLength = byteLength;
        }
    }

    /// <summary>
    /// Immutable terminal result for a TTS synthesis wave. Succeeded means a
    /// usable WAV artifact exists. It does not mean playback started or
    /// completed. Managed timeout/cancellation also does not assert native
    /// stop; NativeReturnedAtUtc records the independently observed fact.
    /// </summary>
    public sealed class TTSSynthesisWaveResult
    {
        public string WaveId { get; }
        public string RequestId { get; }
        public TTSSynthesisWaveState TerminalState { get; }
        public TTSSynthesisFailureReason FailureReason { get; }
        public string NativeErrorCode { get; }
        public string Diagnostic { get; }
        public TTSSynthesisAudioArtifact AudioArtifact { get; }

        public DateTime RequestedAtUtc { get; }
        public DateTime? AcceptedAtUtc { get; }
        public DateTime? StartedAtUtc { get; }
        public DateTime? NativeReturnedAtUtc { get; }
        public DateTime TerminalAtUtc { get; }
        public DateTime? ReleasedAtUtc { get; }

        public bool ManagedCancellationRequested { get; }
        public bool DeadlineExceeded { get; }

        public bool NativeReturnObserved => NativeReturnedAtUtc.HasValue;
        public bool IsReleased => ReleasedAtUtc.HasValue;
        public bool IsAudioReady =>
            TerminalState == TTSSynthesisWaveState.Succeeded
            && AudioArtifact != null
            && AudioArtifact.IsUsable;

        public TTSSynthesisWaveResult(
            string waveId,
            string requestId,
            TTSSynthesisWaveState terminalState,
            TTSSynthesisFailureReason failureReason,
            string nativeErrorCode,
            string diagnostic,
            TTSSynthesisAudioArtifact audioArtifact,
            DateTime requestedAtUtc,
            DateTime? acceptedAtUtc,
            DateTime? startedAtUtc,
            DateTime? nativeReturnedAtUtc,
            DateTime terminalAtUtc,
            DateTime? releasedAtUtc,
            bool managedCancellationRequested,
            bool deadlineExceeded)
        {
            if (!TTSSynthesisWaveStateSemantics.IsTerminal(terminalState))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(terminalState),
                    terminalState,
                    "A synthesis result must retain a terminal outcome.");
            }

            if (terminalState == TTSSynthesisWaveState.Succeeded
                && (audioArtifact == null || !audioArtifact.IsUsable))
            {
                throw new ArgumentException(
                    "Succeeded synthesis requires a usable WAV artifact.",
                    nameof(audioArtifact));
            }

            if (terminalState == TTSSynthesisWaveState.Succeeded
                && failureReason != TTSSynthesisFailureReason.None)
            {
                throw new ArgumentException(
                    "Succeeded synthesis cannot carry a failure reason.",
                    nameof(failureReason));
            }

            if (terminalState != TTSSynthesisWaveState.Succeeded
                && failureReason == TTSSynthesisFailureReason.None)
            {
                throw new ArgumentException(
                    "Unsuccessful synthesis requires a failure reason.",
                    nameof(failureReason));
            }

            WaveId = waveId;
            RequestId = requestId;
            TerminalState = terminalState;
            FailureReason = failureReason;
            NativeErrorCode = nativeErrorCode;
            Diagnostic = diagnostic;
            AudioArtifact = audioArtifact;
            RequestedAtUtc = requestedAtUtc;
            AcceptedAtUtc = acceptedAtUtc;
            StartedAtUtc = startedAtUtc;
            NativeReturnedAtUtc = nativeReturnedAtUtc;
            TerminalAtUtc = terminalAtUtc;
            ReleasedAtUtc = releasedAtUtc;
            ManagedCancellationRequested = managedCancellationRequested;
            DeadlineExceeded = deadlineExceeded;
        }

        /// <summary>
        /// Records release without replacing the original terminal outcome.
        /// A repeated release is idempotent and returns this snapshot.
        /// </summary>
        public TTSSynthesisWaveResult WithReleasedAt(DateTime releasedAtUtc)
        {
            if (IsReleased)
                return this;

            return new TTSSynthesisWaveResult(
                WaveId,
                RequestId,
                TerminalState,
                FailureReason,
                NativeErrorCode,
                Diagnostic,
                AudioArtifact,
                RequestedAtUtc,
                AcceptedAtUtc,
                StartedAtUtc,
                NativeReturnedAtUtc,
                TerminalAtUtc,
                releasedAtUtc,
                ManagedCancellationRequested,
                DeadlineExceeded);
        }
    }
}
