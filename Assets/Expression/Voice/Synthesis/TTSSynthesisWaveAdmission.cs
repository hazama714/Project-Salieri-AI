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
    /// Lifecycle of the pure bounded admission boundary. Closing admission
    /// does not perform native cancellation, cleanup, or resource release.
    /// </summary>
    public enum TTSSynthesisAdmissionLifecycle
    {
        Open = 0,
        ShutdownRequested,
        Disposed
    }

    /// <summary>
    /// Thread-safe, non-queuing admission for AR1-B. Exactly one active slot
    /// and one FIFO pending slot are retained. There are no waiters and no
    /// hidden collection behind either slot.
    /// </summary>
    public sealed class TTSSynthesisWaveAdmission : IDisposable
    {
        private sealed class Slot
        {
            public TTSSynthesisWaveRequest Request { get; }
            public DateTime AdmittedAtUtc { get; }
            public TTSSynthesisWaveState State { get; set; }
            public TTSSynthesisWaveResult TerminalResult { get; set; }

            public Slot(
                TTSSynthesisWaveRequest request,
                DateTime admittedAtUtc,
                TTSSynthesisWaveState state)
            {
                Request = request;
                AdmittedAtUtc = admittedAtUtc;
                State = state;
            }
        }

        private readonly object gate = new object();
        private readonly TTSSynthesisPendingPolicy policy;

        private Slot active;
        private Slot pending;
        private TTSSynthesisAdmissionLifecycle lifecycle;

        public TTSSynthesisWaveAdmission(TTSSynthesisPendingPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            if (policy.ActiveLimit != 1
                || policy.PendingLimit != 1
                || policy.QueueDiscipline
                    != TTSSynthesisQueueDiscipline.BoundedFifo
                || policy.OverflowBehavior
                    != TTSSynthesisOverflowBehavior.RejectIncoming
                || policy.ReplacesPendingRequest)
            {
                throw new ArgumentException(
                    "AR1-B admission requires active=1, pending=1, "
                    + "BoundedFifo, and RejectIncoming.",
                    nameof(policy));
            }

            this.policy = policy;
            lifecycle = TTSSynthesisAdmissionLifecycle.Open;
        }

        public TTSSynthesisPendingPolicy Policy => policy;

        public TTSSynthesisAdmissionLifecycle Lifecycle
        {
            get
            {
                lock (gate)
                    return lifecycle;
            }
        }

        public int ActiveCount
        {
            get
            {
                lock (gate)
                    return active == null ? 0 : 1;
            }
        }

        public int PendingCount
        {
            get
            {
                lock (gate)
                    return pending == null ? 0 : 1;
            }
        }

        public TTSSynthesisWaveRequest ActiveRequest
        {
            get
            {
                lock (gate)
                    return active == null ? null : active.Request;
            }
        }

        public TTSSynthesisWaveRequest PendingRequest
        {
            get
            {
                lock (gate)
                    return pending == null ? null : pending.Request;
            }
        }

        public TTSSynthesisWaveState? ActiveState
        {
            get
            {
                lock (gate)
                    return active == null
                        ? (TTSSynthesisWaveState?)null
                        : active.State;
            }
        }

        public TTSSynthesisWaveState? PendingState
        {
            get
            {
                lock (gate)
                    return pending == null
                        ? (TTSSynthesisWaveState?)null
                        : pending.State;
            }
        }

        public TTSSynthesisWaveResult ActiveTerminalResult
        {
            get
            {
                lock (gate)
                    return active == null ? null : active.TerminalResult;
            }
        }

        /// <summary>
        /// Makes an immediate non-blocking admission decision. It never waits
        /// for either slot and never replaces the FIFO pending request.
        /// </summary>
        public TTSSynthesisAdmissionResult Admit(
            TTSSynthesisWaveRequest request,
            DateTime admittedAtUtc)
        {
            lock (gate)
            {
                if (lifecycle == TTSSynthesisAdmissionLifecycle.Disposed)
                    return TTSSynthesisAdmissionResult.RejectedDisposed;

                if (lifecycle == TTSSynthesisAdmissionLifecycle.ShutdownRequested)
                    return TTSSynthesisAdmissionResult.RejectedShuttingDown;

                if (!IsValid(request))
                    return TTSSynthesisAdmissionResult.RejectedInvalidRequest;

                if (HasIdentity(active, request) || HasIdentity(pending, request))
                    return TTSSynthesisAdmissionResult.RejectedInvalidRequest;

                if (active == null)
                {
                    if (pending != null)
                    {
                        throw new InvalidOperationException(
                            "Pending cannot exist without an active request.");
                    }

                    active = new Slot(
                        request,
                        admittedAtUtc,
                        TTSSynthesisWaveState.Accepted);
                    return TTSSynthesisAdmissionResult.AcceptedActive;
                }

                if (pending == null)
                {
                    pending = new Slot(
                        request,
                        admittedAtUtc,
                        TTSSynthesisWaveState.Pending);
                    return TTSSynthesisAdmissionResult.AcceptedPending;
                }

                return TTSSynthesisAdmissionResult.RejectedQueueFull;
            }
        }

        public bool TryMarkActiveRunning(string waveId, string requestId)
        {
            lock (gate)
            {
                if (!HasIdentity(active, waveId, requestId)
                    || active.State != TTSSynthesisWaveState.Accepted)
                {
                    return false;
                }

                active.State = TTSSynthesisWaveState.Running;
                return true;
            }
        }

        /// <summary>
        /// Records a terminal outcome without releasing the active slot.
        /// A native return timestamp alone has no admission meaning.
        /// </summary>
        public bool TryRecordActiveTerminal(TTSSynthesisWaveResult result)
        {
            lock (gate)
            {
                if (result == null
                    || !TTSSynthesisWaveStateSemantics.IsTerminal(
                        result.TerminalState)
                    || result.IsReleased
                    || !HasIdentity(active, result.WaveId, result.RequestId)
                    || active.TerminalResult != null
                    || !CanBecomeTerminal(active.State))
                {
                    return false;
                }

                active.State = result.TerminalState;
                active.TerminalResult = result;
                return true;
            }
        }

        /// <summary>
        /// Releases a terminal active wave and promotes the single pending
        /// wave exactly once. Promotion cannot occur before explicit release.
        /// </summary>
        public bool TryReleaseActive(
            DateTime releasedAtUtc,
            out TTSSynthesisWaveResult releasedResult,
            out TTSSynthesisWaveRequest promotedRequest)
        {
            lock (gate)
            {
                releasedResult = null;
                promotedRequest = null;

                if (active == null
                    || active.TerminalResult == null
                    || !TTSSynthesisWaveStateSemantics.IsTerminal(active.State)
                    || active.TerminalResult.IsReleased)
                {
                    return false;
                }

                releasedResult = active.TerminalResult.WithReleasedAt(releasedAtUtc);
                active = null;

                if (pending != null)
                {
                    Slot promoted = pending;
                    pending = null;
                    promoted.State = TTSSynthesisWaveState.Accepted;
                    active = promoted;
                    promotedRequest = promoted.Request;
                }

                return true;
            }
        }

        /// <summary>
        /// Cancels and releases only the pending wave. No native cancellation
        /// is implied because a pending wave has not started native work.
        /// </summary>
        public bool TryCancelPending(
            string waveId,
            string requestId,
            DateTime cancelledAtUtc,
            out TTSSynthesisWaveResult cancelledResult)
        {
            lock (gate)
            {
                cancelledResult = null;
                if (!HasIdentity(pending, waveId, requestId))
                    return false;

                cancelledResult = CreatePendingTerminalResult(
                    pending,
                    TTSSynthesisWaveState.Cancelled,
                    TTSSynthesisFailureReason.Cancelled,
                    "PENDING_CANCELLED",
                    cancelledAtUtc,
                    true,
                    false);
                pending = null;
                return true;
            }
        }

        /// <summary>
        /// Evaluates an admission deadline only when explicitly called. This
        /// method owns no clock, timer, scheduler, or background waiter.
        /// </summary>
        public bool TryExpirePending(
            DateTime evaluatedAtUtc,
            out TTSSynthesisWaveResult timedOutResult)
        {
            lock (gate)
            {
                timedOutResult = null;
                if (pending == null
                    || !pending.Request.AdmissionDeadlineUtc.HasValue
                    || evaluatedAtUtc
                        < pending.Request.AdmissionDeadlineUtc.Value)
                {
                    return false;
                }

                timedOutResult = CreatePendingTerminalResult(
                    pending,
                    TTSSynthesisWaveState.TimedOut,
                    TTSSynthesisFailureReason.TimedOut,
                    "ADMISSION_DEADLINE_EXCEEDED",
                    evaluatedAtUtc,
                    false,
                    true);
                pending = null;
                return true;
            }
        }

        public void RequestShutdown()
        {
            lock (gate)
            {
                if (lifecycle == TTSSynthesisAdmissionLifecycle.Open)
                    lifecycle = TTSSynthesisAdmissionLifecycle.ShutdownRequested;
            }
        }

        /// <summary>
        /// Closes admission only. Existing slot ownership is deliberately not
        /// cancelled, drained, released, or otherwise rewritten in Wave 2.
        /// </summary>
        public void Dispose()
        {
            lock (gate)
                lifecycle = TTSSynthesisAdmissionLifecycle.Disposed;
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

        private static bool HasIdentity(
            Slot slot,
            TTSSynthesisWaveRequest request)
        {
            return request != null
                && HasIdentity(slot, request.WaveId, request.RequestId);
        }

        private static bool HasIdentity(
            Slot slot,
            string waveId,
            string requestId)
        {
            return slot != null
                && string.Equals(
                    slot.Request.WaveId,
                    waveId,
                    StringComparison.Ordinal)
                && string.Equals(
                    slot.Request.RequestId,
                    requestId,
                    StringComparison.Ordinal);
        }

        private static bool CanBecomeTerminal(TTSSynthesisWaveState state)
        {
            return state == TTSSynthesisWaveState.Accepted
                || state == TTSSynthesisWaveState.Running
                || TTSSynthesisWaveStateSemantics.IsDraining(state);
        }

        private static TTSSynthesisWaveResult CreatePendingTerminalResult(
            Slot slot,
            TTSSynthesisWaveState terminalState,
            TTSSynthesisFailureReason failureReason,
            string nativeErrorCode,
            DateTime terminalAtUtc,
            bool managedCancellationRequested,
            bool deadlineExceeded)
        {
            return new TTSSynthesisWaveResult(
                slot.Request.WaveId,
                slot.Request.RequestId,
                terminalState,
                failureReason,
                nativeErrorCode,
                "Pending wave ended before native synthesis started.",
                null,
                slot.Request.RequestedAtUtc,
                slot.AdmittedAtUtc,
                null,
                null,
                terminalAtUtc,
                terminalAtUtc,
                managedCancellationRequested,
                deadlineExceeded);
        }
    }
}
