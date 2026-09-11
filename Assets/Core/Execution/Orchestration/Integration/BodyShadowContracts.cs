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
using System.Collections.ObjectModel;
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.Runtime;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    public enum BodyRuntimeAdapterStatus
    {
        Observed = 0,
        RejectedInvalid = 1,
        Failed = 2
    }

    public enum BodyRuntimeCorrelationStatus
    {
        Accepted = 0,
        Duplicate = 1,
        Stale = 2,
        Mismatch = 3,
        Failed = 4
    }

    public enum BodyShadowRunStatus
    {
        Completed = 0,
        Duplicate = 1,
        Stale = 2,
        CorrelationFailed = 3,
        RuntimeAdapterFailed = 4,
        EventTranslationFailed = 5,
        ReductionRejected = 6,
        ObservationOnly = 7
    }

    public sealed class BodyRuntimeAdapterResult
    {
        public string RuntimeExecutionToken { get; }
        public int RuntimeGeneration { get; }
        public string ActionId { get; }
        public BodyRuntimeLifecycle Lifecycle { get; }
        public BodyPhysicalVerificationLevel VerificationLevel { get; }
        public BodyRuntimeAdapterStatus Status { get; }
        public BodyExecutionLifecycleResult LifecycleResult { get; }
        public string Diagnostic { get; }
        public string FailureReason { get; }

        public BodyRuntimeAdapterResult(
            string runtimeExecutionToken, int runtimeGeneration,
            string actionId, BodyRuntimeLifecycle lifecycle,
            BodyPhysicalVerificationLevel verificationLevel,
            BodyRuntimeAdapterStatus status,
            BodyExecutionLifecycleResult lifecycleResult,
            string diagnostic, string failureReason)
        {
            RuntimeExecutionToken = runtimeExecutionToken ?? string.Empty;
            RuntimeGeneration = runtimeGeneration;
            ActionId = actionId ?? string.Empty;
            Lifecycle = lifecycle;
            VerificationLevel = verificationLevel;
            Status = status;
            LifecycleResult = lifecycleResult;
            Diagnostic = diagnostic ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    public sealed class BodyRuntimeCorrelationResult
    {
        public BodyRuntimeCorrelationStatus Status { get; }
        public string FailureReason { get; }
        public BodyRuntimeCorrelationResult(
            BodyRuntimeCorrelationStatus status, string failureReason)
        {
            Status = status;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    public sealed class BodyPermissionSnapshot
    {
        public bool? CanStartAction { get; }
        public bool? CanMoveServo { get; }
        public bool? IsEmergencyMode { get; }
        public BodyPermissionSnapshot(
            bool? canStartAction, bool? canMoveServo,
            bool? isEmergencyMode)
        {
            CanStartAction = canStartAction;
            CanMoveServo = canMoveServo;
            IsEmergencyMode = isEmergencyMode;
        }
    }

    public sealed class BodyShadowEventIds
    {
        public string LifecycleEventId { get; }
        public string CompletionPolicyEventId { get; }
        public string TerminalEventId { get; }
        public BodyShadowEventIds(
            string lifecycleEventId,
            string completionPolicyEventId,
            string terminalEventId)
        {
            LifecycleEventId = lifecycleEventId ?? string.Empty;
            CompletionPolicyEventId = completionPolicyEventId ?? string.Empty;
            TerminalEventId = terminalEventId ?? string.Empty;
        }
    }

    public sealed class BodyShadowObservation
    {
        public string ObservationId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string RequestId { get; }
        public string ActionId { get; }
        public string RuntimeExecutionToken { get; }
        public int RuntimeGeneration { get; }
        public BodyRuntimeLifecycle Lifecycle { get; }
        public ExecutionDomainStage DomainStage { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }
        public InteractionState InteractionStateAtObservation { get; }
        public BodyPermissionSnapshot PermissionSnapshot { get; }
        public BodyPhysicalVerificationLevel VerificationLevel { get; }
        public ExecutionCoordinatorEventType CoordinatorEventType { get; }
        public ExecutionCoordinatorStepLifecycle? ReducerStateBefore { get; }
        public ExecutionCoordinatorStepLifecycle? ReducerStateAfter { get; }
        public bool CompletionPolicySatisfied { get; }
        public IReadOnlyList<ExecutionCoordinatorEffectIntentType>
            GeneratedEffectIntentTypes { get; }
        public string FailureReason { get; }
        public string Diagnostic { get; }
        public DateTime ObservedAtUtc { get; }
        public BodyShadowRunStatus RunStatus { get; }

        public BodyShadowObservation(
            string observationId, string planId, string stepId,
            string executionAttemptId, string requestId,
            string actionId, string runtimeExecutionToken,
            int runtimeGeneration, BodyRuntimeLifecycle lifecycle,
            ExecutionDomainStage domainStage,
            ExecutionTerminalOutcome terminalOutcome,
            InteractionState interactionStateAtObservation,
            BodyPermissionSnapshot permissionSnapshot,
            BodyPhysicalVerificationLevel verificationLevel,
            ExecutionCoordinatorEventType coordinatorEventType,
            ExecutionCoordinatorStepLifecycle? reducerStateBefore,
            ExecutionCoordinatorStepLifecycle? reducerStateAfter,
            bool completionPolicySatisfied,
            IEnumerable<ExecutionCoordinatorEffectIntentType>
                generatedEffectIntentTypes,
            string failureReason, string diagnostic,
            DateTime observedAtUtc, BodyShadowRunStatus runStatus)
        {
            ObservationId = observationId ?? string.Empty;
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            ExecutionAttemptId = executionAttemptId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            RuntimeExecutionToken = runtimeExecutionToken ?? string.Empty;
            RuntimeGeneration = runtimeGeneration;
            Lifecycle = lifecycle;
            DomainStage = domainStage;
            TerminalOutcome = terminalOutcome;
            InteractionStateAtObservation = interactionStateAtObservation;
            PermissionSnapshot = permissionSnapshot;
            VerificationLevel = verificationLevel;
            CoordinatorEventType = coordinatorEventType;
            ReducerStateBefore = reducerStateBefore;
            ReducerStateAfter = reducerStateAfter;
            CompletionPolicySatisfied = completionPolicySatisfied;
            GeneratedEffectIntentTypes = new ReadOnlyCollection<
                ExecutionCoordinatorEffectIntentType>(
                    generatedEffectIntentTypes != null
                        ? new List<ExecutionCoordinatorEffectIntentType>(
                            generatedEffectIntentTypes)
                        : new List<ExecutionCoordinatorEffectIntentType>());
            FailureReason = failureReason ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
            ObservedAtUtc = observedAtUtc;
            RunStatus = runStatus;
        }
    }

    public sealed class BodyShadowExecutionResult
    {
        public BodyShadowObservation Observation { get; }
        public BodyRuntimeAdapterResult AdapterResult { get; }
        public IReadOnlyList<ExecutionCoordinatorEvent> CoordinatorEvents
            { get; }
        public IReadOnlyList<ExecutionCoordinatorReductionResult>
            ReductionResults { get; }
        public bool BodyStartInvoked { get; }

        public BodyShadowExecutionResult(
            BodyShadowObservation observation,
            BodyRuntimeAdapterResult adapterResult,
            IEnumerable<ExecutionCoordinatorEvent> events,
            IEnumerable<ExecutionCoordinatorReductionResult> reductions,
            bool bodyStartInvoked)
        {
            Observation = observation;
            AdapterResult = adapterResult;
            CoordinatorEvents = Copy(events);
            ReductionResults = Copy(reductions);
            BodyStartInvoked = bodyStartInvoked;
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(source != null
                ? new List<T>(source) : new List<T>());
        }
    }
}
