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
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.Execution.Orchestration.Validation;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>
    /// Observes legacy speech lifecycle facts. It never starts speech and
    /// never dispatches reducer effects.
    /// </summary>
    public sealed class ExecutionSpeechShadowDriver
    {
        public const string DriverVersion =
            "execution-speech-shadow-driver-3c4.1";

        private readonly ExecutionSpeechRuntimeAdapter adapter;
        private readonly SpeechRuntimeCorrelationTracker correlation;

        public int TrackedRuntimeCount =>
            correlation != null ? correlation.TrackedRuntimeCount : 0;

        public ExecutionSpeechShadowDriver(
            ExecutionSpeechRuntimeAdapter adapter,
            SpeechRuntimeCorrelationTracker correlation)
        {
            this.adapter = adapter;
            this.correlation = correlation;
        }

        public SpeechShadowExecutionResult Observe(
            SpeechExecutionRequest request,
            SpeechPlaybackRuntimeFact runtimeFact,
            SpeechShadowEventIds eventIds,
            string observationId,
            DateTime observedAtUtc,
            string eventSource,
            int generation,
            InteractionState interactionStateAtObservation,
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorRuntimeState state,
            ExecutionCoordinatorReducerContext reducerContext)
        {
            ExecutionCoordinatorStepLifecycle? before =
                StepLifecycle(state,
                    request != null ? request.StepId : string.Empty);
            if (adapter == null || correlation == null)
                return Failure(
                    request, runtimeFact, observationId,
                    observedAtUtc, interactionStateAtObservation,
                    before, SpeechShadowRunStatus.RuntimeAdapterFailed,
                    "SPEECH_SHADOW_DEPENDENCY_UNAVAILABLE", null);

            SpeechRuntimeAdapterResult adapterResult =
                adapter.Observe(request, runtimeFact, observedAtUtc);
            if (adapterResult.Status !=
                    SpeechRuntimeAdapterStatus.Observed ||
                adapterResult.LifecycleResult == null)
                return Failure(
                    request, runtimeFact, observationId,
                    observedAtUtc, interactionStateAtObservation,
                    before, SpeechShadowRunStatus.RuntimeAdapterFailed,
                    adapterResult.FailureReason, adapterResult);

            SpeechRuntimeCorrelationResult correlationResult =
                correlation.Observe(request, runtimeFact);
            if (correlationResult.Status !=
                SpeechRuntimeCorrelationStatus.Accepted)
            {
                SpeechShadowRunStatus status;
                switch (correlationResult.Status)
                {
                    case SpeechRuntimeCorrelationStatus.Duplicate:
                        status = SpeechShadowRunStatus.Duplicate;
                        break;
                    case SpeechRuntimeCorrelationStatus.Stale:
                        status = SpeechShadowRunStatus.Stale;
                        break;
                    default:
                        status = SpeechShadowRunStatus
                            .CorrelationFailed;
                        break;
                }
                return Failure(
                    request, runtimeFact, observationId,
                    observedAtUtc, interactionStateAtObservation,
                    before, status,
                    correlationResult.FailureReason, adapterResult);
            }

            string idFailure = ValidateEventIds(
                runtimeFact.Lifecycle, eventIds);
            if (idFailure.Length > 0)
                return Failure(
                    request, runtimeFact, observationId,
                    observedAtUtc, interactionStateAtObservation,
                    before,
                    SpeechShadowRunStatus.EventTranslationFailed,
                    idFailure, adapterResult);

            AdapterTranslationResult<ExecutionCoordinatorEvent>
                primaryTranslation = ExecutionRuntimeAdapterTranslator
                    .ToSpeechEvent(
                        request,
                        adapterResult.LifecycleResult,
                        eventIds.LifecycleEventId,
                        generation,
                        eventSource);
            if (!primaryTranslation.IsValid)
                return Failure(
                    request, runtimeFact, observationId,
                    observedAtUtc, interactionStateAtObservation,
                    before,
                    SpeechShadowRunStatus.EventTranslationFailed,
                    FirstFailure(primaryTranslation.FailureCodes),
                    adapterResult);

            var events = new List<ExecutionCoordinatorEvent>();
            var reductions =
                new List<ExecutionCoordinatorReductionResult>();
            events.Add(primaryTranslation.Value);

            if (plan == null || state == null || reducerContext == null)
                return Success(
                    request, runtimeFact, observationId,
                    observedAtUtc, interactionStateAtObservation,
                    before, before, false,
                    SpeechShadowRunStatus.ObservationOnly,
                    string.Empty, adapterResult, events, reductions);

            ExecutionCoordinatorRuntimeState current = state;
            ExecutionCoordinatorReductionResult primaryReduction =
                ExecutionCoordinatorReducer.Reduce(
                    plan, current, primaryTranslation.Value,
                    reducerContext);
            reductions.Add(primaryReduction);
            if (!Applied(primaryReduction))
                return ReductionFailure(
                    request, runtimeFact, observationId,
                    observedAtUtc, interactionStateAtObservation,
                    before, adapterResult, events, reductions,
                    "SPEECH_PRIMARY_REDUCTION_NOT_APPLIED");
            current = primaryReduction.State;

            if (runtimeFact.Lifecycle ==
                SpeechAdapterLifecycle.PlaybackCompleted)
            {
                ExecutionCoordinatorEvent completion = Event(
                    eventIds.CompletionPolicyEventId,
                    ExecutionCoordinatorEventType
                        .CompletionPolicyReached,
                    request, observedAtUtc, eventSource, generation,
                    null, ExecutionTerminalOutcome.None);
                events.Add(completion);
                ExecutionCoordinatorReductionResult completionReduction =
                    ExecutionCoordinatorReducer.Reduce(
                        plan, current, completion, reducerContext);
                reductions.Add(completionReduction);
                if (!Applied(completionReduction))
                    return ReductionFailure(
                        request, runtimeFact, observationId,
                        observedAtUtc, interactionStateAtObservation,
                        before, adapterResult, events, reductions,
                        "SPEECH_COMPLETION_POLICY_NOT_APPLIED");
                current = completionReduction.State;

                ExecutionCoordinatorEvent terminal = Event(
                    eventIds.TerminalEventId,
                    ExecutionCoordinatorEventType.ExecutorTerminal,
                    request, observedAtUtc, eventSource, generation,
                    null, ExecutionTerminalOutcome.Succeeded);
                events.Add(terminal);
                ExecutionCoordinatorReductionResult terminalReduction =
                    ExecutionCoordinatorReducer.Reduce(
                        plan, current, terminal, reducerContext);
                reductions.Add(terminalReduction);
                if (!Applied(terminalReduction))
                    return ReductionFailure(
                        request, runtimeFact, observationId,
                        observedAtUtc, interactionStateAtObservation,
                        before, adapterResult, events, reductions,
                        "SPEECH_TERMINAL_REDUCTION_NOT_APPLIED");
                current = terminalReduction.State;
            }

            ExecutionCoordinatorStepState finalStep =
                current != null ? current.FindStep(request.StepId) : null;
            return Success(
                request, runtimeFact, observationId,
                observedAtUtc, interactionStateAtObservation,
                before,
                finalStep != null
                    ? (ExecutionCoordinatorStepLifecycle?)
                        finalStep.Lifecycle : null,
                finalStep != null &&
                    finalStep.CompletionPolicySatisfied,
                SpeechShadowRunStatus.Completed,
                string.Empty, adapterResult, events, reductions);
        }

        public void ClearCorrelationHistory()
        {
            if (correlation != null) correlation.Clear();
        }

        private static SpeechShadowExecutionResult ReductionFailure(
            SpeechExecutionRequest request,
            SpeechPlaybackRuntimeFact fact,
            string observationId,
            DateTime observedAtUtc,
            InteractionState interactionState,
            ExecutionCoordinatorStepLifecycle? before,
            SpeechRuntimeAdapterResult adapterResult,
            List<ExecutionCoordinatorEvent> events,
            List<ExecutionCoordinatorReductionResult> reductions,
            string failureReason)
        {
            ExecutionCoordinatorRuntimeState last =
                reductions.Count > 0 &&
                reductions[reductions.Count - 1] != null
                    ? reductions[reductions.Count - 1].State : null;
            ExecutionCoordinatorStepState step = last != null &&
                    request != null
                ? last.FindStep(request.StepId) : null;
            return Success(
                request, fact, observationId, observedAtUtc,
                interactionState, before,
                step != null
                    ? (ExecutionCoordinatorStepLifecycle?)step.Lifecycle
                    : before,
                step != null && step.CompletionPolicySatisfied,
                SpeechShadowRunStatus.ReductionRejected,
                failureReason, adapterResult, events, reductions);
        }

        private static SpeechShadowExecutionResult Success(
            SpeechExecutionRequest request,
            SpeechPlaybackRuntimeFact fact,
            string observationId,
            DateTime observedAtUtc,
            InteractionState interactionState,
            ExecutionCoordinatorStepLifecycle? before,
            ExecutionCoordinatorStepLifecycle? after,
            bool completionSatisfied,
            SpeechShadowRunStatus status,
            string failureReason,
            SpeechRuntimeAdapterResult adapterResult,
            IEnumerable<ExecutionCoordinatorEvent> events,
            IEnumerable<ExecutionCoordinatorReductionResult>
                reductions)
        {
            var eventList = new List<ExecutionCoordinatorEvent>(events);
            var reductionList =
                new List<ExecutionCoordinatorReductionResult>(reductions);
            var effectTypes =
                new List<ExecutionCoordinatorEffectIntentType>();
            for (int i = 0; i < reductionList.Count; i++)
            {
                ExecutionCoordinatorReductionResult reduction =
                    reductionList[i];
                if (reduction == null) continue;
                for (int j = 0; j < reduction.EffectIntents.Count; j++)
                    effectTypes.Add(
                        reduction.EffectIntents[j].IntentType);
            }

            ExecutionCoordinatorEventType eventType =
                eventList.Count > 0
                    ? eventList[0].EventType
                    : ExecutionCoordinatorEventType.None;
            SpeechShadowObservation observation =
                new SpeechShadowObservation(
                    observationId,
                    request != null ? request.PlanId : string.Empty,
                    request != null ? request.StepId : string.Empty,
                    request != null
                        ? request.ExecutionAttemptId : string.Empty,
                    request != null ? request.RequestId : string.Empty,
                    fact != null
                        ? fact.RuntimeSpeechId : string.Empty,
                    fact != null ? fact.RuntimeGeneration : 0,
                    fact != null
                        ? fact.Lifecycle
                        : SpeechAdapterLifecycle.RequestAccepted,
                    interactionState, eventType, before, after,
                    completionSatisfied, effectTypes,
                    failureReason, observedAtUtc, status);
            return new SpeechShadowExecutionResult(
                observation, adapterResult, eventList,
                reductionList, false);
        }

        private static SpeechShadowExecutionResult Failure(
            SpeechExecutionRequest request,
            SpeechPlaybackRuntimeFact fact,
            string observationId,
            DateTime observedAtUtc,
            InteractionState interactionState,
            ExecutionCoordinatorStepLifecycle? before,
            SpeechShadowRunStatus status,
            string failureReason,
            SpeechRuntimeAdapterResult adapterResult)
        {
            return Success(
                request, fact, observationId, observedAtUtc,
                interactionState, before, before, false,
                status, failureReason, adapterResult,
                new ExecutionCoordinatorEvent[0],
                new ExecutionCoordinatorReductionResult[0]);
        }

        private static string ValidateEventIds(
            SpeechAdapterLifecycle lifecycle,
            SpeechShadowEventIds eventIds)
        {
            if (eventIds == null ||
                string.IsNullOrWhiteSpace(eventIds.LifecycleEventId))
                return "SPEECH_LIFECYCLE_EVENT_ID_MISSING";
            if (lifecycle == SpeechAdapterLifecycle.PlaybackCompleted &&
                (string.IsNullOrWhiteSpace(
                    eventIds.CompletionPolicyEventId) ||
                 string.IsNullOrWhiteSpace(eventIds.TerminalEventId)))
                return "SPEECH_COMPLETION_EVENT_IDS_MISSING";
            return string.Empty;
        }

        private static ExecutionCoordinatorEvent Event(
            string eventId,
            ExecutionCoordinatorEventType eventType,
            SpeechExecutionRequest request,
            DateTime occurredAtUtc,
            string source,
            int generation,
            ExecutionDomainStage domainStage,
            ExecutionTerminalOutcome outcome)
        {
            return new ExecutionCoordinatorEvent(
                eventId, eventType,
                request.PlanId, request.StepId,
                request.ExecutionAttemptId, string.Empty,
                occurredAtUtc, source, generation,
                domainStage, outcome,
                new string[0], string.Empty);
        }

        private static bool Applied(
            ExecutionCoordinatorReductionResult reduction)
        {
            return reduction != null &&
                reduction.EventDisposition ==
                    ExecutionCoordinatorEventDisposition.Applied;
        }

        private static ExecutionCoordinatorStepLifecycle? StepLifecycle(
            ExecutionCoordinatorRuntimeState state,
            string stepId)
        {
            if (state == null || string.IsNullOrEmpty(stepId))
                return null;
            ExecutionCoordinatorStepState step = state.FindStep(stepId);
            return step != null
                ? (ExecutionCoordinatorStepLifecycle?)step.Lifecycle
                : null;
        }

        private static string FirstFailure(
            IReadOnlyList<string> failures)
        {
            return failures != null && failures.Count > 0
                ? failures[0]
                : "SPEECH_EVENT_TRANSLATION_FAILED";
        }
    }
}
