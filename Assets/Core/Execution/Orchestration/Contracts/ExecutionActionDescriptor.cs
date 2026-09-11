// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections.Generic;

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    /// <summary>
    /// Immutable action-level orchestration semantics.
    ///
    /// Plan/step lifecycle identities and request correlation identities are
    /// intentionally excluded. Those values belong to the future Production
    /// Submission and provenance boundaries, not to an ActionId definition.
    /// </summary>
    public sealed class ExecutionActionDescriptor
    {
        public string ActionId { get; }
        public ExecutionDomain Domain { get; }
        public ExecutionStepType StepType { get; }
        public ExecutionStepRole StepRole { get; }
        public IReadOnlyList<ExecutionResourceRequirement>
            ResourceRequirements { get; }
        public ExecutionCompletionPolicy CompletionPolicy { get; }
        public ExecutionPermissionWaitPolicy PermissionWaitPolicy { get; }
        public ExecutionTimeoutPolicy TimeoutPolicy { get; }
        public ExecutionResourceWaitTimeoutPolicy ResourceWaitTimeoutPolicy
        {
            get;
        }
        public ExecutionRetryPolicy RetryPolicy { get; }
        public ExecutionFailurePolicy FailurePolicy { get; }
        public ExecutionCancellationPolicy CancellationPolicy { get; }
        public ExecutionSafetyPolicy SafetyPolicy { get; }
        public ExecutionOrchestrationPolicyVersion PolicyVersion { get; }
        public ExecutionPlanType PlanType { get; }
        public ExecutionPolicyKind PolicyKind { get; }
        public string PayloadSchemaVersion { get; }

        public ExecutionActionDescriptor(
            string actionId,
            ExecutionDomain domain,
            ExecutionStepType stepType,
            ExecutionStepRole stepRole,
            IEnumerable<ExecutionResourceRequirement>
                resourceRequirements,
            ExecutionCompletionPolicy completionPolicy,
            ExecutionPermissionWaitPolicy permissionWaitPolicy,
            ExecutionTimeoutPolicy timeoutPolicy,
            ExecutionResourceWaitTimeoutPolicy resourceWaitTimeoutPolicy,
            ExecutionRetryPolicy retryPolicy,
            ExecutionFailurePolicy failurePolicy,
            ExecutionCancellationPolicy cancellationPolicy,
            ExecutionSafetyPolicy safetyPolicy,
            ExecutionOrchestrationPolicyVersion policyVersion,
            ExecutionPlanType planType,
            ExecutionPolicyKind policyKind,
            string payloadSchemaVersion)
        {
            ActionId = ExecutionContractUtility.Text(actionId);
            Domain = domain;
            StepType = stepType;
            StepRole = stepRole;
            ResourceRequirements =
                ExecutionContractUtility.ReadOnlyCopy(resourceRequirements);
            CompletionPolicy = completionPolicy;
            PermissionWaitPolicy = permissionWaitPolicy;
            TimeoutPolicy = timeoutPolicy;
            ResourceWaitTimeoutPolicy = resourceWaitTimeoutPolicy;
            RetryPolicy = retryPolicy;
            FailurePolicy = failurePolicy;
            CancellationPolicy = cancellationPolicy;
            SafetyPolicy = safetyPolicy;
            PolicyVersion = policyVersion;
            PlanType = planType;
            PolicyKind = policyKind;
            PayloadSchemaVersion =
                ExecutionContractUtility.Text(payloadSchemaVersion);
        }
    }
}
