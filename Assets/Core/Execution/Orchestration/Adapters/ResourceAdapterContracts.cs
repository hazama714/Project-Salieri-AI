// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using SalieriAI.Core.Execution.Orchestration.Contracts;

namespace SalieriAI.Core.Execution.Orchestration.Adapters
{
    public sealed class ResourceAcquireRequest
    {
        public string RequestId { get; }
        public string EffectIntentId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public DateTime RequestedAtUtc { get; }

        public ResourceAcquireRequest(
            string requestId, string effectIntentId, string planId,
            string stepId, string executionAttemptId,
            string resourceId, string resourceType,
            ExecutionResourceAccessMode accessMode,
            DateTime requestedAtUtc)
        {
            RequestId = AdapterContractUtility.Text(requestId);
            EffectIntentId = AdapterContractUtility.Text(effectIntentId);
            PlanId = AdapterContractUtility.Text(planId);
            StepId = AdapterContractUtility.Text(stepId);
            ExecutionAttemptId =
                AdapterContractUtility.Text(executionAttemptId);
            ResourceId = AdapterContractUtility.Text(resourceId);
            ResourceType = AdapterContractUtility.Text(resourceType);
            AccessMode = accessMode;
            RequestedAtUtc = AdapterContractUtility.Utc(requestedAtUtc);
        }
    }

    public sealed class ResourceAcquireResult
    {
        public string RequestId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string ExternalLeaseId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public ExecutionResourceLeaseStatus Status { get; }
        public string FailureReason { get; }
        public DateTime OccurredAtUtc { get; }

        public ResourceAcquireResult(
            string requestId, string planId, string stepId,
            string executionAttemptId, string externalLeaseId,
            string resourceId, string resourceType,
            ExecutionResourceAccessMode accessMode,
            ExecutionResourceLeaseStatus status,
            string failureReason, DateTime occurredAtUtc)
        {
            RequestId = AdapterContractUtility.Text(requestId);
            PlanId = AdapterContractUtility.Text(planId);
            StepId = AdapterContractUtility.Text(stepId);
            ExecutionAttemptId =
                AdapterContractUtility.Text(executionAttemptId);
            ExternalLeaseId =
                AdapterContractUtility.Text(externalLeaseId);
            ResourceId = AdapterContractUtility.Text(resourceId);
            ResourceType = AdapterContractUtility.Text(resourceType);
            AccessMode = accessMode;
            Status = status;
            FailureReason =
                AdapterContractUtility.Text(failureReason);
            OccurredAtUtc = AdapterContractUtility.Utc(occurredAtUtc);
        }
    }

    public sealed class ResourceReleaseRequest
    {
        public string RequestId { get; }
        public string EffectIntentId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExternalLeaseId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public DateTime RequestedAtUtc { get; }

        public ResourceReleaseRequest(
            string requestId, string effectIntentId, string planId,
            string stepId, string externalLeaseId,
            string resourceId, string resourceType,
            ExecutionResourceAccessMode accessMode,
            DateTime requestedAtUtc)
        {
            RequestId = AdapterContractUtility.Text(requestId);
            EffectIntentId = AdapterContractUtility.Text(effectIntentId);
            PlanId = AdapterContractUtility.Text(planId);
            StepId = AdapterContractUtility.Text(stepId);
            ExternalLeaseId =
                AdapterContractUtility.Text(externalLeaseId);
            ResourceId = AdapterContractUtility.Text(resourceId);
            ResourceType = AdapterContractUtility.Text(resourceType);
            AccessMode = accessMode;
            RequestedAtUtc = AdapterContractUtility.Utc(requestedAtUtc);
        }
    }

    public sealed class ResourceReleaseResult
    {
        public string RequestId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExternalLeaseId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public ExecutionResourceLeaseStatus Status { get; }
        public string FailureReason { get; }
        public DateTime OccurredAtUtc { get; }

        public ResourceReleaseResult(
            string requestId, string planId, string stepId,
            string externalLeaseId, string resourceId,
            string resourceType,
            ExecutionResourceAccessMode accessMode,
            ExecutionResourceLeaseStatus status,
            string failureReason, DateTime occurredAtUtc)
        {
            RequestId = AdapterContractUtility.Text(requestId);
            PlanId = AdapterContractUtility.Text(planId);
            StepId = AdapterContractUtility.Text(stepId);
            ExternalLeaseId =
                AdapterContractUtility.Text(externalLeaseId);
            ResourceId = AdapterContractUtility.Text(resourceId);
            ResourceType = AdapterContractUtility.Text(resourceType);
            AccessMode = accessMode;
            Status = status;
            FailureReason =
                AdapterContractUtility.Text(failureReason);
            OccurredAtUtc = AdapterContractUtility.Utc(occurredAtUtc);
        }
    }
}
