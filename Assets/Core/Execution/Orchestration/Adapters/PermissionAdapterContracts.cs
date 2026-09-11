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
    public enum PermissionAdapterStatus
    {
        Pending = 0,
        Granted = 1,
        Denied = 2,
        Failed = 3
    }

    public sealed class PermissionCheckRequest
    {
        public string RequestId { get; }
        public string EffectIntentId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string PermissionKind { get; }
        public DateTime RequestedAtUtc { get; }
        public ExecutionTimeoutPolicy TimeoutPolicy { get; }
        public string DiagnosticContext { get; }

        public PermissionCheckRequest(
            string requestId,
            string effectIntentId,
            string planId,
            string stepId,
            string executionAttemptId,
            string permissionKind,
            DateTime requestedAtUtc,
            ExecutionTimeoutPolicy timeoutPolicy,
            string diagnosticContext)
        {
            RequestId = AdapterContractUtility.Text(requestId);
            EffectIntentId = AdapterContractUtility.Text(effectIntentId);
            PlanId = AdapterContractUtility.Text(planId);
            StepId = AdapterContractUtility.Text(stepId);
            ExecutionAttemptId =
                AdapterContractUtility.Text(executionAttemptId);
            PermissionKind = AdapterContractUtility.Text(permissionKind);
            RequestedAtUtc = AdapterContractUtility.Utc(requestedAtUtc);
            TimeoutPolicy = timeoutPolicy;
            DiagnosticContext =
                AdapterContractUtility.Text(diagnosticContext);
        }
    }

    public sealed class PermissionAdapterResult
    {
        public string RequestId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public PermissionAdapterStatus Status { get; }
        public string Reason { get; }
        public DateTime OccurredAtUtc { get; }

        public PermissionAdapterResult(
            string requestId,
            string planId,
            string stepId,
            string executionAttemptId,
            PermissionAdapterStatus status,
            string reason,
            DateTime occurredAtUtc)
        {
            RequestId = AdapterContractUtility.Text(requestId);
            PlanId = AdapterContractUtility.Text(planId);
            StepId = AdapterContractUtility.Text(stepId);
            ExecutionAttemptId =
                AdapterContractUtility.Text(executionAttemptId);
            Status = status;
            Reason = AdapterContractUtility.Text(reason);
            OccurredAtUtc = AdapterContractUtility.Utc(occurredAtUtc);
        }
    }
}
