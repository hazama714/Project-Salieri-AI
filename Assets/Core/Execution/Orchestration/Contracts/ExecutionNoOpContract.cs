// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    /// <summary>
    /// First-class orchestration semantics for an internally completed step
    /// with no permission, resource lease, executor, or external action.
    /// This is a reusable step contract and is not a Production Action
    /// registration or a global policy default.
    /// </summary>
    public static class ExecutionNoOpContract
    {
        public const string SafetyGroupId = "NO_OP";
        public const string PayloadSchemaVersion =
            "execution-no-op-payload-v0.1";

        public const ExecutionPlanType PlanType =
            ExecutionPlanType.NoOp;
        public const ExecutionPolicyKind PolicyKind =
            ExecutionPolicyKind.NoOp;
        public const ExecutionDomain Domain = ExecutionDomain.NoOp;
        public const ExecutionStepType StepType = ExecutionStepType.NoOp;
        public const ExecutionStepRole StepRole =
            ExecutionStepRole.PrimaryAction;
        public const ExecutionRequiredness Requiredness =
            ExecutionRequiredness.Required;

        public static ExecutionCompletionPolicy CreateCompletionPolicy()
        {
            return new ExecutionCompletionPolicy(
                ExecutionCompletionKind.LogicalNoOpCompleted,
                new ExecutionDomainStage(
                    ExecutionDomainStageIds.LogicalNoOpCompleted),
                false,
                false,
                false);
        }

        public static ExecutionPermissionWaitPolicy
            CreatePermissionWaitPolicy()
        {
            return new ExecutionPermissionWaitPolicy(
                ExecutionPermissionWaitBehavior.None,
                0d,
                string.Empty);
        }

        public static ExecutionTimeoutPolicy CreateTimeoutPolicy()
        {
            return new ExecutionTimeoutPolicy(
                0d,
                ExecutionTimeoutAction.Fail);
        }

        public static ExecutionResourceWaitTimeoutPolicy
            CreateResourceWaitTimeoutPolicy()
        {
            return ExecutionResourceWaitTimeoutPolicy.Disabled();
        }

        public static ExecutionRetryPolicy CreateRetryPolicy()
        {
            return new ExecutionRetryPolicy(1, new string[0], false);
        }

        public static ExecutionFailurePolicy CreateFailurePolicy()
        {
            return new ExecutionFailurePolicy(
                ExecutionFailureAction.FailPlan,
                string.Empty,
                true);
        }

        public static ExecutionCancellationPolicy
            CreateCancellationPolicy()
        {
            return new ExecutionCancellationPolicy(
                ExecutionCancellationScope.Plan,
                false,
                0d);
        }

        public static ExecutionSafetyPolicy CreateSafetyPolicy()
        {
            return new ExecutionSafetyPolicy(
                SafetyGroupId,
                true,
                false);
        }

        public static ExecutionPlanCompositionPolicy
            CreatePlanCompositionPolicy()
        {
            return new ExecutionPlanCompositionPolicy(
                Requiredness,
                new ExecutionPlanCompletionPolicy(
                    true,
                    ExecutionOptionalFailureBehavior.DiagnosticOnly,
                    false),
                CreateFailurePolicy(),
                CreateCancellationPolicy());
        }

        public static bool IsNoOpStep(ReactionExecutionStep step)
        {
            return step != null &&
                step.Domain == Domain &&
                step.StepType == StepType;
        }

        public static bool MatchesCompletionPolicy(
            ExecutionCompletionPolicy policy)
        {
            return policy != null &&
                policy.Kind ==
                    ExecutionCompletionKind.LogicalNoOpCompleted &&
                policy.RequiredDomainStage != null &&
                policy.RequiredDomainStage.StageId ==
                    ExecutionDomainStageIds.LogicalNoOpCompleted &&
                !policy.RequiresVerifiedCompletion &&
                !policy.AllowsUnverifiedCompletion &&
                !policy.RequiresGoalCondition;
        }

        public static bool MatchesPermissionPolicy(
            ExecutionPermissionWaitPolicy policy)
        {
            return policy != null &&
                policy.Behavior == ExecutionPermissionWaitBehavior.None &&
                policy.TimeoutSeconds == 0d &&
                string.IsNullOrEmpty(policy.FallbackStepId);
        }

        public static bool MatchesTimeoutPolicy(
            ExecutionTimeoutPolicy policy)
        {
            return policy != null &&
                policy.TimeoutSeconds == 0d &&
                policy.Action == ExecutionTimeoutAction.Fail;
        }

        public static bool MatchesResourceWaitPolicy(
            ExecutionResourceWaitTimeoutPolicy policy)
        {
            return policy != null &&
                !policy.TimeoutEnabled &&
                policy.TimeoutSeconds == 0d &&
                policy.Action == ExecutionTimeoutAction.Fail;
        }

        public static bool MatchesRetryPolicy(ExecutionRetryPolicy policy)
        {
            return policy != null &&
                policy.MaxAttempts == 1 &&
                policy.RetryableFailureCodes.Count == 0 &&
                !policy.RequiresNewAttemptAndRequestIds;
        }

        public static bool MatchesFailurePolicy(
            ExecutionFailurePolicy policy)
        {
            return policy != null &&
                policy.Action == ExecutionFailureAction.FailPlan &&
                string.IsNullOrEmpty(policy.FallbackStepId) &&
                policy.RecordDiagnostic;
        }

        public static bool MatchesCancellationPolicy(
            ExecutionCancellationPolicy policy)
        {
            return policy != null &&
                policy.Scope == ExecutionCancellationScope.Plan &&
                !policy.RequiresCleanup &&
                policy.EffectTimeoutSeconds == 0d;
        }

        public static bool MatchesSafetyPolicy(ExecutionSafetyPolicy policy)
        {
            return policy != null &&
                policy.SafetyGroupId == SafetyGroupId &&
                policy.Preemptible &&
                !policy.BypassNormalQueueForSafety;
        }

        public static bool MatchesPlanCompositionPolicy(
            ExecutionPlanCompletionPolicy completion,
            ExecutionFailurePolicy failure,
            ExecutionCancellationPolicy cancellation)
        {
            return completion != null &&
                completion.RequiresAllRequiredSteps &&
                completion.OptionalFailureBehavior ==
                    ExecutionOptionalFailureBehavior.DiagnosticOnly &&
                !completion.AllowsPartialCompletion &&
                MatchesFailurePolicy(failure) &&
                MatchesCancellationPolicy(cancellation);
        }
    }
}
