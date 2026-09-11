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
    public static class ExecutionRuntimeSnapshotValidator
    {
        public const string ValidatorVersion =
            "execution-runtime-snapshot-3a.1";

        public static ExecutionPlanValidationResult Validate(
            ReactionExecutionPlan plan,
            ExecutionPlanRuntimeSnapshot planSnapshot,
            IEnumerable<ExecutionStepRuntimeSnapshot> stepSnapshots,
            IEnumerable<ExecutionAttemptSnapshot> attemptSnapshots,
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
                if (plan == null || planSnapshot == null ||
                    plan.PlanId != planSnapshot.PlanId)
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .RuntimeSnapshotIdMismatch,
                        "planSnapshot.planId"
                    );
                }

                ValidateLifecycle(
                    planSnapshot != null &&
                        planSnapshot.Lifecycle ==
                            ExecutionPlanLifecycle.Terminal,
                    planSnapshot != null
                        ? planSnapshot.FinalOutcome
                        : ExecutionTerminalOutcome.None,
                    "planSnapshot",
                    issues
                );

                Dictionary<string, ReactionExecutionStep> steps =
                    new Dictionary<
                        string,
                        ReactionExecutionStep>(
                        StringComparer.Ordinal
                    );

                if (plan != null)
                {
                    for (int i = 0; i < plan.Steps.Count; i++)
                    {
                        ReactionExecutionStep step = plan.Steps[i];
                        if (step != null &&
                            !steps.ContainsKey(step.StepId))
                        {
                            steps.Add(step.StepId, step);
                        }
                    }
                }

                if (planSnapshot != null)
                {
                    ValidatePlanStepIds(
                        planSnapshot,
                        steps,
                        issues
                    );
                }

                Dictionary<string, ExecutionStepRuntimeSnapshot>
                    runtimeStepById =
                        new Dictionary<
                            string,
                            ExecutionStepRuntimeSnapshot>(
                            StringComparer.Ordinal
                        );

                if (stepSnapshots != null)
                {
                    foreach (
                        ExecutionStepRuntimeSnapshot snapshot in
                        stepSnapshots)
                    {
                        if (snapshot == null ||
                            plan == null ||
                            snapshot.PlanId != plan.PlanId ||
                            !steps.ContainsKey(snapshot.StepId))
                        {
                            issues.AddFailure(
                                ExecutionPlanValidationFailureCodes
                                    .RuntimeSnapshotIdMismatch,
                                "stepSnapshots"
                            );
                            continue;
                        }

                        ValidateLifecycle(
                            snapshot.Lifecycle ==
                                ExecutionStepLifecycle.Terminal,
                            snapshot.TerminalOutcome,
                            "stepSnapshots." + snapshot.StepId,
                            issues
                        );

                        if (!runtimeStepById.ContainsKey(
                                snapshot.StepId))
                        {
                            runtimeStepById.Add(
                                snapshot.StepId,
                                snapshot
                            );
                        }

                        if (!string.IsNullOrEmpty(
                                snapshot.CurrentAttemptId) &&
                            !Contains(
                                snapshot.AttemptIds,
                                snapshot.CurrentAttemptId))
                        {
                            issues.AddFailure(
                                ExecutionPlanValidationFailureCodes
                                    .RuntimeSnapshotIdMismatch,
                                "stepSnapshots." +
                                snapshot.StepId +
                                ".currentAttemptId"
                            );
                        }
                    }
                }

                if (attemptSnapshots != null)
                {
                    HashSet<string> attemptIds =
                        new HashSet<string>(StringComparer.Ordinal);
                    HashSet<string> requestIds =
                        new HashSet<string>(StringComparer.Ordinal);

                    foreach (
                        ExecutionAttemptSnapshot snapshot in
                        attemptSnapshots)
                    {
                        ReactionExecutionStep step;
                        if (snapshot == null ||
                            plan == null ||
                            snapshot.PlanId != plan.PlanId ||
                            !steps.TryGetValue(
                                snapshot.StepId,
                                out step) ||
                            string.IsNullOrWhiteSpace(
                                snapshot.ExecutionAttemptId) ||
                            string.IsNullOrWhiteSpace(
                                snapshot.RequestId) ||
                            snapshot.AttemptNumber < 1 ||
                            !attemptIds.Add(
                                snapshot.ExecutionAttemptId) ||
                            !requestIds.Add(snapshot.RequestId) ||
                            !AttemptIdentityMatches(
                                step,
                                snapshot))
                        {
                            issues.AddFailure(
                                ExecutionPlanValidationFailureCodes
                                    .RuntimeSnapshotIdMismatch,
                                "attemptSnapshots"
                            );
                            continue;
                        }

                        ValidateLifecycle(
                            snapshot.Lifecycle ==
                                ExecutionAttemptLifecycle.Terminal,
                            snapshot.TerminalOutcome,
                            "attemptSnapshots." +
                            snapshot.ExecutionAttemptId,
                            issues
                        );

                        ExecutionStepRuntimeSnapshot stepSnapshot;
                        if (runtimeStepById.TryGetValue(
                                snapshot.StepId,
                                out stepSnapshot) &&
                            !Contains(
                                stepSnapshot.AttemptIds,
                                snapshot.ExecutionAttemptId))
                        {
                            issues.AddFailure(
                                ExecutionPlanValidationFailureCodes
                                    .RuntimeSnapshotIdMismatch,
                                "attemptSnapshots." +
                                snapshot.ExecutionAttemptId
                            );
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InternalValidationError,
                    "runtimeSnapshot"
                );
                issues.AddWarning(
                    exception.GetType().Name + ": " +
                    exception.Message
                );
            }

            return new ExecutionPlanValidationResult(
                safeContext.ValidationId,
                safeContext.CheckedAtUtc,
                ValidatorVersion,
                issues.FailureCodes,
                issues.InvalidFields,
                issues.Warnings,
                "Runtime snapshot validation completed."
            );
        }

        private static void ValidateLifecycle(
            bool terminal,
            ExecutionTerminalOutcome outcome,
            string field,
            ExecutionValidationIssues issues)
        {
            if ((terminal &&
                 outcome == ExecutionTerminalOutcome.None) ||
                (!terminal &&
                 outcome != ExecutionTerminalOutcome.None))
            {
                issues.AddFailure(
                    ExecutionPlanValidationFailureCodes
                        .InvalidLifecycleOutcomeCombination,
                    field
                );
            }
        }

        private static void ValidatePlanStepIds(
            ExecutionPlanRuntimeSnapshot snapshot,
            IDictionary<string, ReactionExecutionStep> knownSteps,
            ExecutionValidationIssues issues)
        {
            HashSet<string> observed =
                new HashSet<string>(StringComparer.Ordinal);
            ValidateStepIdList(
                snapshot.ActiveStepIds,
                "planSnapshot.activeStepIds",
                knownSteps,
                observed,
                issues
            );
            ValidateStepIdList(
                snapshot.ReadyStepIds,
                "planSnapshot.readyStepIds",
                knownSteps,
                observed,
                issues
            );
            ValidateStepIdList(
                snapshot.WaitingStepIds,
                "planSnapshot.waitingStepIds",
                knownSteps,
                observed,
                issues
            );
            ValidateStepIdList(
                snapshot.TerminalStepIds,
                "planSnapshot.terminalStepIds",
                knownSteps,
                observed,
                issues
            );
        }

        private static void ValidateStepIdList(
            IReadOnlyList<string> stepIds,
            string field,
            IDictionary<string, ReactionExecutionStep> knownSteps,
            ISet<string> observed,
            ExecutionValidationIssues issues)
        {
            for (int i = 0; i < stepIds.Count; i++)
            {
                string stepId = stepIds[i];
                if (!knownSteps.ContainsKey(stepId) ||
                    !observed.Add(stepId))
                {
                    issues.AddFailure(
                        ExecutionPlanValidationFailureCodes
                            .RuntimeSnapshotIdMismatch,
                        field + "[" + i + "]"
                    );
                }
            }
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string expected)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == expected)
                    return true;
            }

            return false;
        }

        private static bool AttemptIdentityMatches(
            ReactionExecutionStep step,
            ExecutionAttemptSnapshot snapshot)
        {
            if (step == null || step.RetryPolicy == null)
                return false;

            if (snapshot.AttemptNumber >
                step.RetryPolicy.MaxAttempts)
            {
                return false;
            }

            if (snapshot.AttemptNumber == 1)
                return snapshot.RequestId == step.RequestId;

            return step.RetryPolicy
                    .RequiresNewAttemptAndRequestIds &&
                snapshot.RequestId != step.RequestId;
        }
    }
}
