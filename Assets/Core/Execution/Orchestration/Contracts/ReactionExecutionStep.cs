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
    public sealed class ReactionExecutionStep
    {
        public string StepId { get; }
        public string PlanId { get; }
        public ExecutionStepType StepType { get; }
        public ExecutionStepRole Role { get; }
        public ExecutionDomain Domain { get; }
        public string RequestId { get; }
        public ExecutionRequiredness Requiredness { get; }
        public int Priority { get; }
        public IReadOnlyList<ExecutionResourceRequirement>
            RequiredResources { get; }
        public ExecutionCompletionPolicy CompletionPolicy { get; }
        public ExecutionPermissionWaitPolicy PermissionWaitPolicy
        {
            get;
        }
        public ExecutionTimeoutPolicy TimeoutPolicy { get; }
        public ExecutionResourceWaitTimeoutPolicy ResourceWaitTimeoutPolicy
        {
            get;
        }
        public ExecutionRetryPolicy RetryPolicy { get; }
        public ExecutionFailurePolicy FailurePolicy { get; }
        public ExecutionCancellationPolicy CancellationPolicy { get; }
        public ExecutionRequestReference RequestReference { get; }

        public ReactionExecutionStep(
            string stepId,
            string planId,
            ExecutionStepType stepType,
            ExecutionStepRole role,
            ExecutionDomain domain,
            string requestId,
            ExecutionRequiredness requiredness,
            int priority,
            IEnumerable<ExecutionResourceRequirement> requiredResources,
            ExecutionCompletionPolicy completionPolicy,
            ExecutionPermissionWaitPolicy permissionWaitPolicy,
            ExecutionTimeoutPolicy timeoutPolicy,
            ExecutionRetryPolicy retryPolicy,
            ExecutionFailurePolicy failurePolicy,
            ExecutionCancellationPolicy cancellationPolicy,
            ExecutionRequestReference requestReference,
            ExecutionResourceWaitTimeoutPolicy resourceWaitTimeoutPolicy =
                null)
        {
            StepId = ExecutionContractUtility.Text(stepId);
            PlanId = ExecutionContractUtility.Text(planId);
            StepType = stepType;
            Role = role;
            Domain = domain;
            RequestId = ExecutionContractUtility.Text(requestId);
            Requiredness = requiredness;
            Priority = priority;
            RequiredResources =
                ExecutionContractUtility.ReadOnlyCopy(
                    requiredResources
                );
            CompletionPolicy = completionPolicy;
            PermissionWaitPolicy = permissionWaitPolicy;
            TimeoutPolicy = timeoutPolicy;
            ResourceWaitTimeoutPolicy = resourceWaitTimeoutPolicy ??
                ExecutionResourceWaitTimeoutPolicy.Disabled();
            RetryPolicy = retryPolicy;
            FailurePolicy = failurePolicy;
            CancellationPolicy = cancellationPolicy;
            RequestReference = requestReference;
        }
    }
}
