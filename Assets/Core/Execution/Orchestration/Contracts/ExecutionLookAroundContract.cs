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
    /// Project Salieri lookAround Orchestration Contract v0.1.
    /// This is the single Production source for lookAround action and plan
    /// semantics. It does not register a caller or grant live Body authority.
    /// </summary>
    public static class ExecutionLookAroundContract
    {
        public const string ActionId = "lookAround";
        public const string SafetyGroupId = "NORMAL_BODY_ACTION";
        public const string PayloadSchemaVersion =
            "execution-look-around-payload-v0.1";

        public const ExecutionPlanType PlanType =
            ExecutionPlanType.Reaction;
        public const ExecutionPolicyKind PolicyKind =
            ExecutionPolicyKind.NoAcknowledgement;
        public const ExecutionDomain Domain =
            ExecutionDomain.PhysicalBody;
        public const ExecutionStepType StepType =
            ExecutionStepType.PhysicalBody;
        public const ExecutionStepRole StepRole =
            ExecutionStepRole.PrimaryAction;
        public const ExecutionRequiredness Requiredness =
            ExecutionRequiredness.Required;

        public static ExecutionResourceRequirement[]
            CreateResourceRequirements()
        {
            return new[]
            {
                new ExecutionResourceRequirement(
                    ExecutionResourceIds.HeadMotion,
                    ExecutionResourceAccessMode.Exclusive,
                    ExecutionRequiredness.Required,
                    ExecutionResourceLeaseScope.Step,
                    0,
                    ExecutionResourceReleasePolicy.OnStepTerminal)
            };
        }

        public static ExecutionCompletionPolicy CreateCompletionPolicy()
        {
            return new ExecutionCompletionPolicy(
                ExecutionCompletionKind
                    .PhysicalCompletionUnverifiedAllowed,
                new ExecutionDomainStage(
                    ExecutionDomainStageIds.PhysicalDispatched),
                false,
                true,
                false);
        }

        public static ExecutionDelegatedPhysicalBodyActionContract
            CreateDelegatedBodyActionContract()
        {
            return new ExecutionDelegatedPhysicalBodyActionContract(
                true,
                false,
                new ExecutionBodyActionCancellationCapability(
                    false,
                    false,
                    false,
                    false));
        }

        public static ExecutionPermissionWaitPolicy
            CreatePermissionWaitPolicy()
        {
            return new ExecutionPermissionWaitPolicy(
                ExecutionPermissionWaitBehavior.RejectImmediately,
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
            // MaxAttempts includes the initial execution attempt.
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
            CreateStepCancellationPolicy()
        {
            return new ExecutionCancellationPolicy(
                ExecutionCancellationScope.Step,
                false,
                0d);
        }

        public static ExecutionCancellationPolicy
            CreatePlanCancellationPolicy()
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
                false,
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
                CreatePlanCancellationPolicy());
        }

        public static bool IsLookAroundStep(ReactionExecutionStep step)
        {
            return step != null &&
                step.Domain == Domain &&
                step.StepType == StepType &&
                step.RequestReference != null &&
                step.RequestReference.RequestTypeId == ActionId;
        }

        public static bool MatchesDescriptor(
            ExecutionActionDescriptor descriptor)
        {
            return descriptor != null &&
                descriptor.ActionId == ActionId &&
                descriptor.Domain == Domain &&
                descriptor.StepType == StepType &&
                descriptor.StepRole == StepRole &&
                descriptor.PlanType == PlanType &&
                descriptor.PolicyKind == PolicyKind &&
                descriptor.PayloadSchemaVersion == PayloadSchemaVersion &&
                MatchesResources(descriptor.ResourceRequirements) &&
                MatchesCompletionPolicy(descriptor.CompletionPolicy) &&
                MatchesPermissionPolicy(
                    descriptor.PermissionWaitPolicy) &&
                MatchesTimeoutPolicy(descriptor.TimeoutPolicy) &&
                MatchesResourceWaitPolicy(
                    descriptor.ResourceWaitTimeoutPolicy) &&
                MatchesRetryPolicy(descriptor.RetryPolicy) &&
                MatchesFailurePolicy(descriptor.FailurePolicy) &&
                MatchesStepCancellationPolicy(
                    descriptor.CancellationPolicy) &&
                MatchesSafetyPolicy(descriptor.SafetyPolicy);
        }

        public static bool MatchesStep(ReactionExecutionStep step)
        {
            return IsLookAroundStep(step) &&
                step.Role == StepRole &&
                step.Requiredness == Requiredness &&
                MatchesResources(step.RequiredResources) &&
                MatchesCompletionPolicy(step.CompletionPolicy) &&
                MatchesPermissionPolicy(step.PermissionWaitPolicy) &&
                MatchesTimeoutPolicy(step.TimeoutPolicy) &&
                MatchesResourceWaitPolicy(
                    step.ResourceWaitTimeoutPolicy) &&
                MatchesRetryPolicy(step.RetryPolicy) &&
                MatchesFailurePolicy(step.FailurePolicy) &&
                MatchesStepCancellationPolicy(step.CancellationPolicy);
        }

        public static bool MatchesResources(
            System.Collections.Generic.IReadOnlyList<
                ExecutionResourceRequirement> resources)
        {
            if (resources == null || resources.Count != 1)
                return false;
            ExecutionResourceRequirement resource = resources[0];
            return resource != null &&
                resource.ResourceId == ExecutionResourceIds.HeadMotion &&
                resource.AccessMode ==
                    ExecutionResourceAccessMode.Exclusive &&
                resource.Requiredness == ExecutionRequiredness.Required &&
                resource.LeaseScope == ExecutionResourceLeaseScope.Step &&
                resource.AcquisitionOrderHint == 0 &&
                resource.ReleasePolicy ==
                    ExecutionResourceReleasePolicy.OnStepTerminal;
        }

        public static bool MatchesCompletionPolicy(
            ExecutionCompletionPolicy policy)
        {
            return policy != null &&
                policy.Kind == ExecutionCompletionKind
                    .PhysicalCompletionUnverifiedAllowed &&
                policy.RequiredDomainStage != null &&
                policy.RequiredDomainStage.StageId ==
                    ExecutionDomainStageIds.PhysicalDispatched &&
                !policy.RequiresVerifiedCompletion &&
                policy.AllowsUnverifiedCompletion &&
                !policy.RequiresGoalCondition;
        }

        public static bool MatchesPermissionPolicy(
            ExecutionPermissionWaitPolicy policy)
        {
            return policy != null &&
                policy.Behavior ==
                    ExecutionPermissionWaitBehavior.RejectImmediately &&
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

        public static bool MatchesStepCancellationPolicy(
            ExecutionCancellationPolicy policy)
        {
            return policy != null &&
                policy.Scope == ExecutionCancellationScope.Step &&
                !policy.RequiresCleanup &&
                policy.EffectTimeoutSeconds == 0d;
        }

        public static bool MatchesPlanCancellationPolicy(
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
                !policy.Preemptible &&
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
                MatchesPlanCancellationPolicy(cancellation);
        }

        public static bool MatchesDelegatedBodyActionContract(
            ExecutionDelegatedPhysicalBodyActionContract contract)
        {
            return contract != null &&
                contract.AllowsUnverifiedCompletion &&
                !contract.SupportsPhysicalCompletionFeedback &&
                !contract.SupportsCancel &&
                !contract.SupportsInterrupt &&
                !contract.CancelEffectConfirmed &&
                !contract.PhysicalStopVerified;
        }
    }
}
