// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Reduction;

namespace SalieriAI.Core.Execution.Orchestration.Runtime
{
    public sealed class ExecutionCoordinatorRuntimeState
    {
        public string PlanId { get; }
        public int Generation { get; }
        public ExecutionCoordinatorPlanLifecycle PlanLifecycle { get; }
        public IReadOnlyList<ExecutionCoordinatorStepState> StepStates
        {
            get;
        }
        public IReadOnlyList<ExecutionCoordinatorAttemptState>
            AttemptStates { get; }
        public IReadOnlyList<ResourceLeaseReference>
            ActiveLeaseReferences { get; }
        public IReadOnlyList<ResourceLeaseReference>
            ResourceLeaseReferences { get; }
        public IReadOnlyList<ExecutionCoordinatorControlState>
            PendingControlRequests { get; }
        public IReadOnlyList<string> ProcessedEventIds { get; }
        public DateTime? StartedAtUtc { get; }
        public DateTime LastUpdatedAtUtc { get; }
        public ExecutionTerminalOutcome FinalOutcome { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public ExecutionCoordinatorRuntimeState(
            string planId,
            int generation,
            ExecutionCoordinatorPlanLifecycle planLifecycle,
            IEnumerable<ExecutionCoordinatorStepState> stepStates,
            IEnumerable<ExecutionCoordinatorAttemptState> attemptStates,
            IEnumerable<ResourceLeaseReference> resourceLeaseReferences,
            IEnumerable<ExecutionCoordinatorControlState>
                pendingControlRequests,
            IEnumerable<string> processedEventIds,
            DateTime? startedAtUtc,
            DateTime lastUpdatedAtUtc,
            ExecutionTerminalOutcome finalOutcome,
            IEnumerable<string> diagnostics)
        {
            PlanId = Text(planId);
            Generation = generation;
            PlanLifecycle = planLifecycle;
            StepStates = Copy(stepStates);
            AttemptStates = Copy(attemptStates);
            ResourceLeaseReferences = Copy(resourceLeaseReferences);
            ActiveLeaseReferences = Active(ResourceLeaseReferences);
            PendingControlRequests = Copy(pendingControlRequests);
            ProcessedEventIds = Copy(processedEventIds);
            StartedAtUtc = Utc(startedAtUtc);
            LastUpdatedAtUtc = Utc(lastUpdatedAtUtc);
            FinalOutcome = finalOutcome;
            Diagnostics = Copy(diagnostics);
        }

        public ExecutionCoordinatorStepState FindStep(string stepId)
        {
            for (int i = 0; i < StepStates.Count; i++)
            {
                if (string.Equals(
                        StepStates[i].StepId,
                        stepId,
                        StringComparison.Ordinal))
                {
                    return StepStates[i];
                }
            }

            return null;
        }

        public ExecutionCoordinatorAttemptState FindAttempt(
            string executionAttemptId)
        {
            for (int i = 0; i < AttemptStates.Count; i++)
            {
                if (string.Equals(
                        AttemptStates[i].ExecutionAttemptId,
                        executionAttemptId,
                        StringComparison.Ordinal))
                {
                    return AttemptStates[i];
                }
            }

            return null;
        }

        private static string Text(string value)
        {
            return value ?? string.Empty;
        }

        private static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime) ||
                value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static DateTime? Utc(DateTime? value)
        {
            return value.HasValue ? Utc(value.Value) : (DateTime?)null;
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(
                source != null ? new List<T>(source) : new List<T>()
            );
        }

        private static IReadOnlyList<ResourceLeaseReference> Active(
            IReadOnlyList<ResourceLeaseReference> source)
        {
            List<ResourceLeaseReference> active =
                new List<ResourceLeaseReference>();
            for (int i = 0; i < source.Count; i++)
            {
                ResourceLeaseReference lease = source[i];
                if (lease != null &&
                    lease.Status != ExecutionResourceLeaseStatus.Released &&
                    lease.Status != ExecutionResourceLeaseStatus.Rejected)
                {
                    active.Add(lease);
                }
            }
            return new ReadOnlyCollection<ResourceLeaseReference>(active);
        }
    }

    public sealed class ExecutionCoordinatorStepState
    {
        public string StepId { get; }
        public ExecutionCoordinatorStepLifecycle Lifecycle { get; }
        public ExecutionDomainStage DomainStage { get; }
        public bool CompletionPolicySatisfied { get; }
        public bool DependencySatisfied { get; }
        public bool ExecutorAccepted { get; }
        public string CurrentAttemptId { get; }
        public IReadOnlyList<string> AttemptIds { get; }
        public IReadOnlyList<string> ActiveLeaseIds { get; }
        public DateTime? StartedAtUtc { get; }
        public DateTime? CompletedAtUtc { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }
        public ExecutionTerminalOutcome PendingTerminalOutcome { get; }
        public IReadOnlyList<string> FailureCodes { get; }
        public string ResourceWaitCycleId { get; }
        public DateTime? ResourceWaitStartedAtUtc { get; }
        public DateTime? ResourceWaitDeadlineUtc { get; }

        public ExecutionCoordinatorStepState(
            string stepId,
            ExecutionCoordinatorStepLifecycle lifecycle,
            ExecutionDomainStage domainStage,
            bool completionPolicySatisfied,
            bool dependencySatisfied,
            bool executorAccepted,
            string currentAttemptId,
            IEnumerable<string> attemptIds,
            IEnumerable<string> activeLeaseIds,
            DateTime? startedAtUtc,
            DateTime? completedAtUtc,
            ExecutionTerminalOutcome terminalOutcome,
            ExecutionTerminalOutcome pendingTerminalOutcome,
            IEnumerable<string> failureCodes,
            string resourceWaitCycleId = "",
            DateTime? resourceWaitStartedAtUtc = null,
            DateTime? resourceWaitDeadlineUtc = null)
        {
            StepId = stepId ?? string.Empty;
            Lifecycle = lifecycle;
            DomainStage = domainStage;
            CompletionPolicySatisfied = completionPolicySatisfied;
            DependencySatisfied = dependencySatisfied;
            ExecutorAccepted = executorAccepted;
            CurrentAttemptId = currentAttemptId ?? string.Empty;
            AttemptIds = Copy(attemptIds);
            ActiveLeaseIds = Copy(activeLeaseIds);
            StartedAtUtc = Utc(startedAtUtc);
            CompletedAtUtc = Utc(completedAtUtc);
            TerminalOutcome = terminalOutcome;
            PendingTerminalOutcome = pendingTerminalOutcome;
            FailureCodes = Copy(failureCodes);
            ResourceWaitCycleId = resourceWaitCycleId ?? string.Empty;
            ResourceWaitStartedAtUtc = Utc(resourceWaitStartedAtUtc);
            ResourceWaitDeadlineUtc = Utc(resourceWaitDeadlineUtc);
        }

        private static DateTime? Utc(DateTime? value)
        {
            if (!value.HasValue || value.Value == default(DateTime) ||
                value.Value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            return value.Value.Kind == DateTimeKind.Local
                ? value.Value.ToUniversalTime()
                : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(
                source != null ? new List<T>(source) : new List<T>()
            );
        }
    }

    public sealed class ExecutionCoordinatorAttemptState
    {
        public string ExecutionAttemptId { get; }
        public string StepId { get; }
        public string RequestId { get; }
        public int AttemptNumber { get; }
        public ExecutionAttemptLifecycle Lifecycle { get; }
        public ExecutionDomainStage DomainStage { get; }
        public DateTime? StartedAtUtc { get; }
        public DateTime? EndedAtUtc { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }
        public IReadOnlyList<string> FailureCodes { get; }

        public ExecutionCoordinatorAttemptState(
            string executionAttemptId,
            string stepId,
            string requestId,
            int attemptNumber,
            ExecutionAttemptLifecycle lifecycle,
            ExecutionDomainStage domainStage,
            DateTime? startedAtUtc,
            DateTime? endedAtUtc,
            ExecutionTerminalOutcome terminalOutcome,
            IEnumerable<string> failureCodes)
        {
            ExecutionAttemptId = executionAttemptId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            AttemptNumber = attemptNumber;
            Lifecycle = lifecycle;
            DomainStage = domainStage;
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
            TerminalOutcome = terminalOutcome;
            FailureCodes = new ReadOnlyCollection<string>(
                failureCodes != null
                    ? new List<string>(failureCodes)
                    : new List<string>()
            );
        }
    }

    public sealed class ExecutionCoordinatorControlState
    {
        public string ControlRequestId { get; }
        public ExecutionControlType ControlType { get; }
        public int PriorityRank { get; }
        public bool Dispatched { get; }
        public bool EffectConfirmed { get; }

        public ExecutionCoordinatorControlState(
            string controlRequestId,
            ExecutionControlType controlType,
            int priorityRank,
            bool dispatched,
            bool effectConfirmed)
        {
            ControlRequestId = controlRequestId ?? string.Empty;
            ControlType = controlType;
            PriorityRank = priorityRank;
            Dispatched = dispatched;
            EffectConfirmed = effectConfirmed;
        }
    }
}
