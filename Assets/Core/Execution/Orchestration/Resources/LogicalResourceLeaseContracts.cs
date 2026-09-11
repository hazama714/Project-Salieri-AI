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
using System.Collections.ObjectModel;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;

namespace SalieriAI.Core.Execution.Orchestration.Resources
{
    public enum LogicalResourceLeaseAcquireStatus
    {
        Acquired = 0,
        RejectedUnavailable = 1,
        RejectedConflict = 2,
        RejectedDuplicate = 3,
        RejectedInvalid = 4,
        Failed = 5
    }

    public enum LogicalResourceLeaseReleaseStatus
    {
        Released = 0,
        RejectedInvalid = 1,
        AlreadyReleased = 2,
        Failed = 3
    }

    public enum LogicalResourceForceReleaseStatus
    {
        Requested = 0,
        RejectedInvalid = 1,
        Failed = 2
    }

    public sealed class ResourceLeaseAcquireRequest
    {
        public string LeaseId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string RequestId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public ResourceShadowAvailabilityStatus Availability { get; }
        public DateTime RequestedAtUtc { get; }
        public int Generation { get; }
        public bool StepIsTerminal { get; }

        public ResourceLeaseAcquireRequest(
            string leaseId, string planId, string stepId,
            string executionAttemptId, string requestId,
            string resourceId, string resourceType,
            ExecutionResourceAccessMode accessMode,
            ResourceShadowAvailabilityStatus availability,
            DateTime requestedAtUtc, int generation,
            bool stepIsTerminal)
        {
            LeaseId = Text(leaseId);
            PlanId = Text(planId);
            StepId = Text(stepId);
            ExecutionAttemptId = Text(executionAttemptId);
            RequestId = Text(requestId);
            ResourceId = Text(resourceId);
            ResourceType = Text(resourceType);
            AccessMode = accessMode;
            Availability = availability;
            RequestedAtUtc = Utc(requestedAtUtc);
            Generation = generation;
            StepIsTerminal = stepIsTerminal;
        }

        private static string Text(string value)
        {
            return value ?? string.Empty;
        }

        private static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime) ||
                value.Kind == DateTimeKind.Utc)
                return value;
            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }

    public sealed class ResourceLeaseAcquireResult
    {
        public string LeaseId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string RequestId { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public LogicalResourceLeaseAcquireStatus Status { get; }
        public string FailureReason { get; }
        public DateTime OccurredAtUtc { get; }
        public int Generation { get; }

        public ResourceLeaseAcquireResult(
            ResourceLeaseAcquireRequest request,
            LogicalResourceLeaseAcquireStatus status,
            string failureReason)
        {
            LeaseId = request != null ? request.LeaseId : string.Empty;
            ResourceId =
                request != null ? request.ResourceId : string.Empty;
            ResourceType =
                request != null ? request.ResourceType : string.Empty;
            PlanId = request != null ? request.PlanId : string.Empty;
            StepId = request != null ? request.StepId : string.Empty;
            ExecutionAttemptId = request != null
                ? request.ExecutionAttemptId : string.Empty;
            RequestId =
                request != null ? request.RequestId : string.Empty;
            AccessMode = request != null
                ? request.AccessMode
                : ExecutionResourceAccessMode.Exclusive;
            Status = status;
            FailureReason = failureReason ?? string.Empty;
            OccurredAtUtc = request != null
                ? request.RequestedAtUtc : default(DateTime);
            Generation = request != null ? request.Generation : 0;
        }
    }

    public sealed class ResourceLeaseReleaseRequest
    {
        public string LeaseId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string RequestId { get; }
        public string ResourceId { get; }
        public DateTime RequestedAtUtc { get; }
        public int Generation { get; }
        public bool ReleaseConfirmed { get; }
        public string ExternalFailureReason { get; }

        public ResourceLeaseReleaseRequest(
            string leaseId, string planId, string stepId,
            string requestId, string resourceId,
            DateTime requestedAtUtc, int generation,
            bool releaseConfirmed,
            string externalFailureReason)
        {
            LeaseId = leaseId ?? string.Empty;
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            RequestedAtUtc = requestedAtUtc;
            Generation = generation;
            ReleaseConfirmed = releaseConfirmed;
            ExternalFailureReason =
                externalFailureReason ?? string.Empty;
        }
    }

    public sealed class ResourceLeaseReleaseResult
    {
        public string LeaseId { get; }
        public string ResourceId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string RequestId { get; }
        public LogicalResourceLeaseReleaseStatus Status { get; }
        public string FailureReason { get; }
        public DateTime OccurredAtUtc { get; }
        public int Generation { get; }

        public ResourceLeaseReleaseResult(
            ResourceLeaseReleaseRequest request,
            LogicalResourceLeaseReleaseStatus status,
            string failureReason)
        {
            LeaseId = request != null ? request.LeaseId : string.Empty;
            ResourceId =
                request != null ? request.ResourceId : string.Empty;
            PlanId = request != null ? request.PlanId : string.Empty;
            StepId = request != null ? request.StepId : string.Empty;
            RequestId =
                request != null ? request.RequestId : string.Empty;
            Status = status;
            FailureReason = failureReason ?? string.Empty;
            OccurredAtUtc = request != null
                ? request.RequestedAtUtc : default(DateTime);
            Generation = request != null ? request.Generation : 0;
        }
    }

    public sealed class LogicalResourceForceReleaseRequest
    {
        public string RequestId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ResourceId { get; }
        public DateTime RequestedAtUtc { get; }
        public int Generation { get; }

        public LogicalResourceForceReleaseRequest(
            string requestId, string planId, string stepId,
            string resourceId, DateTime requestedAtUtc,
            int generation)
        {
            RequestId = requestId ?? string.Empty;
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            RequestedAtUtc = requestedAtUtc;
            Generation = generation;
        }
    }

    public sealed class LogicalResourceForceReleaseResult
    {
        public string RequestId { get; }
        public LogicalResourceForceReleaseStatus Status { get; }
        public IReadOnlyList<string> AffectedLeaseIds { get; }
        public string FailureReason { get; }
        public DateTime OccurredAtUtc { get; }
        public int Generation { get; }

        public LogicalResourceForceReleaseResult(
            LogicalResourceForceReleaseRequest request,
            LogicalResourceForceReleaseStatus status,
            IEnumerable<string> affectedLeaseIds,
            string failureReason)
        {
            RequestId =
                request != null ? request.RequestId : string.Empty;
            Status = status;
            AffectedLeaseIds = Copy(affectedLeaseIds);
            FailureReason = failureReason ?? string.Empty;
            OccurredAtUtc = request != null
                ? request.RequestedAtUtc : default(DateTime);
            Generation = request != null ? request.Generation : 0;
        }

        private static IReadOnlyList<T> Copy<T>(
            IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(
                source != null
                    ? new List<T>(source)
                    : new List<T>());
        }
    }

    public sealed class LogicalResourceLeaseSnapshot
    {
        public string LeaseId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public ExecutionResourceLeaseStatus Status { get; }
        public DateTime AcquiredAtUtc { get; }
        public DateTime? ReleasedAtUtc { get; }
        public bool ForceReleaseRequested { get; }
        public string FailureReason { get; }

        public LogicalResourceLeaseSnapshot(
            string leaseId, string resourceId, string resourceType,
            string planId, string stepId,
            string executionAttemptId,
            ExecutionResourceAccessMode accessMode,
            ExecutionResourceLeaseStatus status,
            DateTime acquiredAtUtc, DateTime? releasedAtUtc,
            bool forceReleaseRequested, string failureReason)
        {
            LeaseId = leaseId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            ResourceType = resourceType ?? string.Empty;
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            ExecutionAttemptId =
                executionAttemptId ?? string.Empty;
            AccessMode = accessMode;
            Status = status;
            AcquiredAtUtc = acquiredAtUtc;
            ReleasedAtUtc = releasedAtUtc;
            ForceReleaseRequested = forceReleaseRequested;
            FailureReason = failureReason ?? string.Empty;
        }

        public LogicalResourceLeaseSnapshot WithStatus(
            ExecutionResourceLeaseStatus status,
            DateTime? releasedAtUtc,
            bool forceReleaseRequested,
            string failureReason)
        {
            return new LogicalResourceLeaseSnapshot(
                LeaseId, ResourceId, ResourceType, PlanId, StepId,
                ExecutionAttemptId, AccessMode, status,
                AcquiredAtUtc, releasedAtUtc,
                forceReleaseRequested, failureReason);
        }
    }

    public sealed class LogicalResourceLeaseState
    {
        public const int MaxActiveLeases = 256;
        public const int MaxHistoryEntries = 512;
        public const int MaxProcessedRequestIds = 65536;
        public const int MaxKnownLeaseIds = 65536;

        public int Generation { get; }
        public IReadOnlyList<LogicalResourceLeaseSnapshot>
            ActiveLeases { get; }
        public IReadOnlyList<LogicalResourceLeaseSnapshot>
            History { get; }
        public IReadOnlyList<string> ProcessedRequestIds { get; }
        public IReadOnlyList<string> KnownLeaseIds { get; }

        public LogicalResourceLeaseState(
            int generation,
            IEnumerable<LogicalResourceLeaseSnapshot> activeLeases,
            IEnumerable<LogicalResourceLeaseSnapshot> history,
            IEnumerable<string> processedRequestIds,
            IEnumerable<string> knownLeaseIds)
        {
            Generation = generation;
            ActiveLeases = Copy(activeLeases);
            History = Copy(history);
            ProcessedRequestIds = Copy(processedRequestIds);
            KnownLeaseIds = Copy(knownLeaseIds);
        }

        public static LogicalResourceLeaseState Empty(int generation)
        {
            return new LogicalResourceLeaseState(
                generation,
                new LogicalResourceLeaseSnapshot[0],
                new LogicalResourceLeaseSnapshot[0],
                new string[0], new string[0]);
        }

        public LogicalResourceLeaseSnapshot FindActive(
            string leaseId)
        {
            for (int i = 0; i < ActiveLeases.Count; i++)
                if (Same(ActiveLeases[i].LeaseId, leaseId))
                    return ActiveLeases[i];
            return null;
        }

        public bool HasProcessedRequest(string requestId)
        {
            return Contains(ProcessedRequestIds, requestId);
        }

        public bool HasKnownLease(string leaseId)
        {
            return Contains(KnownLeaseIds, leaseId);
        }

        private static bool Contains(
            IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (Same(values[i], value)) return true;
            return false;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(
                left, right, StringComparison.Ordinal);
        }

        private static IReadOnlyList<T> Copy<T>(
            IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(
                source != null
                    ? new List<T>(source)
                    : new List<T>());
        }
    }

    public sealed class LogicalResourceLeaseTransition<TResult>
        where TResult : class
    {
        public LogicalResourceLeaseState State { get; }
        public TResult Result { get; }

        public LogicalResourceLeaseTransition(
            LogicalResourceLeaseState state, TResult result)
        {
            State = state;
            Result = result;
        }
    }
}
