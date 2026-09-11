// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using System.Collections.Generic;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;

namespace SalieriAI.Core.Execution.Orchestration.Resources
{
    /// <summary>
    /// Stateless facade over the pure logical lease reducer. Runtime
    /// availability and all identifiers/timestamps are supplied by callers.
    /// </summary>
    public sealed class LogicalResourceLeaseService
    {
        public LogicalResourceLeaseTransition<ResourceLeaseAcquireResult>
            Acquire(LogicalResourceLeaseState state,
                ResourceLeaseAcquireRequest request)
        {
            return LogicalResourceLeaseReducer.Acquire(state, request);
        }

        public LogicalResourceLeaseTransition<ResourceLeaseReleaseResult>
            Release(LogicalResourceLeaseState state,
                ResourceLeaseReleaseRequest request)
        {
            return LogicalResourceLeaseReducer.Release(state, request);
        }

        public LogicalResourceLeaseTransition<
            LogicalResourceForceReleaseResult> RequestForceRelease(
                LogicalResourceLeaseState state,
                LogicalResourceForceReleaseRequest request)
        {
            return LogicalResourceLeaseReducer.RequestForceRelease(
                state, request);
        }
    }

    /// <summary>
    /// Pure state transitions for step-owned logical resource leases.
    /// This type never queries runtime state and never performs physical work.
    /// </summary>
    public static class LogicalResourceLeaseReducer
    {
        public static LogicalResourceLeaseTransition<
            ResourceLeaseAcquireResult> Acquire(
                LogicalResourceLeaseState state,
                ResourceLeaseAcquireRequest request)
        {
            state = StateOrEmpty(state, request != null
                ? request.Generation : 0);

            if (request != null && !Empty(request.RequestId) &&
                state.HasProcessedRequest(request.RequestId))
                return AcquireRejected(state, request,
                    LogicalResourceLeaseAcquireStatus.RejectedDuplicate,
                    "Request ID was already processed.", false);

            string invalidReason = ValidateAcquire(state, request);
            if (invalidReason.Length > 0)
                return AcquireRejected(state, request,
                    LogicalResourceLeaseAcquireStatus.RejectedInvalid,
                    invalidReason, CanRecord(request, state));

            if (state.HasKnownLease(request.LeaseId))
                return AcquireRejected(state, request,
                    LogicalResourceLeaseAcquireStatus.RejectedDuplicate,
                    "Lease ID was already used.", true);

            if (state.ProcessedRequestIds.Count >=
                    LogicalResourceLeaseState.MaxProcessedRequestIds ||
                state.KnownLeaseIds.Count >=
                    LogicalResourceLeaseState.MaxKnownLeaseIds)
                return AcquireRejected(state, request,
                    LogicalResourceLeaseAcquireStatus.Failed,
                    "Lease correlation capacity was reached.", false);

            LogicalResourceLeaseSnapshot sameOwner =
                FindSameOwner(state, request);
            if (sameOwner != null)
                return AcquireRejected(state, request,
                    LogicalResourceLeaseAcquireStatus.RejectedDuplicate,
                    "The step already owns this resource in the same mode.",
                    true);

            switch (request.Availability)
            {
                case ResourceShadowAvailabilityStatus.Unavailable:
                    return AcquireRejected(state, request,
                        LogicalResourceLeaseAcquireStatus
                            .RejectedUnavailable,
                        "Runtime availability is Unavailable.", true);
                case ResourceShadowAvailabilityStatus.Unknown:
                    return AcquireRejected(state, request,
                        LogicalResourceLeaseAcquireStatus.RejectedInvalid,
                        "Runtime availability is Unknown.", true);
                case ResourceShadowAvailabilityStatus.Failed:
                    return AcquireRejected(state, request,
                        LogicalResourceLeaseAcquireStatus.Failed,
                        "Runtime availability lookup failed.", true);
            }

            if (state.ActiveLeases.Count >=
                LogicalResourceLeaseState.MaxActiveLeases)
                return AcquireRejected(state, request,
                    LogicalResourceLeaseAcquireStatus.Failed,
                    "Active lease capacity was reached.", true);

            LogicalResourceLeaseSnapshot conflict =
                FindConflict(state, request);
            if (conflict != null)
                return AcquireRejected(state, request,
                    LogicalResourceLeaseAcquireStatus.RejectedConflict,
                    "Resource conflicts with active lease " +
                    conflict.LeaseId + ".", true);

            var requested = SnapshotFromAcquire(
                request, ExecutionResourceLeaseStatus.Requested,
                string.Empty);
            var acquired = SnapshotFromAcquire(
                request, ExecutionResourceLeaseStatus.Acquired,
                string.Empty);
            LogicalResourceLeaseState next = BuildState(
                state,
                Append(state.ActiveLeases, acquired),
                Append(Append(state.History, requested), acquired),
                Append(state.ProcessedRequestIds, request.RequestId),
                Append(state.KnownLeaseIds, request.LeaseId));

            return new LogicalResourceLeaseTransition<
                ResourceLeaseAcquireResult>(
                    next,
                    new ResourceLeaseAcquireResult(
                        request,
                        LogicalResourceLeaseAcquireStatus.Acquired,
                        string.Empty));
        }

        public static LogicalResourceLeaseTransition<
            ResourceLeaseReleaseResult> Release(
                LogicalResourceLeaseState state,
                ResourceLeaseReleaseRequest request)
        {
            state = StateOrEmpty(state, request != null
                ? request.Generation : 0);
            if (request != null && !Empty(request.RequestId) &&
                state.HasProcessedRequest(request.RequestId))
                return ReleaseRejected(state, request,
                    LogicalResourceLeaseReleaseStatus.RejectedInvalid,
                    "Request ID was already processed.", false);

            string invalidReason = ValidateRelease(state, request);
            if (invalidReason.Length > 0)
                return ReleaseRejected(state, request,
                    LogicalResourceLeaseReleaseStatus.RejectedInvalid,
                    invalidReason, CanRecord(request, state));

            if (state.ProcessedRequestIds.Count >=
                LogicalResourceLeaseState.MaxProcessedRequestIds)
                return ReleaseRejected(state, request,
                    LogicalResourceLeaseReleaseStatus.Failed,
                    "Request correlation capacity was reached.", false);

            LogicalResourceLeaseSnapshot active =
                state.FindActive(request.LeaseId);
            if (active == null)
            {
                LogicalResourceLeaseReleaseStatus status =
                    state.HasKnownLease(request.LeaseId)
                    ? LogicalResourceLeaseReleaseStatus.AlreadyReleased
                    : LogicalResourceLeaseReleaseStatus.RejectedInvalid;
                return ReleaseRejected(state, request, status,
                    status == LogicalResourceLeaseReleaseStatus
                        .AlreadyReleased
                        ? "Lease was already released or rejected."
                        : "Lease ID is unknown.", true);
            }

            string ownerError = ValidateOwner(active, request);
            if (ownerError.Length > 0)
                return ReleaseRejected(state, request,
                    LogicalResourceLeaseReleaseStatus.RejectedInvalid,
                    ownerError, true);

            var releaseRequested = active.WithStatus(
                ExecutionResourceLeaseStatus.ReleaseRequested,
                null, active.ForceReleaseRequested, string.Empty);
            List<LogicalResourceLeaseSnapshot> history =
                Append(state.History, releaseRequested);

            if (!request.ReleaseConfirmed)
            {
                string reason = request.ExternalFailureReason.Length > 0
                    ? request.ExternalFailureReason
                    : "External release was not confirmed.";
                var failed = active.WithStatus(
                    ExecutionResourceLeaseStatus.ReleaseFailed,
                    null, active.ForceReleaseRequested, reason);
                history = Append(history, failed);
                LogicalResourceLeaseState failedState = BuildState(
                    state, ReplaceActive(state.ActiveLeases, failed),
                    history,
                    Append(state.ProcessedRequestIds,
                        request.RequestId),
                    state.KnownLeaseIds);
                return new LogicalResourceLeaseTransition<
                    ResourceLeaseReleaseResult>(
                        failedState,
                        new ResourceLeaseReleaseResult(
                            request,
                            LogicalResourceLeaseReleaseStatus.Failed,
                            reason));
            }

            var released = active.WithStatus(
                ExecutionResourceLeaseStatus.Released,
                request.RequestedAtUtc,
                active.ForceReleaseRequested, string.Empty);
            history = Append(history, released);
            LogicalResourceLeaseState next = BuildState(
                state, RemoveActive(state.ActiveLeases, active.LeaseId),
                history,
                Append(state.ProcessedRequestIds, request.RequestId),
                state.KnownLeaseIds);
            return new LogicalResourceLeaseTransition<
                ResourceLeaseReleaseResult>(
                    next,
                    new ResourceLeaseReleaseResult(
                        request,
                        LogicalResourceLeaseReleaseStatus.Released,
                        string.Empty));
        }

        public static LogicalResourceLeaseTransition<
            LogicalResourceForceReleaseResult> RequestForceRelease(
                LogicalResourceLeaseState state,
                LogicalResourceForceReleaseRequest request)
        {
            state = StateOrEmpty(state, request != null
                ? request.Generation : 0);
            if (request != null && !Empty(request.RequestId) &&
                state.HasProcessedRequest(request.RequestId))
                return ForceRejected(state, request,
                    LogicalResourceForceReleaseStatus.RejectedInvalid,
                    "Request ID was already processed.", false);

            string invalidReason = ValidateForce(state, request);
            if (invalidReason.Length > 0)
                return ForceRejected(state, request,
                    LogicalResourceForceReleaseStatus.RejectedInvalid,
                    invalidReason, CanRecord(request, state));

            if (state.ProcessedRequestIds.Count >=
                LogicalResourceLeaseState.MaxProcessedRequestIds)
                return ForceRejected(state, request,
                    LogicalResourceForceReleaseStatus.Failed,
                    "Request correlation capacity was reached.", false);

            var active = new List<LogicalResourceLeaseSnapshot>(
                state.ActiveLeases);
            var affectedIds = new List<string>();
            var forcedHistory = new List<LogicalResourceLeaseSnapshot>();
            for (int i = 0; i < active.Count; i++)
            {
                LogicalResourceLeaseSnapshot lease = active[i];
                if (!MatchesForce(lease, request)) continue;
                var forced = lease.WithStatus(
                    ExecutionResourceLeaseStatus.ForceReleaseRequested,
                    null, true, string.Empty);
                active[i] = forced;
                affectedIds.Add(lease.LeaseId);
                forcedHistory.Add(forced);
            }

            if (affectedIds.Count == 0)
                return ForceRejected(state, request,
                    LogicalResourceForceReleaseStatus.RejectedInvalid,
                    "No active lease matched the supplied ownership scope.",
                    true);

            List<LogicalResourceLeaseSnapshot> history =
                new List<LogicalResourceLeaseSnapshot>(state.History);
            history.AddRange(forcedHistory);
            LogicalResourceLeaseState next = BuildState(
                state, active, history,
                Append(state.ProcessedRequestIds, request.RequestId),
                state.KnownLeaseIds);
            return new LogicalResourceLeaseTransition<
                LogicalResourceForceReleaseResult>(
                    next,
                    new LogicalResourceForceReleaseResult(
                        request,
                        LogicalResourceForceReleaseStatus.Requested,
                        affectedIds, string.Empty));
        }

        private static string ValidateAcquire(
            LogicalResourceLeaseState state,
            ResourceLeaseAcquireRequest request)
        {
            if (request == null) return "Acquire request is null.";
            if (request.Generation != state.Generation)
                return "Generation does not match current state.";
            if (request.StepIsTerminal)
                return "Terminal steps cannot acquire resources.";
            if (Empty(request.LeaseId) || Empty(request.PlanId) ||
                Empty(request.StepId) || Empty(request.RequestId) ||
                Empty(request.ResourceId) || Empty(request.ResourceType))
                return "Required acquire identity is missing.";
            if (!Enum.IsDefined(typeof(ExecutionResourceAccessMode),
                request.AccessMode))
                return "Access mode is invalid.";
            if (!Enum.IsDefined(
                typeof(ResourceShadowAvailabilityStatus),
                request.Availability))
                return "Availability is invalid.";
            return string.Empty;
        }

        private static string ValidateRelease(
            LogicalResourceLeaseState state,
            ResourceLeaseReleaseRequest request)
        {
            if (request == null) return "Release request is null.";
            if (request.Generation != state.Generation)
                return "Generation does not match current state.";
            if (Empty(request.LeaseId) || Empty(request.PlanId) ||
                Empty(request.StepId) || Empty(request.RequestId) ||
                Empty(request.ResourceId))
                return "Required release identity is missing.";
            return string.Empty;
        }

        private static string ValidateForce(
            LogicalResourceLeaseState state,
            LogicalResourceForceReleaseRequest request)
        {
            if (request == null) return "Force-release request is null.";
            if (request.Generation != state.Generation)
                return "Generation does not match current state.";
            if (Empty(request.RequestId))
                return "Request ID is required.";
            return string.Empty;
        }

        private static string ValidateOwner(
            LogicalResourceLeaseSnapshot active,
            ResourceLeaseReleaseRequest request)
        {
            if (!Same(active.PlanId, request.PlanId))
                return "Plan does not own the lease.";
            if (!Same(active.StepId, request.StepId))
                return "Step does not own the lease.";
            if (!Same(active.ResourceId, request.ResourceId))
                return "Resource ID does not match the lease.";
            return string.Empty;
        }

        private static LogicalResourceLeaseSnapshot FindSameOwner(
            LogicalResourceLeaseState state,
            ResourceLeaseAcquireRequest request)
        {
            for (int i = 0; i < state.ActiveLeases.Count; i++)
            {
                LogicalResourceLeaseSnapshot lease =
                    state.ActiveLeases[i];
                if (Same(lease.PlanId, request.PlanId) &&
                    Same(lease.StepId, request.StepId) &&
                    Same(lease.ResourceId, request.ResourceId) &&
                    lease.AccessMode == request.AccessMode)
                    return lease;
            }
            return null;
        }

        private static LogicalResourceLeaseSnapshot FindConflict(
            LogicalResourceLeaseState state,
            ResourceLeaseAcquireRequest request)
        {
            for (int i = 0; i < state.ActiveLeases.Count; i++)
            {
                LogicalResourceLeaseSnapshot lease =
                    state.ActiveLeases[i];
                if (!Same(lease.ResourceId, request.ResourceId))
                    continue;
                if (lease.AccessMode ==
                    ExecutionResourceAccessMode.Exclusive ||
                    request.AccessMode ==
                    ExecutionResourceAccessMode.Exclusive)
                    return lease;
            }
            return null;
        }

        private static bool MatchesForce(
            LogicalResourceLeaseSnapshot lease,
            LogicalResourceForceReleaseRequest request)
        {
            return (Empty(request.PlanId) ||
                    Same(lease.PlanId, request.PlanId)) &&
                (Empty(request.StepId) ||
                    Same(lease.StepId, request.StepId)) &&
                (Empty(request.ResourceId) ||
                    Same(lease.ResourceId, request.ResourceId));
        }

        private static LogicalResourceLeaseTransition<
            ResourceLeaseAcquireResult> AcquireRejected(
                LogicalResourceLeaseState state,
                ResourceLeaseAcquireRequest request,
                LogicalResourceLeaseAcquireStatus status,
                string reason, bool record)
        {
            LogicalResourceLeaseState next = state;
            if (record && request != null)
            {
                var requested = SnapshotFromAcquire(request,
                    ExecutionResourceLeaseStatus.Requested,
                    string.Empty);
                var rejected = SnapshotFromAcquire(request,
                    ExecutionResourceLeaseStatus.Rejected, reason);
                next = BuildState(state, state.ActiveLeases,
                    Append(Append(state.History, requested), rejected),
                    Append(state.ProcessedRequestIds, request.RequestId),
                    Append(state.KnownLeaseIds, request.LeaseId));
            }
            return new LogicalResourceLeaseTransition<
                ResourceLeaseAcquireResult>(
                    next,
                    new ResourceLeaseAcquireResult(
                        request, status, reason));
        }

        private static LogicalResourceLeaseTransition<
            ResourceLeaseReleaseResult> ReleaseRejected(
                LogicalResourceLeaseState state,
                ResourceLeaseReleaseRequest request,
                LogicalResourceLeaseReleaseStatus status,
                string reason, bool record)
        {
            LogicalResourceLeaseState next = state;
            if (record && request != null)
                next = BuildState(state, state.ActiveLeases,
                    state.History,
                    Append(state.ProcessedRequestIds, request.RequestId),
                    state.KnownLeaseIds);
            return new LogicalResourceLeaseTransition<
                ResourceLeaseReleaseResult>(
                    next,
                    new ResourceLeaseReleaseResult(
                        request, status, reason));
        }

        private static LogicalResourceLeaseTransition<
            LogicalResourceForceReleaseResult> ForceRejected(
                LogicalResourceLeaseState state,
                LogicalResourceForceReleaseRequest request,
                LogicalResourceForceReleaseStatus status,
                string reason, bool record)
        {
            LogicalResourceLeaseState next = state;
            if (record && request != null)
                next = BuildState(state, state.ActiveLeases,
                    state.History,
                    Append(state.ProcessedRequestIds, request.RequestId),
                    state.KnownLeaseIds);
            return new LogicalResourceLeaseTransition<
                LogicalResourceForceReleaseResult>(
                    next,
                    new LogicalResourceForceReleaseResult(
                        request, status,
                        new string[0], reason));
        }

        private static LogicalResourceLeaseSnapshot SnapshotFromAcquire(
            ResourceLeaseAcquireRequest request,
            ExecutionResourceLeaseStatus status, string reason)
        {
            return new LogicalResourceLeaseSnapshot(
                request.LeaseId, request.ResourceId,
                request.ResourceType, request.PlanId, request.StepId,
                request.ExecutionAttemptId, request.AccessMode, status,
                request.RequestedAtUtc, null, false, reason);
        }

        private static LogicalResourceLeaseState StateOrEmpty(
            LogicalResourceLeaseState state, int generation)
        {
            return state ?? LogicalResourceLeaseState.Empty(generation);
        }

        private static LogicalResourceLeaseState BuildState(
            LogicalResourceLeaseState previous,
            IEnumerable<LogicalResourceLeaseSnapshot> active,
            IEnumerable<LogicalResourceLeaseSnapshot> history,
            IEnumerable<string> requests,
            IEnumerable<string> knownLeases)
        {
            return new LogicalResourceLeaseState(previous.Generation,
                active,
                KeepLast(history,
                    LogicalResourceLeaseState.MaxHistoryEntries),
                requests,
                knownLeases);
        }

        private static List<T> Append<T>(
            IEnumerable<T> source, T value)
        {
            var result = source != null
                ? new List<T>(source) : new List<T>();
            result.Add(value);
            return result;
        }

        private static List<LogicalResourceLeaseSnapshot>
            ReplaceActive(
                IEnumerable<LogicalResourceLeaseSnapshot> source,
                LogicalResourceLeaseSnapshot replacement)
        {
            var result = new List<LogicalResourceLeaseSnapshot>(source);
            for (int i = 0; i < result.Count; i++)
                if (Same(result[i].LeaseId, replacement.LeaseId))
                {
                    result[i] = replacement;
                    break;
                }
            return result;
        }

        private static List<LogicalResourceLeaseSnapshot>
            RemoveActive(
                IEnumerable<LogicalResourceLeaseSnapshot> source,
                string leaseId)
        {
            var result = new List<LogicalResourceLeaseSnapshot>();
            foreach (LogicalResourceLeaseSnapshot lease in source)
                if (!Same(lease.LeaseId, leaseId)) result.Add(lease);
            return result;
        }

        private static List<T> KeepLast<T>(
            IEnumerable<T> source, int maximum)
        {
            var values = source != null
                ? new List<T>(source) : new List<T>();
            if (values.Count <= maximum) return values;
            return values.GetRange(values.Count - maximum, maximum);
        }

        private static bool CanRecord(
            ResourceLeaseAcquireRequest request,
            LogicalResourceLeaseState state)
        {
            return request != null &&
                request.Generation == state.Generation &&
                !Empty(request.RequestId) &&
                !Empty(request.LeaseId) &&
                state.ProcessedRequestIds.Count <
                    LogicalResourceLeaseState.MaxProcessedRequestIds &&
                state.KnownLeaseIds.Count <
                    LogicalResourceLeaseState.MaxKnownLeaseIds;
        }

        private static bool CanRecord(
            ResourceLeaseReleaseRequest request,
            LogicalResourceLeaseState state)
        {
            return request != null &&
                request.Generation == state.Generation &&
                !Empty(request.RequestId) &&
                state.ProcessedRequestIds.Count <
                    LogicalResourceLeaseState.MaxProcessedRequestIds;
        }

        private static bool CanRecord(
            LogicalResourceForceReleaseRequest request,
            LogicalResourceLeaseState state)
        {
            return request != null &&
                request.Generation == state.Generation &&
                !Empty(request.RequestId) &&
                state.ProcessedRequestIds.Count <
                    LogicalResourceLeaseState.MaxProcessedRequestIds;
        }

        private static bool Empty(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right,
                StringComparison.Ordinal);
        }
    }
}
