// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    public static class ExecutionDiagnosticCodes
    {
        public const string FallbackUsed = "FALLBACK_USED";
        public const string OptionalStepFailed =
            "OPTIONAL_STEP_FAILED";
        public const string CompletionUnverified =
            "COMPLETION_UNVERIFIED";
    }

    public sealed class ExecutionPlanRuntimeSnapshot
    {
        public string PlanId { get; }
        public ExecutionPlanLifecycle Lifecycle { get; }
        public IReadOnlyList<string> ActiveStepIds { get; }
        public IReadOnlyList<string> ReadyStepIds { get; }
        public IReadOnlyList<string> WaitingStepIds { get; }
        public IReadOnlyList<string> TerminalStepIds { get; }
        public DateTime? StartedAtUtc { get; }
        public DateTime LastUpdatedAtUtc { get; }
        public ExecutionTerminalOutcome FinalOutcome { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public ExecutionPlanRuntimeSnapshot(
            string planId,
            ExecutionPlanLifecycle lifecycle,
            IEnumerable<string> activeStepIds,
            IEnumerable<string> readyStepIds,
            IEnumerable<string> waitingStepIds,
            IEnumerable<string> terminalStepIds,
            DateTime? startedAtUtc,
            DateTime lastUpdatedAtUtc,
            ExecutionTerminalOutcome finalOutcome,
            IEnumerable<string> diagnostics)
        {
            PlanId = ExecutionContractUtility.Text(planId);
            Lifecycle = lifecycle;
            ActiveStepIds =
                ExecutionContractUtility.ReadOnlyCopy(activeStepIds);
            ReadyStepIds =
                ExecutionContractUtility.ReadOnlyCopy(readyStepIds);
            WaitingStepIds =
                ExecutionContractUtility.ReadOnlyCopy(waitingStepIds);
            TerminalStepIds =
                ExecutionContractUtility.ReadOnlyCopy(terminalStepIds);
            StartedAtUtc = ExecutionContractUtility.Utc(startedAtUtc);
            LastUpdatedAtUtc =
                ExecutionContractUtility.Utc(lastUpdatedAtUtc);
            FinalOutcome = finalOutcome;
            Diagnostics =
                ExecutionContractUtility.ReadOnlyCopy(diagnostics);
        }
    }

    public sealed class ExecutionStepRuntimeSnapshot
    {
        public string PlanId { get; }
        public string StepId { get; }
        public ExecutionStepLifecycle Lifecycle { get; }
        public ExecutionDomainStage DomainStage { get; }
        public string CurrentAttemptId { get; }
        public IReadOnlyList<string> AttemptIds { get; }
        public DateTime? StartedAtUtc { get; }
        public DateTime? CompletedAtUtc { get; }
        public DateTime? TimeoutDeadlineUtc { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }
        public IReadOnlyList<string> FailureCodes { get; }

        public ExecutionStepRuntimeSnapshot(
            string planId,
            string stepId,
            ExecutionStepLifecycle lifecycle,
            ExecutionDomainStage domainStage,
            string currentAttemptId,
            IEnumerable<string> attemptIds,
            DateTime? startedAtUtc,
            DateTime? completedAtUtc,
            DateTime? timeoutDeadlineUtc,
            ExecutionTerminalOutcome terminalOutcome,
            IEnumerable<string> failureCodes)
        {
            PlanId = ExecutionContractUtility.Text(planId);
            StepId = ExecutionContractUtility.Text(stepId);
            Lifecycle = lifecycle;
            DomainStage = domainStage;
            CurrentAttemptId =
                ExecutionContractUtility.Text(currentAttemptId);
            AttemptIds =
                ExecutionContractUtility.ReadOnlyCopy(attemptIds);
            StartedAtUtc = ExecutionContractUtility.Utc(startedAtUtc);
            CompletedAtUtc =
                ExecutionContractUtility.Utc(completedAtUtc);
            TimeoutDeadlineUtc =
                ExecutionContractUtility.Utc(timeoutDeadlineUtc);
            TerminalOutcome = terminalOutcome;
            FailureCodes =
                ExecutionContractUtility.ReadOnlyCopy(failureCodes);
        }
    }

    public sealed class ExecutionAttemptSnapshot
    {
        public string ExecutionAttemptId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string RequestId { get; }
        public int AttemptNumber { get; }
        public ExecutionAttemptLifecycle Lifecycle { get; }
        public ExecutionDomainStage DomainStage { get; }
        public DateTime? StartedAtUtc { get; }
        public DateTime? EndedAtUtc { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }
        public IReadOnlyList<string> FailureCodes { get; }

        public ExecutionAttemptSnapshot(
            string executionAttemptId,
            string planId,
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
            ExecutionAttemptId =
                ExecutionContractUtility.Text(executionAttemptId);
            PlanId = ExecutionContractUtility.Text(planId);
            StepId = ExecutionContractUtility.Text(stepId);
            RequestId = ExecutionContractUtility.Text(requestId);
            AttemptNumber = attemptNumber;
            Lifecycle = lifecycle;
            DomainStage = domainStage;
            StartedAtUtc = ExecutionContractUtility.Utc(startedAtUtc);
            EndedAtUtc = ExecutionContractUtility.Utc(endedAtUtc);
            TerminalOutcome = terminalOutcome;
            FailureCodes =
                ExecutionContractUtility.ReadOnlyCopy(failureCodes);
        }
    }
}
