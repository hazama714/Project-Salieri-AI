// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using System.Collections.Generic;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Adapters
{
    public static class ExecutionRuntimeSnapshotBridge
    {
        public const string BridgeVersion =
            "execution-runtime-snapshot-bridge-3c1.1";

        public static ExecutionCoordinatorRuntimeSnapshot Create(
            ExecutionCoordinatorRuntimeState state)
        {
            if (state == null) return null;
            List<ExecutionCoordinatorStepSnapshot> steps =
                new List<ExecutionCoordinatorStepSnapshot>();
            for (int i = 0; i < state.StepStates.Count; i++)
            {
                ExecutionCoordinatorStepState step = state.StepStates[i];
                if (step == null)
                {
                    steps.Add(null);
                    continue;
                }
                steps.Add(new ExecutionCoordinatorStepSnapshot(
                    step.StepId, step.Lifecycle,
                    step.DomainStage != null
                        ? step.DomainStage.StageId : string.Empty,
                    step.CurrentAttemptId,
                    step.CompletionPolicySatisfied,
                    step.DependencySatisfied,
                    step.TerminalOutcome,
                    step.FailureCodes));
            }

            List<ExecutionCoordinatorResourceSnapshot> resources =
                new List<ExecutionCoordinatorResourceSnapshot>();
            List<ExecutionCoordinatorAttemptSnapshot> attempts =
                new List<ExecutionCoordinatorAttemptSnapshot>();
            for (int i = 0; i < state.AttemptStates.Count; i++)
            {
                ExecutionCoordinatorAttemptState attempt =
                    state.AttemptStates[i];
                if (attempt == null)
                {
                    attempts.Add(null);
                    continue;
                }
                attempts.Add(new ExecutionCoordinatorAttemptSnapshot(
                    attempt.ExecutionAttemptId, attempt.StepId,
                    attempt.RequestId, attempt.AttemptNumber,
                    attempt.Lifecycle,
                    attempt.DomainStage != null
                        ? attempt.DomainStage.StageId : string.Empty,
                    attempt.TerminalOutcome));
            }
            for (int i = 0;
                i < state.ResourceLeaseReferences.Count; i++)
            {
                ResourceLeaseReference lease =
                    state.ResourceLeaseReferences[i];
                if (lease == null)
                {
                    resources.Add(null);
                    continue;
                }
                resources.Add(new ExecutionCoordinatorResourceSnapshot(
                    lease.ResourceId, lease.LeaseId, lease.PlanId,
                    lease.Status,
                    lease.AccessMode, lease.StepId));
            }

            List<ExecutionCoordinatorControlSnapshot> controls =
                new List<ExecutionCoordinatorControlSnapshot>();
            for (int i = 0;
                i < state.PendingControlRequests.Count; i++)
            {
                ExecutionCoordinatorControlState control =
                    state.PendingControlRequests[i];
                if (control == null)
                {
                    controls.Add(null);
                    continue;
                }
                controls.Add(new ExecutionCoordinatorControlSnapshot(
                    control.ControlType, control.PriorityRank,
                    control.ControlRequestId,
                    control.Dispatched,
                    control.EffectConfirmed));
            }

            return new ExecutionCoordinatorRuntimeSnapshot(
                state.PlanId, state.PlanLifecycle, state.FinalOutcome,
                state.Generation, state.StartedAtUtc,
                state.LastUpdatedAtUtc, steps, attempts, resources, controls,
                ProjectRepresentativeState(state));
        }

        public static CoordinatorRepresentativeState
            ProjectRepresentativeState(
                ExecutionCoordinatorRuntimeState state)
        {
            if (state == null)
                return CoordinatorRepresentativeState.Idle;
            if (state.PlanLifecycle ==
                ExecutionCoordinatorPlanLifecycle.SafetyPreempting)
                return CoordinatorRepresentativeState.SafetyPreempting;
            if (state.PlanLifecycle ==
                ExecutionCoordinatorPlanLifecycle.Terminal)
                return CoordinatorRepresentativeState.Terminal;
            if (state.PlanLifecycle ==
                ExecutionCoordinatorPlanLifecycle.Cancelling)
                return CoordinatorRepresentativeState.Interrupted;

            bool permission = false;
            bool resource = false;
            bool speaking = false;
            bool acting = false;
            for (int i = 0; i < state.StepStates.Count; i++)
            {
                ExecutionCoordinatorStepState step = state.StepStates[i];
                if (step == null)
                    continue;
                permission |= step.Lifecycle ==
                        ExecutionCoordinatorStepLifecycle
                            .WaitingForPermissionBeforeResources ||
                    step.Lifecycle ==
                        ExecutionCoordinatorStepLifecycle
                            .WaitingForPermissionBeforeStart;
                resource |= step.Lifecycle ==
                    ExecutionCoordinatorStepLifecycle.WaitingForResource;
                bool active = step.Lifecycle ==
                        ExecutionCoordinatorStepLifecycle.Starting ||
                    step.Lifecycle ==
                        ExecutionCoordinatorStepLifecycle.Running ||
                    step.Lifecycle ==
                        ExecutionCoordinatorStepLifecycle
                            .WaitingForCompletion;
                if (active && step.DomainStage != null &&
                    step.DomainStage.StageId ==
                        ExecutionDomainStageIds.PlaybackStarted)
                    speaking = true;
                else if (active)
                    acting = true;
            }
            int count = (permission ? 1 : 0) + (resource ? 1 : 0) +
                (speaking ? 1 : 0) + (acting ? 1 : 0);
            if (count > 1) return CoordinatorRepresentativeState.Mixed;
            if (permission)
                return CoordinatorRepresentativeState.WaitingForPermission;
            if (resource)
                return CoordinatorRepresentativeState.WaitingForResource;
            if (speaking) return CoordinatorRepresentativeState.Speaking;
            if (acting) return CoordinatorRepresentativeState.Acting;
            return CoordinatorRepresentativeState.Idle;
        }
    }

    public static class ExecutionRuntimeSnapshotValidator
    {
        public const string ValidatorVersion =
            "execution-runtime-snapshot-validator-3c1.1";

        public static AdapterSnapshotValidationResult Validate(
            ExecutionCoordinatorRuntimeSnapshot snapshot)
        {
            List<string> failures = new List<string>();
            if (snapshot == null)
                failures.Add("SNAPSHOT_NULL");
            else
            {
                if (string.IsNullOrWhiteSpace(snapshot.PlanId))
                    failures.Add("SNAPSHOT_PLAN_ID_MISSING");
                HashSet<string> steps =
                    new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> attempts =
                    new HashSet<string>(StringComparer.Ordinal);
                Dictionary<string, string> attemptSteps =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < snapshot.Steps.Count; i++)
                {
                    ExecutionCoordinatorStepSnapshot step =
                        snapshot.Steps[i];
                    if (step == null ||
                        string.IsNullOrWhiteSpace(step.StepId) ||
                        !steps.Add(step.StepId))
                        failures.Add("SNAPSHOT_STEP_INVALID");
                    if (step != null && step.Lifecycle ==
                            ExecutionCoordinatorStepLifecycle.Terminal &&
                        step.TerminalOutcome ==
                            ExecutionTerminalOutcome.None)
                        failures.Add("SNAPSHOT_TERMINAL_OUTCOME_MISSING");
                    if (step != null && step.Lifecycle !=
                            ExecutionCoordinatorStepLifecycle.Terminal &&
                        step.TerminalOutcome !=
                            ExecutionTerminalOutcome.None)
                        failures.Add("SNAPSHOT_NONTERMINAL_HAS_OUTCOME");
                }
                for (int i = 0; i < snapshot.Attempts.Count; i++)
                {
                    ExecutionCoordinatorAttemptSnapshot attempt =
                        snapshot.Attempts[i];
                    if (attempt == null ||
                        string.IsNullOrWhiteSpace(
                            attempt.ExecutionAttemptId) ||
                        !attempts.Add(attempt.ExecutionAttemptId) ||
                        !steps.Contains(attempt.StepId))
                        failures.Add("SNAPSHOT_ATTEMPT_REFERENCE_INVALID");
                    else
                        attemptSteps[attempt.ExecutionAttemptId] =
                            attempt.StepId;
                }
                for (int i = 0; i < snapshot.Steps.Count; i++)
                {
                    string currentAttemptId =
                        snapshot.Steps[i].CurrentAttemptId;
                    if (!string.IsNullOrWhiteSpace(currentAttemptId))
                    {
                        string attemptStepId;
                        if (!attemptSteps.TryGetValue(
                                currentAttemptId, out attemptStepId))
                            failures.Add(
                                "SNAPSHOT_CURRENT_ATTEMPT_MISSING");
                        else if (!string.Equals(attemptStepId,
                            snapshot.Steps[i].StepId,
                            StringComparison.Ordinal))
                            failures.Add(
                                "SNAPSHOT_CURRENT_ATTEMPT_WRONG_STEP");
                    }
                }
                for (int i = 0; i < snapshot.Resources.Count; i++)
                {
                    ExecutionCoordinatorResourceSnapshot resource =
                        snapshot.Resources[i];
                    if (resource == null ||
                        string.IsNullOrWhiteSpace(resource.ResourceId) ||
                        !steps.Contains(resource.StepId) ||
                        !string.Equals(resource.PlanId, snapshot.PlanId,
                            StringComparison.Ordinal))
                        failures.Add("SNAPSHOT_LEASE_REFERENCE_INVALID");
                }
                if (snapshot.Lifecycle ==
                        ExecutionCoordinatorPlanLifecycle.Terminal &&
                    snapshot.FinalOutcome == ExecutionTerminalOutcome.None)
                    failures.Add("SNAPSHOT_FINAL_OUTCOME_MISSING");
                if (snapshot.Lifecycle !=
                        ExecutionCoordinatorPlanLifecycle.Terminal &&
                    snapshot.FinalOutcome != ExecutionTerminalOutcome.None)
                    failures.Add("SNAPSHOT_NONTERMINAL_FINAL_OUTCOME");
                for (int i = 0; i < snapshot.Controls.Count; i++)
                    if (snapshot.Controls[i] == null ||
                        string.IsNullOrWhiteSpace(
                            snapshot.Controls[i].ControlRequestId) ||
                        snapshot.Controls[i].ActiveControlKind ==
                            ExecutionControlType.None ||
                        (snapshot.Controls[i].EffectConfirmed &&
                         !snapshot.Controls[i].Dispatched))
                        failures.Add("SNAPSHOT_CONTROL_INVALID");
            }
            return new AdapterSnapshotValidationResult(
                failures, ValidatorVersion);
        }
    }
}
