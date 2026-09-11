// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;

namespace SalieriAI.Core.Execution.Orchestration.Runtime
{
    /// <summary>
    /// A resource-availability observation. It is not a lease-service result
    /// and therefore must never be represented as LeaseRejected.
    /// </summary>
    public sealed class ExecutionResourceAvailabilityEventPayload
    {
        public string ResourceId { get; }
        public ResourceShadowAvailabilityStatus Availability { get; }
        public string SnapshotRevision { get; }
        public DateTime ObservedAtUtc { get; }
        public string RequestId { get; }
        public string TriggerId { get; }
        public string ResourceWaitCycleId { get; }
        public string Diagnostic { get; }

        public ExecutionResourceAvailabilityEventPayload(
            string resourceId,
            ResourceShadowAvailabilityStatus availability,
            string snapshotRevision,
            DateTime observedAtUtc,
            string requestId,
            string triggerId,
            string resourceWaitCycleId,
            string diagnostic)
        {
            ResourceId = ExecutionContractUtility.Text(resourceId);
            Availability = availability;
            SnapshotRevision =
                ExecutionContractUtility.Text(snapshotRevision);
            ObservedAtUtc = ExecutionContractUtility.Utc(observedAtUtc);
            RequestId = ExecutionContractUtility.Text(requestId);
            TriggerId = ExecutionContractUtility.Text(triggerId);
            ResourceWaitCycleId =
                ExecutionContractUtility.Text(resourceWaitCycleId);
            Diagnostic = ExecutionContractUtility.Text(diagnostic);
        }
    }

    /// <summary>
    /// Explicit timeout fact for a pre-attempt resource-wait cycle. The
    /// evaluation time is supplied by the caller; the payload owns no clock.
    /// </summary>
    public sealed class ExecutionResourceWaitTimeoutEventPayload
    {
        public string ResourceWaitCycleId { get; }
        public DateTime EvaluationTimeUtc { get; }
        public DateTime DeadlineUtc { get; }

        public ExecutionResourceWaitTimeoutEventPayload(
            string resourceWaitCycleId,
            DateTime evaluationTimeUtc,
            DateTime deadlineUtc)
        {
            ResourceWaitCycleId =
                ExecutionContractUtility.Text(resourceWaitCycleId);
            EvaluationTimeUtc =
                ExecutionContractUtility.Utc(evaluationTimeUtc);
            DeadlineUtc = ExecutionContractUtility.Utc(deadlineUtc);
        }
    }
}
