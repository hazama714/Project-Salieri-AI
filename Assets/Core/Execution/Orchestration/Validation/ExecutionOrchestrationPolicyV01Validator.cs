// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Core.Execution.Orchestration.Contracts;

namespace SalieriAI.Core.Execution.Orchestration.Validation
{
    /// <summary>
    /// Validates only versioned orchestration policy. It does not check live
    /// capability, grounding, safety, authorization, resources, Limbo, or
    /// executor state.
    /// </summary>
    public static class ExecutionOrchestrationPolicyV01Validator
    {
        public const string ValidatorVersion =
            "execution-orchestration-policy-v0.1-3a.1";

        public static ExecutionPlanValidationResult Validate(
            ReactionExecutionPlan plan,
            ExecutionOrchestrationPolicyVersion policyVersion,
            ExecutionPlanValidationContext context)
        {
            ExecutionPlanValidationContext safeContext =
                context ?? new ExecutionPlanValidationContext(
                    string.Empty,
                    default(DateTime)
                );

            try
            {
                ExecutionValidationIssues issues =
                    new ExecutionValidationIssues();

                if (plan == null)
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes.NullPlan,
                        "plan"
                    );
                }
                else
                {
                    ValidateVersion(
                        plan,
                        policyVersion,
                        issues
                    );
                    ValidateRetryRules(plan, issues);

                    if (plan.PlanType == ExecutionPlanType.NoOp)
                    {
                        ValidateNoOpPlan(plan, issues);
                    }
                    else if (plan.PlanType ==
                            ExecutionPlanType.Emergency ||
                        plan.PlanType == ExecutionPlanType.Stop)
                    {
                        ValidatePriorityControlPlan(plan, issues);
                    }
                    else
                    {
                        ValidateNormalBodyAction(plan, issues);
                        ValidateLookAroundPlan(plan, issues);
                    }
                }

                return Result(
                    safeContext,
                    issues,
                    "Policy v0.1 validation completed."
                );
            }
            catch (Exception exception)
            {
                ExecutionValidationIssues issues =
                    new ExecutionValidationIssues();
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InternalValidationError,
                    "plan"
                );

                return Result(
                    safeContext,
                    issues,
                    exception.GetType().Name + ": " +
                    exception.Message
                );
            }
        }

        public static ExecutionPlanValidationResult
            ValidateControlPriorities(
                IEnumerable<ExecutionControlRequest> requests,
                ExecutionPlanValidationContext context)
        {
            ExecutionPlanValidationContext safeContext =
                context ?? new ExecutionPlanValidationContext(
                    string.Empty,
                    default(DateTime)
                );
            ExecutionValidationIssues issues =
                new ExecutionValidationIssues();

            try
            {
                Dictionary<ExecutionControlType, int> seen =
                    new Dictionary<ExecutionControlType, int>();

                if (requests != null)
                {
                    foreach (ExecutionControlRequest request in requests)
                    {
                        if (request == null ||
                            request.ControlType ==
                                ExecutionControlType.None)
                        {
                            issues.AddFailure(
                                ExecutionPlanValidationFailureCodes
                                    .InvalidControlPriority,
                                "controlRequests"
                            );
                            continue;
                        }

                        int policyRank =
                            ExecutionControlPriorityPolicyV01.GetRank(
                                request.ControlType
                            );

                        if (request.Priority != policyRank)
                        {
                            issues.AddFailure(
                                ExecutionPlanValidationFailureCodes
                                    .InvalidControlPriority,
                                "controlRequests.priority"
                            );
                        }

                        seen[request.ControlType] = request.Priority;
                    }
                }

                ValidateRelativePriority(
                    seen,
                    ExecutionControlType.EmergencyPreempt,
                    ExecutionControlType.SafetyStop,
                    issues
                );
                ValidateRelativePriority(
                    seen,
                    ExecutionControlType.SafetyStop,
                    ExecutionControlType.Interrupt,
                    issues
                );
                ValidateRelativePriority(
                    seen,
                    ExecutionControlType.Interrupt,
                    ExecutionControlType.Cancel,
                    issues
                );
                ValidateRelativePriority(
                    seen,
                    ExecutionControlType.Cancel,
                    ExecutionControlType.Pause,
                    issues
                );
            }
            catch (Exception)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InternalValidationError,
                    "controlRequests"
                );
            }

            return Result(
                safeContext,
                issues,
                "Control priority validation completed."
            );
        }

        private static void ValidateVersion(
            ReactionExecutionPlan plan,
            ExecutionOrchestrationPolicyVersion policyVersion,
            ExecutionValidationIssues issues)
        {
            if (policyVersion == null ||
                !policyVersion.IsPolicyV01 ||
                plan.PolicyVersion == null ||
                !plan.PolicyVersion.IsPolicyV01)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .UnsupportedPolicyVersion,
                    "policyVersion"
                );
            }
        }

        private static void ValidateNormalBodyAction(
            ReactionExecutionPlan plan,
            ExecutionValidationIssues issues)
        {
            ReactionExecutionStep primary = FindRole(
                plan,
                ExecutionStepRole.PrimaryAction
            );
            ReactionExecutionStep ack = FindRole(
                plan,
                ExecutionStepRole.Acknowledgement
            );

            bool hasBodyStep = false;
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                ReactionExecutionStep step = plan.Steps[i];
                if (step != null &&
                    IsBodyDomain(step.Domain))
                {
                    hasBodyStep = true;
                    break;
                }
            }

            if (!hasBodyStep)
                return;

            if (primary == null ||
                !IsBodyDomain(primary.Domain))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingPrimaryBodyStep,
                    "steps"
                );
                return;
            }

            if (primary.Requiredness !=
                ExecutionRequiredness.Required)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .PrimaryBodyMustBeRequired,
                    "primary.requiredness"
                );
            }

            bool lookAround =
                ExecutionLookAroundContract.IsLookAroundStep(primary);
            if (lookAround &&
                !ExecutionLookAroundContract.MatchesPermissionPolicy(
                    primary.PermissionWaitPolicy))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidPermissionWaitPolicy,
                    "primary.permissionWaitPolicy"
                );
            }
            else if (!lookAround &&
                (primary.PermissionWaitPolicy == null ||
                 primary.PermissionWaitPolicy.Behavior !=
                    ExecutionPermissionWaitBehavior.WaitWithTimeout))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidPermissionWaitPolicy,
                    "primary.permissionWaitPolicy"
                );
            }
            else if (!lookAround &&
                primary.PermissionWaitPolicy.TimeoutSeconds <= 0d)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingPermissionTimeout,
                    "primary.permissionWaitPolicy.timeoutSeconds"
                );
            }

            ValidatePhysicalCompletion(primary, issues);

            if (ack == null)
                return;

            if (ack.Domain != ExecutionDomain.Speech ||
                ack.Requiredness != ExecutionRequiredness.Optional)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .AckMustBeOptional,
                    "ack.requiredness"
                );
            }

            if (!HasAcceptedOrLaterDependency(
                    plan,
                    primary.StepId,
                    ack.StepId))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .AckStartsBeforePrimaryAccepted,
                    "dependencies"
                );
            }

            if (plan.PlanCompletionPolicy != null &&
                plan.PlanCompletionPolicy.OptionalFailureBehavior ==
                    ExecutionOptionalFailureBehavior.FailPlan)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .OptionalAckFailureEscalatesPlan,
                    "planCompletionPolicy.optionalFailureBehavior"
                );
            }
        }

        private static void ValidateLookAroundPlan(
            ReactionExecutionPlan plan,
            ExecutionValidationIssues issues)
        {
            ReactionExecutionStep lookAround = null;
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                if (ExecutionLookAroundContract.IsLookAroundStep(
                        plan.Steps[i]))
                {
                    lookAround = plan.Steps[i];
                    break;
                }
            }

            if (lookAround == null)
                return;

            bool valid = plan.Steps.Count == 1 &&
                plan.Dependencies.Count == 0 &&
                plan.PlanType == ExecutionLookAroundContract.PlanType &&
                plan.PolicyKind == ExecutionLookAroundContract.PolicyKind &&
                ExecutionLookAroundContract.MatchesStep(lookAround) &&
                ExecutionLookAroundContract.MatchesPlanCompositionPolicy(
                    plan.PlanCompletionPolicy,
                    plan.FailurePolicy,
                    plan.CancellationPolicy) &&
                ExecutionLookAroundContract.MatchesSafetyPolicy(
                    plan.SafetyPolicy);

            if (!valid)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidLookAroundSemantics,
                    "plan"
                );
            }
        }

        private static void ValidateNoOpPlan(
            ReactionExecutionPlan plan,
            ExecutionValidationIssues issues)
        {
            bool valid = plan.PolicyKind == ExecutionPolicyKind.NoOp &&
                plan.Steps.Count == 1 &&
                plan.Dependencies.Count == 0 &&
                ExecutionNoOpContract.IsNoOpStep(plan.Steps[0]) &&
                plan.Steps[0].Role == ExecutionNoOpContract.StepRole &&
                plan.Steps[0].Requiredness ==
                    ExecutionNoOpContract.Requiredness &&
                plan.Steps[0].RequiredResources.Count == 0 &&
                ExecutionNoOpContract.MatchesCompletionPolicy(
                    plan.Steps[0].CompletionPolicy) &&
                ExecutionNoOpContract.MatchesPermissionPolicy(
                    plan.Steps[0].PermissionWaitPolicy) &&
                ExecutionNoOpContract.MatchesTimeoutPolicy(
                    plan.Steps[0].TimeoutPolicy) &&
                ExecutionNoOpContract.MatchesResourceWaitPolicy(
                    plan.Steps[0].ResourceWaitTimeoutPolicy) &&
                ExecutionNoOpContract.MatchesRetryPolicy(
                    plan.Steps[0].RetryPolicy) &&
                ExecutionNoOpContract.MatchesFailurePolicy(
                    plan.Steps[0].FailurePolicy) &&
                ExecutionNoOpContract.MatchesCancellationPolicy(
                    plan.Steps[0].CancellationPolicy) &&
                ExecutionNoOpContract.MatchesPlanCompositionPolicy(
                    plan.PlanCompletionPolicy,
                    plan.FailurePolicy,
                    plan.CancellationPolicy) &&
                ExecutionNoOpContract.MatchesSafetyPolicy(
                    plan.SafetyPolicy);

            if (!valid)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidNoOpSemantics,
                    "plan");
            }
        }

        private static void ValidatePhysicalCompletion(
            ReactionExecutionStep primary,
            ExecutionValidationIssues issues)
        {
            if (primary.Domain != ExecutionDomain.PhysicalBody ||
                primary.CompletionPolicy == null)
            {
                return;
            }

            ExecutionCompletionPolicy completion =
                primary.CompletionPolicy;

            if (completion.Kind ==
                    ExecutionCompletionKind.PhysicalDispatched &&
                completion.RequiresVerifiedCompletion)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .PhysicalCompletionFalselyVerified,
                    "primary.completionPolicy"
                );
            }

            bool explicitUnverified =
                completion.AllowsUnverifiedCompletion ||
                completion.Kind ==
                    ExecutionCompletionKind
                        .PhysicalCompletionUnverifiedAllowed;

            if (completion.Kind ==
                    ExecutionCompletionKind.PhysicalDispatched &&
                !explicitUnverified)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .PhysicalUnverifiedNotDeclared,
                    "primary.completionPolicy"
                );
            }
        }

        private static void ValidatePriorityControlPlan(
            ReactionExecutionPlan plan,
            ExecutionValidationIssues issues)
        {
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                ReactionExecutionStep step = plan.Steps[i];
                if (step == null)
                    continue;

                if (step.Role ==
                        ExecutionStepRole.Acknowledgement &&
                    step.Requiredness ==
                        ExecutionRequiredness.Required)
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .EmergencyRequiresAck,
                        "steps[" + i + "]"
                    );
                }

                if (step.PermissionWaitPolicy != null &&
                    step.PermissionWaitPolicy.Behavior ==
                        ExecutionPermissionWaitBehavior
                            .WaitWithTimeout)
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .EmergencyUsesNormalPermissionWait,
                        "steps[" + i +
                        "].permissionWaitPolicy"
                    );
                }

                for (
                    int resourceIndex = 0;
                    resourceIndex < step.RequiredResources.Count;
                    resourceIndex++)
                {
                    ExecutionResourceRequirement requirement =
                        step.RequiredResources[resourceIndex];
                    if (requirement != null &&
                        requirement.Requiredness ==
                            ExecutionRequiredness.Required)
                    {
                        issues.AddFailure(
                            ExecutionPlanValidationFailureCodes
                                .EmergencyUsesNormalResourceWait,
                            "steps[" + i +
                            "].requiredResources"
                        );
                    }
                }
            }

            for (int i = 0; i < plan.Dependencies.Count; i++)
            {
                ExecutionDependency dependency =
                    plan.Dependencies[i];
                if (dependency != null &&
                    dependency.GateType !=
                        ExecutionDependencyGate.AfterAccepted)
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .EmergencyUsesNormalDependencyWait,
                        "dependencies[" + i + "]"
                    );
                }
            }
        }

        private static void ValidateRetryRules(
            ReactionExecutionPlan plan,
            ExecutionValidationIssues issues)
        {
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                ReactionExecutionStep step = plan.Steps[i];
                if (step == null || step.RetryPolicy == null)
                    continue;

                ExecutionRetryPolicy retry = step.RetryPolicy;
                if (retry.MaxAttempts < 1 ||
                    (retry.MaxAttempts > 1 &&
                     !retry.RequiresNewAttemptAndRequestIds))
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .InvalidRetryPolicy,
                        "steps[" + i + "].retryPolicy"
                    );
                }

                for (
                    int failureIndex = 0;
                    failureIndex <
                        retry.RetryableFailureCodes.Count;
                    failureIndex++)
                {
                    if (IsForbiddenRetry(
                            retry.RetryableFailureCodes[
                                failureIndex]))
                    {
                        issues.AddFailure(
                            ExecutionPlanValidationFailureCodes
                                .RetryUsesForbiddenFailure,
                            "steps[" + i +
                            "].retryPolicy"
                        );
                    }
                }
            }
        }

        private static bool HasAcceptedOrLaterDependency(
            ReactionExecutionPlan plan,
            string primaryStepId,
            string ackStepId)
        {
            for (int i = 0; i < plan.Dependencies.Count; i++)
            {
                ExecutionDependency dependency =
                    plan.Dependencies[i];

                if (dependency == null ||
                    dependency.PredecessorStepId !=
                        primaryStepId ||
                    dependency.SuccessorStepId != ackStepId)
                {
                    continue;
                }

                switch (dependency.GateType)
                {
                    case ExecutionDependencyGate.AfterAccepted:
                    case ExecutionDependencyGate.AfterStarted:
                    case ExecutionDependencyGate
                        .AfterCompletionPolicy:
                    case ExecutionDependencyGate.AfterSuccess:
                    case ExecutionDependencyGate.AfterTerminal:
                    case ExecutionDependencyGate
                        .AfterSpecificOutcome:
                        return true;
                }
            }

            return false;
        }

        private static ReactionExecutionStep FindRole(
            ReactionExecutionPlan plan,
            ExecutionStepRole role)
        {
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                ReactionExecutionStep step = plan.Steps[i];
                if (step != null && step.Role == role)
                    return step;
            }

            return null;
        }

        private static bool IsBodyDomain(ExecutionDomain domain)
        {
            return domain == ExecutionDomain.Vrm ||
                domain == ExecutionDomain.VirtualBody ||
                domain == ExecutionDomain.PhysicalBody;
        }

        private static bool IsForbiddenRetry(string code)
        {
            return code == ExecutionFailureCodes.InvalidRequest ||
                code == ExecutionFailureCodes.SafetyRejected ||
                code == ExecutionFailureCodes.CapabilityUnavailable ||
                code == ExecutionFailureCodes.Emergency ||
                code == ExecutionFailureCodes.Cancellation ||
                code == ExecutionFailureCodes.PermissionDenied;
        }

        private static void ValidateRelativePriority(
            IDictionary<ExecutionControlType, int> seen,
            ExecutionControlType higher,
            ExecutionControlType lower,
            ExecutionValidationIssues issues)
        {
            int higherValue;
            int lowerValue;
            if (seen.TryGetValue(higher, out higherValue) &&
                seen.TryGetValue(lower, out lowerValue) &&
                higherValue <= lowerValue)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidControlPriority,
                    "controlRequests.priority"
                );
            }
        }

        private static ExecutionPlanValidationResult Result(
            ExecutionPlanValidationContext context,
            ExecutionValidationIssues issues,
            string message)
        {
            return new ExecutionPlanValidationResult(
                context.ValidationId,
                context.CheckedAtUtc,
                ValidatorVersion,
                issues.FailureCodes,
                issues.InvalidFields,
                issues.Warnings,
                message
            );
        }
    }
}
