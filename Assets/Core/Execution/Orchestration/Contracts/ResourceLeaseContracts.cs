// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    public enum ExecutionResourceLeaseStatus
    {
        Requested = 0,
        Acquired = 1,
        ReleaseRequested = 2,
        Released = 3,
        Rejected = 4,
        ReleaseFailed = 5,
        ForceReleaseRequested = 6
    }

    /// <summary>
    /// Immutable resource-level lease correlation. Ownership belongs to a
    /// step; an execution attempt may be associated but is not required.
    /// </summary>
    public sealed class ResourceLeaseReference
    {
        public string LeaseId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public DateTime AcquiredAtUtc { get; }
        public ExecutionResourceLeaseStatus Status { get; }

        public ResourceLeaseReference(
            string leaseId,
            string resourceId,
            string resourceType,
            string planId,
            string stepId,
            string executionAttemptId,
            ExecutionResourceAccessMode accessMode,
            DateTime acquiredAtUtc,
            ExecutionResourceLeaseStatus status)
        {
            LeaseId = ExecutionContractUtility.Text(leaseId);
            ResourceId = ExecutionContractUtility.Text(resourceId);
            ResourceType = ExecutionContractUtility.Text(resourceType);
            PlanId = ExecutionContractUtility.Text(planId);
            StepId = ExecutionContractUtility.Text(stepId);
            ExecutionAttemptId =
                ExecutionContractUtility.Text(executionAttemptId);
            AccessMode = accessMode;
            AcquiredAtUtc = ExecutionContractUtility.Utc(acquiredAtUtc);
            Status = status;
        }

        public ResourceLeaseReference WithStatus(
            ExecutionResourceLeaseStatus status)
        {
            return new ResourceLeaseReference(
                LeaseId,
                ResourceId,
                ResourceType,
                PlanId,
                StepId,
                ExecutionAttemptId,
                AccessMode,
                AcquiredAtUtc,
                status
            );
        }
    }

    /// <summary>
    /// Type-safe payload for lease facts. A rejected acquisition has no
    /// lease ID; acquired/released facts must carry one.
    /// </summary>
    public sealed class ExecutionResourceLeaseEventPayload
    {
        public ResourceLeaseReference Lease { get; }
        public string Reason { get; }

        public ExecutionResourceLeaseEventPayload(
            ResourceLeaseReference lease,
            string reason)
        {
            Lease = lease;
            Reason = ExecutionContractUtility.Text(reason);
        }
    }

    /// <summary>
    /// Type-safe resource correlation for an acquire or release intent.
    /// Creating this payload does not mean that the operation succeeded.
    /// </summary>
    public sealed class ExecutionResourceLeaseIntentPayload
    {
        public string LeaseId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public ExecutionResourceAccessMode AccessMode { get; }

        public ExecutionResourceLeaseIntentPayload(
            string leaseId,
            string resourceId,
            string resourceType,
            ExecutionResourceAccessMode accessMode)
        {
            LeaseId = ExecutionContractUtility.Text(leaseId);
            ResourceId = ExecutionContractUtility.Text(resourceId);
            ResourceType = ExecutionContractUtility.Text(resourceType);
            AccessMode = accessMode;
        }
    }
}
