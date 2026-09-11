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

namespace SalieriAI.Core.Execution.Orchestration.Adapters
{
    public enum CoordinatorRepresentativeState
    {
        Idle = 0,
        WaitingForPermission = 1,
        WaitingForResource = 2,
        Speaking = 3,
        Acting = 4,
        Mixed = 5,
        Interrupted = 6,
        SafetyPreempting = 7,
        Terminal = 8
    }

    public sealed class ExecutionCoordinatorStepSnapshot
    {
        public string StepId { get; }
        public ExecutionCoordinatorStepLifecycle Lifecycle { get; }
        public string DomainStageId { get; }
        public string CurrentAttemptId { get; }
        public bool CompletionPolicySatisfied { get; }
        public bool DependencySatisfied { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }
        public IReadOnlyList<string> FailureCodes { get; }

        public ExecutionCoordinatorStepSnapshot(
            string stepId,
            ExecutionCoordinatorStepLifecycle lifecycle,
            string domainStageId, string currentAttemptId,
            bool completionPolicySatisfied,
            bool dependencySatisfied,
            ExecutionTerminalOutcome terminalOutcome,
            IEnumerable<string> failureCodes)
        {
            StepId = AdapterContractUtility.Text(stepId);
            Lifecycle = lifecycle;
            DomainStageId = AdapterContractUtility.Text(domainStageId);
            CurrentAttemptId =
                AdapterContractUtility.Text(currentAttemptId);
            CompletionPolicySatisfied = completionPolicySatisfied;
            DependencySatisfied = dependencySatisfied;
            TerminalOutcome = terminalOutcome;
            FailureCodes = AdapterContractUtility.Copy(failureCodes);
        }
    }

    public sealed class ExecutionCoordinatorResourceSnapshot
    {
        public string ResourceId { get; }
        public string LeaseId { get; }
        public string PlanId { get; }
        public ExecutionResourceLeaseStatus LeaseStatus { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public string StepId { get; }

        public ExecutionCoordinatorResourceSnapshot(
            string resourceId, string leaseId, string planId,
            ExecutionResourceLeaseStatus leaseStatus,
            ExecutionResourceAccessMode accessMode,
            string stepId)
        {
            ResourceId = AdapterContractUtility.Text(resourceId);
            LeaseId = AdapterContractUtility.Text(leaseId);
            PlanId = AdapterContractUtility.Text(planId);
            LeaseStatus = leaseStatus;
            AccessMode = accessMode;
            StepId = AdapterContractUtility.Text(stepId);
        }
    }

    public sealed class ExecutionCoordinatorAttemptSnapshot
    {
        public string ExecutionAttemptId { get; }
        public string StepId { get; }
        public string RequestId { get; }
        public int AttemptNumber { get; }
        public ExecutionAttemptLifecycle Lifecycle { get; }
        public string DomainStageId { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }

        public ExecutionCoordinatorAttemptSnapshot(
            string executionAttemptId, string stepId, string requestId,
            int attemptNumber, ExecutionAttemptLifecycle lifecycle,
            string domainStageId,
            ExecutionTerminalOutcome terminalOutcome)
        {
            ExecutionAttemptId =
                AdapterContractUtility.Text(executionAttemptId);
            StepId = AdapterContractUtility.Text(stepId);
            RequestId = AdapterContractUtility.Text(requestId);
            AttemptNumber = attemptNumber;
            Lifecycle = lifecycle;
            DomainStageId = AdapterContractUtility.Text(domainStageId);
            TerminalOutcome = terminalOutcome;
        }
    }

    public sealed class ExecutionCoordinatorControlSnapshot
    {
        public ExecutionControlType ActiveControlKind { get; }
        public int ActiveControlPriority { get; }
        public string ControlRequestId { get; }
        public bool Dispatched { get; }
        public bool EffectConfirmed { get; }

        public ExecutionCoordinatorControlSnapshot(
            ExecutionControlType activeControlKind,
            int activeControlPriority,
            string controlRequestId,
            bool dispatched,
            bool effectConfirmed)
        {
            ActiveControlKind = activeControlKind;
            ActiveControlPriority = activeControlPriority;
            ControlRequestId =
                AdapterContractUtility.Text(controlRequestId);
            Dispatched = dispatched;
            EffectConfirmed = effectConfirmed;
        }
    }

    public sealed class ExecutionCoordinatorRuntimeSnapshot
    {
        public string PlanId { get; }
        public ExecutionCoordinatorPlanLifecycle Lifecycle { get; }
        public ExecutionTerminalOutcome FinalOutcome { get; }
        public int Generation { get; }
        public DateTime? StartedAtUtc { get; }
        public DateTime UpdatedAtUtc { get; }
        public IReadOnlyList<ExecutionCoordinatorStepSnapshot> Steps
            { get; }
        public IReadOnlyList<ExecutionCoordinatorResourceSnapshot>
            Resources { get; }
        public IReadOnlyList<ExecutionCoordinatorAttemptSnapshot>
            Attempts { get; }
        public IReadOnlyList<ExecutionCoordinatorControlSnapshot>
            Controls { get; }
        public CoordinatorRepresentativeState RepresentativeState
            { get; }

        public ExecutionCoordinatorRuntimeSnapshot(
            string planId,
            ExecutionCoordinatorPlanLifecycle lifecycle,
            ExecutionTerminalOutcome finalOutcome,
            int generation, DateTime? startedAtUtc,
            DateTime updatedAtUtc,
            IEnumerable<ExecutionCoordinatorStepSnapshot> steps,
            IEnumerable<ExecutionCoordinatorAttemptSnapshot> attempts,
            IEnumerable<ExecutionCoordinatorResourceSnapshot> resources,
            IEnumerable<ExecutionCoordinatorControlSnapshot> controls,
            CoordinatorRepresentativeState representativeState)
        {
            PlanId = AdapterContractUtility.Text(planId);
            Lifecycle = lifecycle;
            FinalOutcome = finalOutcome;
            Generation = generation;
            StartedAtUtc = startedAtUtc.HasValue
                ? AdapterContractUtility.Utc(startedAtUtc.Value)
                : (DateTime?)null;
            UpdatedAtUtc = AdapterContractUtility.Utc(updatedAtUtc);
            Steps = AdapterContractUtility.Copy(steps);
            Attempts = AdapterContractUtility.Copy(attempts);
            Resources = AdapterContractUtility.Copy(resources);
            Controls = AdapterContractUtility.Copy(controls);
            RepresentativeState = representativeState;
        }
    }

    public sealed class AdapterSnapshotValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<string> FailureCodes { get; }
        public string ValidatorVersion { get; }

        public AdapterSnapshotValidationResult(
            IEnumerable<string> failureCodes, string validatorVersion)
        {
            FailureCodes = AdapterContractUtility.Copy(failureCodes);
            IsValid = FailureCodes.Count == 0;
            ValidatorVersion =
                AdapterContractUtility.Text(validatorVersion);
        }
    }
}
