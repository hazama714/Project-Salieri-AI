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
using SalieriAI.Core.Runtime;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>
    /// Observation-only legacy safety bridge. It can simulate the pure
    /// reducer, but never dispatches generated intents or safety commands.
    /// </summary>
    public sealed class ExecutionSafetyShadowDriver
    {
        public const string DriverVersion = "execution-safety-shadow-driver-3c6.1";
        private readonly ExecutionSafetyRuntimeAdapter adapter;
        private readonly SafetyRuntimeCorrelationTracker correlation;
        public int TrackedRuntimeCount => correlation != null
            ? correlation.TrackedRuntimeCount : 0;

        public ExecutionSafetyShadowDriver(
            ExecutionSafetyRuntimeAdapter runtimeAdapter,
            SafetyRuntimeCorrelationTracker correlationTracker)
        {
            adapter = runtimeAdapter;
            correlation = correlationTracker;
        }

        public SafetyShadowExecutionResult Observe(
            SafetyControlRequest request, SafetyControlRuntimeFact fact,
            string eventId, string observationId, string eventSource,
            int generation, InteractionState interactionState,
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorRuntimeState state,
            ExecutionCoordinatorReducerContext reducerContext)
        {
            ExecutionCoordinatorPlanLifecycle? before = state != null
                ? (ExecutionCoordinatorPlanLifecycle?)state.PlanLifecycle : null;
            if (adapter == null || correlation == null)
                return Failure(request, fact, observationId,
                    interactionState, before,
                    SafetyShadowRunStatus.RuntimeAdapterFailed,
                    "SAFETY_SHADOW_DEPENDENCY_UNAVAILABLE", null);

            SafetyRuntimeAdapterResult adapted = adapter.Observe(request, fact);
            if (adapted.Status != SafetyRuntimeAdapterStatus.Observed)
                return Failure(request, fact, observationId,
                    interactionState, before,
                    SafetyShadowRunStatus.RuntimeAdapterFailed,
                    adapted.FailureReason, adapted);

            SafetyRuntimeCorrelationResult correlated =
                correlation.Observe(request, fact);
            if (correlated.Status != SafetyRuntimeCorrelationStatus.Accepted)
            {
                SafetyShadowRunStatus status = correlated.Status ==
                    SafetyRuntimeCorrelationStatus.Duplicate
                        ? SafetyShadowRunStatus.Duplicate
                        : correlated.Status == SafetyRuntimeCorrelationStatus.Stale
                            ? SafetyShadowRunStatus.Stale
                            : SafetyShadowRunStatus.CorrelationFailed;
                return Failure(request, fact, observationId,
                    interactionState, before, status,
                    correlated.FailureReason, adapted);
            }

            if (!adapted.CanTranslateToCoordinatorEvent)
                return Success(request, fact, observationId,
                    interactionState, before, before,
                    ExecutionCoordinatorEventType.None,
                    new ExecutionCoordinatorEvent[0],
                    new ExecutionCoordinatorReductionResult[0],
                    adapted, SafetyShadowRunStatus.ObservationOnly, "");

            if (string.IsNullOrWhiteSpace(eventId))
                return Failure(request, fact, observationId,
                    interactionState, before,
                    SafetyShadowRunStatus.EventTranslationFailed,
                    "SAFETY_EVENT_ID_MISSING", adapted);

            AdapterTranslationResult<ExecutionCoordinatorEvent> translated =
                ExecutionRuntimeAdapterTranslator.ToSafetyEvent(
                    request, adapted.ControlResult, eventId,
                    generation, eventSource);
            if (!translated.IsValid)
                return Failure(request, fact, observationId,
                    interactionState, before,
                    SafetyShadowRunStatus.EventTranslationFailed,
                    First(translated.FailureCodes), adapted);

            var events = new List<ExecutionCoordinatorEvent> { translated.Value };
            var reductions = new List<ExecutionCoordinatorReductionResult>();
            if (plan == null || state == null || reducerContext == null)
                return Success(request, fact, observationId,
                    interactionState, before, before,
                    translated.Value.EventType, events, reductions,
                    adapted, SafetyShadowRunStatus.ObservationOnly, "");

            ExecutionCoordinatorReductionResult reduction =
                ExecutionCoordinatorReducer.Reduce(
                    plan, state, translated.Value, reducerContext);
            reductions.Add(reduction);
            if (reduction == null || reduction.EventDisposition !=
                ExecutionCoordinatorEventDisposition.Applied)
                return Success(request, fact, observationId,
                    interactionState, before,
                    reduction != null && reduction.State != null
                        ? (ExecutionCoordinatorPlanLifecycle?)reduction.State.PlanLifecycle
                        : before,
                    translated.Value.EventType, events, reductions,
                    adapted, SafetyShadowRunStatus.ReductionRejected,
                    "SAFETY_REDUCTION_NOT_APPLIED");

            return Success(request, fact, observationId,
                interactionState, before,
                reduction.State != null
                    ? (ExecutionCoordinatorPlanLifecycle?)reduction.State.PlanLifecycle
                    : before,
                translated.Value.EventType, events, reductions,
                adapted, SafetyShadowRunStatus.Completed, "");
        }

        public void ClearCorrelationHistory()
        {
            if (correlation != null) correlation.Clear();
        }

        private static SafetyShadowExecutionResult Failure(
            SafetyControlRequest request, SafetyControlRuntimeFact fact,
            string observationId, InteractionState interactionState,
            ExecutionCoordinatorPlanLifecycle? before,
            SafetyShadowRunStatus status, string reason,
            SafetyRuntimeAdapterResult adapted)
        {
            return Success(request, fact, observationId, interactionState,
                before, before, ExecutionCoordinatorEventType.None,
                new ExecutionCoordinatorEvent[0],
                new ExecutionCoordinatorReductionResult[0],
                adapted, status, reason);
        }

        private static SafetyShadowExecutionResult Success(
            SafetyControlRequest request, SafetyControlRuntimeFact fact,
            string observationId, InteractionState interactionState,
            ExecutionCoordinatorPlanLifecycle? before,
            ExecutionCoordinatorPlanLifecycle? after,
            ExecutionCoordinatorEventType eventType,
            IEnumerable<ExecutionCoordinatorEvent> events,
            IEnumerable<ExecutionCoordinatorReductionResult> reductions,
            SafetyRuntimeAdapterResult adapted,
            SafetyShadowRunStatus status, string reason)
        {
            var effects = new List<ExecutionCoordinatorEffectIntentType>();
            if (reductions != null)
            {
                foreach (ExecutionCoordinatorReductionResult reduction in reductions)
                {
                    if (reduction == null) continue;
                    for (int i = 0; i < reduction.EffectIntents.Count; i++)
                        effects.Add(reduction.EffectIntents[i].IntentType);
                }
            }
            var observation = new SafetyShadowObservation(
                observationId, request, fact, adapted, interactionState,
                before, after, eventType, effects, reason, status);
            return new SafetyShadowExecutionResult(
                observation, adapted, events, reductions);
        }

        private static string First(IReadOnlyList<string> values)
        {
            return values != null && values.Count > 0
                ? values[0] : "SAFETY_TRANSLATION_FAILED";
        }
    }
}
