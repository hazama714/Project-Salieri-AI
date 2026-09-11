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
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    public enum SpeechRuntimeAdapterStatus
    {
        Observed = 0,
        RejectedInvalid = 1,
        Failed = 2
    }

    public enum SpeechRuntimeCorrelationStatus
    {
        Accepted = 0,
        Duplicate = 1,
        Stale = 2,
        Mismatch = 3,
        Failed = 4
    }

    public enum SpeechShadowRunStatus
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

    public sealed class SpeechRuntimeAdapterResult
    {
        public string RuntimeSpeechId { get; }
        public int RuntimeGeneration { get; }
        public SpeechRuntimeAdapterStatus Status { get; }
        public SpeechLifecycleResult LifecycleResult { get; }
        public string FailureReason { get; }

        public SpeechRuntimeAdapterResult(
            string runtimeSpeechId,
            int runtimeGeneration,
            SpeechRuntimeAdapterStatus status,
            SpeechLifecycleResult lifecycleResult,
            string failureReason)
        {
            RuntimeSpeechId = runtimeSpeechId ?? string.Empty;
            RuntimeGeneration = runtimeGeneration;
            Status = status;
            LifecycleResult = lifecycleResult;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    public sealed class SpeechRuntimeCorrelationResult
    {
        public SpeechRuntimeCorrelationStatus Status { get; }
        public string FailureReason { get; }

        public SpeechRuntimeCorrelationResult(
            SpeechRuntimeCorrelationStatus status,
            string failureReason)
        {
            Status = status;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    public sealed class SpeechShadowEventIds
    {
        public string LifecycleEventId { get; }
        public string CompletionPolicyEventId { get; }
        public string TerminalEventId { get; }

        public SpeechShadowEventIds(
            string lifecycleEventId,
            string completionPolicyEventId,
            string terminalEventId)
        {
            LifecycleEventId = lifecycleEventId ?? string.Empty;
            CompletionPolicyEventId =
                completionPolicyEventId ?? string.Empty;
            TerminalEventId = terminalEventId ?? string.Empty;
        }
    }

    public sealed class SpeechShadowObservation
    {
        public string ObservationId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string RequestId { get; }
        public string RuntimeSpeechId { get; }
        public int RuntimeGeneration { get; }
        public SpeechAdapterLifecycle Lifecycle { get; }
        public InteractionState InteractionStateAtObservation { get; }
        public ExecutionCoordinatorEventType CoordinatorEventType
            { get; }
        public ExecutionCoordinatorStepLifecycle? ReducerStateBefore
            { get; }
        public ExecutionCoordinatorStepLifecycle? ReducerStateAfter
            { get; }
        public bool CompletionPolicySatisfied { get; }
        public IReadOnlyList<ExecutionCoordinatorEffectIntentType>
            GeneratedEffectIntentTypes { get; }
        public string FailureReason { get; }
        public DateTime ObservedAtUtc { get; }
        public SpeechShadowRunStatus RunStatus { get; }

        public SpeechShadowObservation(
            string observationId,
            string planId,
            string stepId,
            string executionAttemptId,
            string requestId,
            string runtimeSpeechId,
            int runtimeGeneration,
            SpeechAdapterLifecycle lifecycle,
            InteractionState interactionStateAtObservation,
            ExecutionCoordinatorEventType coordinatorEventType,
            ExecutionCoordinatorStepLifecycle? reducerStateBefore,
            ExecutionCoordinatorStepLifecycle? reducerStateAfter,
            bool completionPolicySatisfied,
            IEnumerable<ExecutionCoordinatorEffectIntentType>
                generatedEffectIntentTypes,
            string failureReason,
            DateTime observedAtUtc,
            SpeechShadowRunStatus runStatus)
        {
            ObservationId = observationId ?? string.Empty;
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            ExecutionAttemptId = executionAttemptId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            RuntimeSpeechId = runtimeSpeechId ?? string.Empty;
            RuntimeGeneration = runtimeGeneration;
            Lifecycle = lifecycle;
            InteractionStateAtObservation =
                interactionStateAtObservation;
            CoordinatorEventType = coordinatorEventType;
            ReducerStateBefore = reducerStateBefore;
            ReducerStateAfter = reducerStateAfter;
            CompletionPolicySatisfied = completionPolicySatisfied;
            GeneratedEffectIntentTypes =
                new ReadOnlyCollection<
                    ExecutionCoordinatorEffectIntentType>(
                    generatedEffectIntentTypes != null
                        ? new List<
                            ExecutionCoordinatorEffectIntentType>(
                            generatedEffectIntentTypes)
                        : new List<
                            ExecutionCoordinatorEffectIntentType>());
            FailureReason = failureReason ?? string.Empty;
            ObservedAtUtc = observedAtUtc;
            RunStatus = runStatus;
        }
    }

    public sealed class SpeechShadowExecutionResult
    {
        public SpeechShadowObservation Observation { get; }
        public SpeechRuntimeAdapterResult AdapterResult { get; }
        public IReadOnlyList<ExecutionCoordinatorEvent>
            CoordinatorEvents { get; }
        public IReadOnlyList<ExecutionCoordinatorReductionResult>
            ReductionResults { get; }
        public bool SpeechStartInvoked { get; }

        public SpeechShadowExecutionResult(
            SpeechShadowObservation observation,
            SpeechRuntimeAdapterResult adapterResult,
            IEnumerable<ExecutionCoordinatorEvent> coordinatorEvents,
            IEnumerable<ExecutionCoordinatorReductionResult>
                reductionResults,
            bool speechStartInvoked)
        {
            Observation = observation;
            AdapterResult = adapterResult;
            CoordinatorEvents = Copy(coordinatorEvents);
            ReductionResults = Copy(reductionResults);
            SpeechStartInvoked = speechStartInvoked;
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(
                source != null
                    ? new List<T>(source)
                    : new List<T>());
        }
    }
}
