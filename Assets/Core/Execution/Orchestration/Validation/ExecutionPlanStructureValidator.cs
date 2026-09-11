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
    /// Side-effect-free structural validator. It never repairs, reorders,
    /// augments, authorizes, or executes the supplied plan.
    /// </summary>
    public static class ExecutionPlanStructureValidator
    {
        public const string ValidatorVersion =
            "execution-plan-structure-3a.1";

        public static ExecutionPlanValidationResult Validate(
            ReactionExecutionPlan plan,
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
                ValidatePlan(plan, issues);

                return Result(
                    safeContext,
                    issues,
                    "Plan structure validation completed."
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

        private static void ValidatePlan(
            ReactionExecutionPlan plan,
            ExecutionValidationIssues issues)
        {
            if (plan == null)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes.NullPlan,
                    "plan"
                );
                return;
            }

            Required(
                plan.PlanId,
                ExecutionPlanValidationFailureCodes.MissingPlanId,
                "planId",
                issues
            );
            ValidateProvenance(plan, issues);

            if (plan.PolicyVersion == null ||
                string.IsNullOrWhiteSpace(
                    plan.PolicyVersion.PolicyId))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingPolicyVersion,
                    "policyVersion"
                );
            }

            if (plan.PlanGeneration <= 0)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidPlanGeneration,
                    "planGeneration"
                );
            }

            if (plan.PlanType == ExecutionPlanType.None ||
                !Enum.IsDefined(
                    typeof(ExecutionPlanType),
                    plan.PlanType))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidPlanType,
                    "planType"
                );
            }

            if (plan.PlanCompletionPolicy == null)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingPlanPolicy,
                    "planCompletionPolicy"
                );
            }

            if (plan.FailurePolicy == null)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingFailurePolicy,
                    "failurePolicy"
                );
            }

            if (plan.CancellationPolicy == null)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingCancellationPolicy,
                    "cancellationPolicy"
                );
            }

            if (plan.Steps.Count == 0)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .EmptyStepCollection,
                    "steps"
                );
                return;
            }

            Dictionary<string, ReactionExecutionStep> stepById =
                new Dictionary<string, ReactionExecutionStep>(
                    StringComparer.Ordinal
                );
            HashSet<string> requestIds =
                new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                ReactionExecutionStep step = plan.Steps[i];
                string prefix = "steps[" + i + "]";

                if (step == null)
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes.NullStep,
                        prefix
                    );
                    continue;
                }

                ValidateStep(plan, step, prefix, issues);

                if (!string.IsNullOrWhiteSpace(step.StepId))
                {
                    if (stepById.ContainsKey(step.StepId))
                    {
                        issues.AddFailure(
                            ExecutionPlanValidationFailureCodes
                                .DuplicateStepId,
                            prefix + ".stepId"
                        );
                    }
                    else
                    {
                        stepById.Add(step.StepId, step);
                    }
                }

                if (!string.IsNullOrWhiteSpace(step.RequestId) &&
                    !requestIds.Add(step.RequestId))
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .DuplicateRequestId,
                        prefix + ".requestId"
                    );
                }
            }

            ValidateDependencies(plan, stepById, issues);
            ValidateFallbackTargets(plan, stepById, issues);
        }

        private static void ValidateProvenance(
            ReactionExecutionPlan plan,
            ExecutionValidationIssues issues)
        {
            // Directly constructed pre-source-neutral plans retain the legacy
            // four-ID structure contract. Production Submission always emits
            // an explicit SourceKind and follows the source-specific rules.
            if (plan.SourceKind == ExecutionSubmissionSourceKind.Unknown)
            {
                Required(plan.InteractionId,
                    ExecutionPlanValidationFailureCodes.MissingInteractionId,
                    "interactionId", issues);
                Required(plan.InputId,
                    ExecutionPlanValidationFailureCodes.MissingInputId,
                    "inputId", issues);
                Required(plan.AnalysisId,
                    ExecutionPlanValidationFailureCodes.MissingAnalysisId,
                    "analysisId", issues);
                Required(plan.ReactionId,
                    ExecutionPlanValidationFailureCodes.MissingReactionId,
                    "reactionId", issues);
                return;
            }

            var provenance = new ExecutionRequestProvenance(
                plan.SourceKind,
                plan.SourceEventId,
                plan.InteractionId,
                plan.InputId,
                plan.AnalysisId,
                plan.ReactionId);
            ExecutionRequestProvenanceValidationResult validation =
                ExecutionRequestProvenanceValidator.Validate(provenance);
            if (!validation.IsValid)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidSubmissionProvenance,
                    validation.FailureCode);
            }
        }

        private static void ValidateStep(
            ReactionExecutionPlan plan,
            ReactionExecutionStep step,
            string prefix,
            ExecutionValidationIssues issues)
        {
            Required(
                step.StepId,
                ExecutionPlanValidationFailureCodes.MissingStepId,
                prefix + ".stepId",
                issues
            );
            Required(
                step.RequestId,
                ExecutionPlanValidationFailureCodes.MissingRequestId,
                prefix + ".requestId",
                issues
            );

            if (!Same(step.PlanId, plan.PlanId))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .StepPlanIdMismatch,
                    prefix + ".planId"
                );
            }

            if (step.StepType == ExecutionStepType.None ||
                !Enum.IsDefined(
                    typeof(ExecutionStepType),
                    step.StepType))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidStepType,
                    prefix + ".stepType"
                );
            }

            if (step.Domain == ExecutionDomain.None ||
                !Enum.IsDefined(
                    typeof(ExecutionDomain),
                    step.Domain))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes.InvalidDomain,
                    prefix + ".domain"
                );
            }

            if (step.RequestReference == null)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingRequestReference,
                    prefix + ".requestReference"
                );
            }
            else if (string.IsNullOrWhiteSpace(
                         step.RequestReference.RequestTypeId))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingRequestReference,
                    prefix + ".requestReference.requestTypeId"
                );
            }
            else if (!Same(
                         step.RequestReference.RequestId,
                         step.RequestId))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .RequestReferenceIdMismatch,
                    prefix + ".requestReference.requestId"
                );
            }

            ValidateCompletion(step, prefix, issues);
            ValidatePermission(step, prefix, issues);
            ValidateTimeout(step, prefix, issues);
            ValidateResourceWaitTimeout(step, prefix, issues);
            ValidateRetry(step, prefix, issues);
            ValidateResources(step, prefix, issues);
            ValidateNoOp(step, prefix, issues);
            ValidateLookAround(step, prefix, issues);

            if (step.FailurePolicy == null)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingFailurePolicy,
                    prefix + ".failurePolicy"
                );
            }

            if (step.CancellationPolicy == null)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingCancellationPolicy,
                    prefix + ".cancellationPolicy"
                );
            }
        }

        private static void ValidateCompletion(
            ReactionExecutionStep step,
            string prefix,
            ExecutionValidationIssues issues)
        {
            ExecutionCompletionPolicy policy =
                step.CompletionPolicy;

            if (policy == null ||
                policy.Kind == ExecutionCompletionKind.None ||
                policy.RequiredDomainStage == null ||
                string.IsNullOrWhiteSpace(
                    policy.RequiredDomainStage.StageId))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidCompletionPolicy,
                    prefix + ".completionPolicy"
                );
                return;
            }

            if (policy.RequiresVerifiedCompletion &&
                policy.AllowsUnverifiedCompletion)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidCompletionPolicy,
                    prefix + ".completionPolicy"
                );
            }

            if (!CompletionStageMatches(
                    policy.Kind,
                    policy.RequiredDomainStage.StageId))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .DomainCompletionMismatch,
                    prefix +
                    ".completionPolicy.requiredDomainStage"
                );
            }

            if (policy.Kind ==
                    ExecutionCompletionKind
                        .PhysicalCompletionUnverifiedAllowed &&
                (policy.RequiresVerifiedCompletion ||
                 !policy.AllowsUnverifiedCompletion))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidCompletionPolicy,
                    prefix + ".completionPolicy"
                );
            }

            if (policy.Kind ==
                    ExecutionCompletionKind
                        .PhysicalCompletionVerified &&
                !policy.RequiresVerifiedCompletion)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidCompletionPolicy,
                    prefix + ".completionPolicy"
                );
            }

            if (policy.Kind ==
                    ExecutionCompletionKind
                        .GoalConditionSatisfied &&
                !policy.RequiresGoalCondition)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidCompletionPolicy,
                    prefix + ".completionPolicy"
                );
            }

            if (IsPhysicalCompletion(policy.Kind) &&
                step.Domain != ExecutionDomain.PhysicalBody)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .DomainCompletionMismatch,
                    prefix + ".completionPolicy"
                );
            }

            if (policy.Kind ==
                    ExecutionCompletionKind.PlaybackCompleted &&
                step.Domain != ExecutionDomain.Speech)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .DomainCompletionMismatch,
                    prefix + ".completionPolicy"
                );
            }

            if (policy.Kind ==
                    ExecutionCompletionKind.VrmTargetApplied &&
                step.Domain != ExecutionDomain.Vrm)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .DomainCompletionMismatch,
                    prefix + ".completionPolicy"
                );
            }

            if (policy.Kind ==
                    ExecutionCompletionKind.VirtualBodyResolved &&
                step.Domain != ExecutionDomain.VirtualBody)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .DomainCompletionMismatch,
                    prefix + ".completionPolicy"
                );
            }
        }

        private static void ValidatePermission(
            ReactionExecutionStep step,
            string prefix,
            ExecutionValidationIssues issues)
        {
            if (step.PermissionWaitPolicy == null)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidPermissionWaitPolicy,
                    prefix + ".permissionWaitPolicy"
                );
                return;
            }

            if (step.PermissionWaitPolicy.Behavior ==
                    ExecutionPermissionWaitBehavior.WaitWithTimeout &&
                step.PermissionWaitPolicy.TimeoutSeconds <= 0d)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingPermissionTimeout,
                    prefix +
                    ".permissionWaitPolicy.timeoutSeconds"
                );
            }
        }

        private static void ValidateTimeout(
            ReactionExecutionStep step,
            string prefix,
            ExecutionValidationIssues issues)
        {
            bool noOp = ExecutionNoOpContract.IsNoOpStep(step);
            bool lookAround =
                ExecutionLookAroundContract.IsLookAroundStep(step);
            if (step.TimeoutPolicy == null ||
                (!noOp && !lookAround &&
                 step.TimeoutPolicy.TimeoutSeconds <= 0d) ||
                (noOp && step.TimeoutPolicy.TimeoutSeconds != 0d) ||
                (lookAround &&
                 !ExecutionLookAroundContract.MatchesTimeoutPolicy(
                     step.TimeoutPolicy)) ||
                double.IsNaN(step.TimeoutPolicy != null
                    ? step.TimeoutPolicy.TimeoutSeconds
                    : double.NaN) ||
                double.IsInfinity(step.TimeoutPolicy != null
                    ? step.TimeoutPolicy.TimeoutSeconds
                    : double.NaN))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .MissingTimeoutPolicy,
                    prefix + ".timeoutPolicy"
                );
            }
        }

        private static void ValidateResourceWaitTimeout(
            ReactionExecutionStep step,
            string prefix,
            ExecutionValidationIssues issues)
        {
            ExecutionResourceWaitTimeoutPolicy policy =
                step.ResourceWaitTimeoutPolicy;
            if (policy == null ||
                !Enum.IsDefined(
                    typeof(ExecutionTimeoutAction),
                    policy != null
                        ? policy.Action
                        : (ExecutionTimeoutAction)(-1)) ||
                (policy.TimeoutEnabled && policy.TimeoutSeconds <= 0d) ||
                (policy.TimeoutEnabled &&
                 policy.Action != ExecutionTimeoutAction.Fail) ||
                (policy != null &&
                 (double.IsNaN(policy.TimeoutSeconds) ||
                  double.IsInfinity(policy.TimeoutSeconds))) ||
                (!policy.TimeoutEnabled && policy.TimeoutSeconds != 0d))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidResourceWaitTimeoutPolicy,
                    prefix + ".resourceWaitTimeoutPolicy"
                );
            }
        }

        private static void ValidateRetry(
            ReactionExecutionStep step,
            string prefix,
            ExecutionValidationIssues issues)
        {
            ExecutionRetryPolicy policy = step.RetryPolicy;
            if (policy == null ||
                policy.MaxAttempts < 1 ||
                (policy.MaxAttempts > 1 &&
                 !policy.RequiresNewAttemptAndRequestIds))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidRetryPolicy,
                    prefix + ".retryPolicy"
                );
                return;
            }

            for (
                int i = 0;
                i < policy.RetryableFailureCodes.Count;
                i++)
            {
                if (IsForbiddenRetryFailure(
                        policy.RetryableFailureCodes[i]))
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .RetryUsesForbiddenFailure,
                        prefix +
                        ".retryPolicy.retryableFailureCodes"
                    );
                }
            }
        }

        private static void ValidateResources(
            ReactionExecutionStep step,
            string prefix,
            ExecutionValidationIssues issues)
        {
            Dictionary<string, ExecutionResourceAccessMode> seen =
                new Dictionary<
                    string,
                    ExecutionResourceAccessMode>(
                    StringComparer.Ordinal
                );

            for (
                int i = 0;
                i < step.RequiredResources.Count;
                i++)
            {
                ExecutionResourceRequirement requirement =
                    step.RequiredResources[i];
                string field =
                    prefix + ".requiredResources[" + i + "]";

                if (requirement == null ||
                    string.IsNullOrWhiteSpace(
                        requirement.ResourceId))
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .MissingResourceId,
                        field + ".resourceId"
                    );
                    continue;
                }

                ExecutionResourceAccessMode previous;
                if (seen.TryGetValue(
                        requirement.ResourceId,
                        out previous))
                {
                    issues.AddFailure(
                        previous == requirement.AccessMode
                            ? ExecutionPlanValidationFailureCodes
                                .DuplicateResourceRequirement
                            : ExecutionPlanValidationFailureCodes
                                .ConflictingResourceAccess,
                        field
                    );
                }
                else
                {
                    seen.Add(
                        requirement.ResourceId,
                        requirement.AccessMode
                    );
                }
            }
        }

        private static void ValidateNoOp(
            ReactionExecutionStep step,
            string prefix,
            ExecutionValidationIssues issues)
        {
            bool noOpDomain = step.Domain == ExecutionDomain.NoOp;
            bool noOpType = step.StepType == ExecutionStepType.NoOp;
            bool noOpCompletion = step.CompletionPolicy != null &&
                step.CompletionPolicy.Kind ==
                    ExecutionCompletionKind.LogicalNoOpCompleted;

            if (!noOpDomain && !noOpType && !noOpCompletion)
                return;

            if (!noOpDomain || !noOpType ||
                step.Role != ExecutionNoOpContract.StepRole ||
                step.Requiredness != ExecutionNoOpContract.Requiredness ||
                step.RequiredResources.Count != 0 ||
                !ExecutionNoOpContract.MatchesCompletionPolicy(
                    step.CompletionPolicy) ||
                !ExecutionNoOpContract.MatchesPermissionPolicy(
                    step.PermissionWaitPolicy) ||
                !ExecutionNoOpContract.MatchesTimeoutPolicy(
                    step.TimeoutPolicy) ||
                !ExecutionNoOpContract.MatchesResourceWaitPolicy(
                    step.ResourceWaitTimeoutPolicy) ||
                !ExecutionNoOpContract.MatchesRetryPolicy(
                    step.RetryPolicy) ||
                !ExecutionNoOpContract.MatchesFailurePolicy(
                    step.FailurePolicy) ||
                !ExecutionNoOpContract.MatchesCancellationPolicy(
                    step.CancellationPolicy))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidNoOpSemantics,
                    prefix);
            }
        }

        private static void ValidateLookAround(
            ReactionExecutionStep step,
            string prefix,
            ExecutionValidationIssues issues)
        {
            if (!ExecutionLookAroundContract.IsLookAroundStep(step))
                return;

            if (!ExecutionLookAroundContract.MatchesStep(step))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidLookAroundSemantics,
                    prefix);
            }
        }

        private static void ValidateDependencies(
            ReactionExecutionPlan plan,
            IDictionary<string, ReactionExecutionStep> stepById,
            ExecutionValidationIssues issues)
        {
            HashSet<string> dependencyIds =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> semanticEdges =
                new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, List<string>> graph =
                CreateGraph(stepById.Keys);
            Dictionary<string, List<string>> fallbackGraph =
                CreateGraph(stepById.Keys);
            List<string[]> fallbackEdges = new List<string[]>();

            for (int i = 0; i < plan.Dependencies.Count; i++)
            {
                ExecutionDependency dependency =
                    plan.Dependencies[i];
                string prefix = "dependencies[" + i + "]";

                if (dependency == null)
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .MissingDependencyTarget,
                        prefix
                    );
                    continue;
                }

                Required(
                    dependency.DependencyId,
                    ExecutionPlanValidationFailureCodes
                        .MissingDependencyId,
                    prefix + ".dependencyId",
                    issues
                );

                if (!Same(dependency.PlanId, plan.PlanId))
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .DependencyPlanIdMismatch,
                        prefix + ".planId"
                    );
                }

                bool sourceExists = stepById.ContainsKey(
                    dependency.PredecessorStepId
                );
                bool targetExists = stepById.ContainsKey(
                    dependency.SuccessorStepId
                );

                if (!sourceExists || !targetExists)
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .MissingDependencyTarget,
                        prefix
                    );
                }

                if (Same(
                        dependency.PredecessorStepId,
                        dependency.SuccessorStepId))
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .SelfDependency,
                        prefix
                    );
                }

                string edgeKey =
                    dependency.PredecessorStepId + "\u001f" +
                    dependency.SuccessorStepId + "\u001f" +
                    ((int)dependency.GateType).ToString() + "\u001f" +
                    ((int)dependency.RequiredOutcome).ToString();

                if ((!string.IsNullOrWhiteSpace(
                         dependency.DependencyId) &&
                     !dependencyIds.Add(
                         dependency.DependencyId)) ||
                    !semanticEdges.Add(edgeKey))
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .DuplicateDependency,
                        prefix
                    );
                }

                if (sourceExists &&
                    targetExists &&
                    !Same(
                        dependency.PredecessorStepId,
                        dependency.SuccessorStepId))
                {
                    graph[dependency.PredecessorStepId].Add(
                        dependency.SuccessorStepId
                    );

                    if (dependency.GateType ==
                        ExecutionDependencyGate.Fallback)
                    {
                        fallbackGraph[
                            dependency.PredecessorStepId
                        ].Add(dependency.SuccessorStepId);
                        fallbackEdges.Add(
                            new[]
                            {
                                dependency.PredecessorStepId,
                                dependency.SuccessorStepId
                            }
                        );
                    }
                }
            }

            if (HasCycle(graph))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .DependencyCycle,
                    "dependencies"
                );
            }

            if (HasCycle(fallbackGraph) ||
                HasCycleContainingFallback(graph, fallbackEdges))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .FallbackCycle,
                    "dependencies"
                );
            }
        }

        private static void ValidateFallbackTargets(
            ReactionExecutionPlan plan,
            IDictionary<string, ReactionExecutionStep> stepById,
            ExecutionValidationIssues issues)
        {
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                ReactionExecutionStep step = plan.Steps[i];
                if (step == null || step.FailurePolicy == null)
                    continue;

                if (step.FailurePolicy.Action ==
                        ExecutionFailureAction.UseFallback &&
                    !stepById.ContainsKey(
                        step.FailurePolicy.FallbackStepId))
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .MissingDependencyTarget,
                        "steps[" + i +
                        "].failurePolicy.fallbackStepId"
                    );
                }
            }
        }

        private static Dictionary<string, List<string>> CreateGraph(
            IEnumerable<string> stepIds)
        {
            Dictionary<string, List<string>> graph =
                new Dictionary<string, List<string>>(
                    StringComparer.Ordinal
                );

            foreach (string stepId in stepIds)
                graph.Add(stepId, new List<string>());

            return graph;
        }

        private static bool HasCycle(
            IDictionary<string, List<string>> graph)
        {
            List<string> nodes = new List<string>(graph.Keys);
            nodes.Sort(StringComparer.Ordinal);
            Dictionary<string, int> colors =
                new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < nodes.Count; i++)
                colors[nodes[i]] = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (colors[nodes[i]] == 0 &&
                    Visit(nodes[i], graph, colors))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCycleContainingFallback(
            IDictionary<string, List<string>> graph,
            IEnumerable<string[]> fallbackEdges)
        {
            foreach (string[] edge in fallbackEdges)
            {
                if (edge != null &&
                    edge.Length == 2 &&
                    CanReach(edge[1], edge[0], graph))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanReach(
            string start,
            string target,
            IDictionary<string, List<string>> graph)
        {
            Stack<string> pending = new Stack<string>();
            HashSet<string> visited =
                new HashSet<string>(StringComparer.Ordinal);
            pending.Push(start);

            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (!visited.Add(current))
                    continue;

                if (current == target)
                    return true;

                List<string> next = new List<string>(graph[current]);
                next.Sort(StringComparer.Ordinal);
                for (int i = next.Count - 1; i >= 0; i--)
                    pending.Push(next[i]);
            }

            return false;
        }

        private static bool Visit(
            string node,
            IDictionary<string, List<string>> graph,
            IDictionary<string, int> colors)
        {
            colors[node] = 1;
            List<string> targets = new List<string>(graph[node]);
            targets.Sort(StringComparer.Ordinal);

            for (int i = 0; i < targets.Count; i++)
            {
                string target = targets[i];
                if (colors[target] == 1)
                    return true;

                if (colors[target] == 0 &&
                    Visit(target, graph, colors))
                {
                    return true;
                }
            }

            colors[node] = 2;
            return false;
        }

        private static bool IsPhysicalCompletion(
            ExecutionCompletionKind kind)
        {
            return kind ==
                    ExecutionCompletionKind.PhysicalDispatched ||
                kind ==
                    ExecutionCompletionKind
                        .PhysicalCompletionVerified ||
                kind ==
                    ExecutionCompletionKind
                        .PhysicalCompletionUnverifiedAllowed;
        }

        private static bool CompletionStageMatches(
            ExecutionCompletionKind kind,
            string stageId)
        {
            switch (kind)
            {
                case ExecutionCompletionKind.RequestAccepted:
                    return stageId ==
                        ExecutionDomainStageIds.RequestAccepted;
                case ExecutionCompletionKind.ExecutionStarted:
                    return stageId ==
                        ExecutionDomainStageIds.ExecutionStarted;
                case ExecutionCompletionKind.PlaybackCompleted:
                    return stageId ==
                        ExecutionDomainStageIds.PlaybackCompleted;
                case ExecutionCompletionKind.VrmTargetApplied:
                    return stageId ==
                        ExecutionDomainStageIds.VrmTargetApplied;
                case ExecutionCompletionKind.VirtualBodyResolved:
                    return stageId ==
                        ExecutionDomainStageIds.VirtualBodyResolved;
                case ExecutionCompletionKind.PhysicalDispatched:
                case ExecutionCompletionKind
                    .PhysicalCompletionUnverifiedAllowed:
                    return stageId ==
                        ExecutionDomainStageIds.PhysicalDispatched;
                case ExecutionCompletionKind
                    .PhysicalCompletionVerified:
                    return stageId ==
                        ExecutionDomainStageIds
                            .PhysicalCompletionVerified;
                case ExecutionCompletionKind
                    .GoalConditionSatisfied:
                    return stageId ==
                        ExecutionDomainStageIds
                            .GoalConditionSatisfied;
                case ExecutionCompletionKind
                    .ContactFeedbackReceived:
                    return stageId ==
                        ExecutionDomainStageIds
                            .ContactFeedbackReceived;
                case ExecutionCompletionKind.LogicalNoOpCompleted:
                    return stageId ==
                        ExecutionDomainStageIds.LogicalNoOpCompleted;
                default:
                    return false;
            }
        }

        private static bool IsForbiddenRetryFailure(string code)
        {
            return code == ExecutionFailureCodes.InvalidRequest ||
                code == ExecutionFailureCodes.SafetyRejected ||
                code == ExecutionFailureCodes.CapabilityUnavailable ||
                code == ExecutionFailureCodes.Emergency ||
                code == ExecutionFailureCodes.Cancellation ||
                code == ExecutionFailureCodes.PermissionDenied;
        }

        private static void Required(
            string value,
            string code,
            string field,
            ExecutionValidationIssues issues)
        {
            if (string.IsNullOrWhiteSpace(value))
                issues.AddFailure(code, field);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(
                left ?? string.Empty,
                right ?? string.Empty,
                StringComparison.Ordinal
            );
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
