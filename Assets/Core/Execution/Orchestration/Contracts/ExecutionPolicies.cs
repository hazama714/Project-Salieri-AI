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
    public sealed class ExecutionCompletionPolicy
    {
        public ExecutionCompletionKind Kind { get; }
        public ExecutionDomainStage RequiredDomainStage { get; }
        public bool RequiresVerifiedCompletion { get; }
        public bool AllowsUnverifiedCompletion { get; }
        public bool RequiresGoalCondition { get; }

        public ExecutionCompletionPolicy(
            ExecutionCompletionKind kind,
            ExecutionDomainStage requiredDomainStage,
            bool requiresVerifiedCompletion,
            bool allowsUnverifiedCompletion,
            bool requiresGoalCondition)
        {
            Kind = kind;
            RequiredDomainStage = requiredDomainStage;
            RequiresVerifiedCompletion = requiresVerifiedCompletion;
            AllowsUnverifiedCompletion = allowsUnverifiedCompletion;
            RequiresGoalCondition = requiresGoalCondition;
        }
    }

    public sealed class ExecutionPermissionWaitPolicy
    {
        public ExecutionPermissionWaitBehavior Behavior { get; }
        public double TimeoutSeconds { get; }
        public string FallbackStepId { get; }

        public ExecutionPermissionWaitPolicy(
            ExecutionPermissionWaitBehavior behavior,
            double timeoutSeconds,
            string fallbackStepId)
        {
            Behavior = behavior;
            TimeoutSeconds = timeoutSeconds;
            FallbackStepId =
                ExecutionContractUtility.Text(fallbackStepId);
        }
    }

    public sealed class ExecutionTimeoutPolicy
    {
        public double TimeoutSeconds { get; }
        public ExecutionTimeoutAction Action { get; }

        public ExecutionTimeoutPolicy(
            double timeoutSeconds,
            ExecutionTimeoutAction action)
        {
            TimeoutSeconds = timeoutSeconds;
            Action = action;
        }
    }

    /// <summary>
    /// Explicit policy for the pre-attempt resource-wait cycle. This is
    /// intentionally separate from ExecutionTimeoutPolicy because resource
    /// waiting can begin before an execution attempt exists.
    /// </summary>
    public sealed class ExecutionResourceWaitTimeoutPolicy
    {
        public bool TimeoutEnabled { get; }
        public double TimeoutSeconds { get; }
        public ExecutionTimeoutAction Action { get; }

        public ExecutionResourceWaitTimeoutPolicy(
            bool timeoutEnabled,
            double timeoutSeconds,
            ExecutionTimeoutAction action)
        {
            TimeoutEnabled = timeoutEnabled;
            TimeoutSeconds = timeoutSeconds;
            Action = action;
        }

        public static ExecutionResourceWaitTimeoutPolicy Disabled()
        {
            return new ExecutionResourceWaitTimeoutPolicy(
                false,
                0d,
                ExecutionTimeoutAction.Fail
            );
        }

        public static ExecutionResourceWaitTimeoutPolicy Enabled(
            double timeoutSeconds,
            ExecutionTimeoutAction action)
        {
            return new ExecutionResourceWaitTimeoutPolicy(
                true,
                timeoutSeconds,
                action
            );
        }
    }

    public sealed class ExecutionRetryPolicy
    {
        public int MaxAttempts { get; }
        public IReadOnlyList<string> RetryableFailureCodes { get; }
        public bool RequiresNewAttemptAndRequestIds { get; }

        public ExecutionRetryPolicy(
            int maxAttempts,
            IEnumerable<string> retryableFailureCodes,
            bool requiresNewAttemptAndRequestIds)
        {
            MaxAttempts = maxAttempts;
            RetryableFailureCodes =
                ExecutionContractUtility.ReadOnlyCopy(
                    retryableFailureCodes
                );
            RequiresNewAttemptAndRequestIds =
                requiresNewAttemptAndRequestIds;
        }
    }

    public sealed class ExecutionFailurePolicy
    {
        public ExecutionFailureAction Action { get; }
        public string FallbackStepId { get; }
        public bool RecordDiagnostic { get; }

        public ExecutionFailurePolicy(
            ExecutionFailureAction action,
            string fallbackStepId,
            bool recordDiagnostic)
        {
            Action = action;
            FallbackStepId =
                ExecutionContractUtility.Text(fallbackStepId);
            RecordDiagnostic = recordDiagnostic;
        }
    }

    public sealed class ExecutionCancellationPolicy
    {
        public ExecutionCancellationScope Scope { get; }
        public bool RequiresCleanup { get; }
        public double EffectTimeoutSeconds { get; }

        public ExecutionCancellationPolicy(
            ExecutionCancellationScope scope,
            bool requiresCleanup,
            double effectTimeoutSeconds)
        {
            Scope = scope;
            RequiresCleanup = requiresCleanup;
            EffectTimeoutSeconds = effectTimeoutSeconds;
        }
    }

    public sealed class ExecutionSafetyPolicy
    {
        public string SafetyGroupId { get; }
        public bool Preemptible { get; }
        public bool BypassNormalQueueForSafety { get; }

        public ExecutionSafetyPolicy(
            string safetyGroupId,
            bool preemptible,
            bool bypassNormalQueueForSafety)
        {
            SafetyGroupId =
                ExecutionContractUtility.Text(safetyGroupId);
            Preemptible = preemptible;
            BypassNormalQueueForSafety =
                bypassNormalQueueForSafety;
        }
    }

    public sealed class ExecutionPlanCompletionPolicy
    {
        public bool RequiresAllRequiredSteps { get; }
        public ExecutionOptionalFailureBehavior
            OptionalFailureBehavior { get; }
        public bool AllowsPartialCompletion { get; }

        public ExecutionPlanCompletionPolicy(
            bool requiresAllRequiredSteps,
            ExecutionOptionalFailureBehavior optionalFailureBehavior,
            bool allowsPartialCompletion)
        {
            RequiresAllRequiredSteps = requiresAllRequiredSteps;
            OptionalFailureBehavior = optionalFailureBehavior;
            AllowsPartialCompletion = allowsPartialCompletion;
        }
    }
}
