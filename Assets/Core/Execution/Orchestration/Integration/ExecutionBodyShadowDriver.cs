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
    /// Observation-only bridge from legacy body facts to the existing
    /// coordinator contract. It never starts body work or dispatches effects.
    /// </summary>
    public sealed class ExecutionBodyShadowDriver
    {
        public const string DriverVersion =
            "execution-body-shadow-driver-3c5.1";
        private readonly ExecutionBodyRuntimeAdapter adapter;
        private readonly BodyRuntimeCorrelationTracker correlation;

        public int TrackedRuntimeCount => correlation != null
            ? correlation.TrackedRuntimeCount : 0;

        public ExecutionBodyShadowDriver(
            ExecutionBodyRuntimeAdapter runtimeAdapter,
            BodyRuntimeCorrelationTracker correlationTracker)
        {
            adapter = runtimeAdapter;
            correlation = correlationTracker;
        }

        public BodyShadowExecutionResult Observe(
            BodyExecutionRequest request,
            BodyExecutionRuntimeFact fact,
            BodyShadowEventIds eventIds,
            string observationId,
            string eventSource,
            int generation,
            InteractionState interactionState,
            BodyPermissionSnapshot permissionSnapshot,
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorRuntimeState state,
            ExecutionCoordinatorReducerContext reducerContext)
        {
            ExecutionCoordinatorStepLifecycle? before =
                StepLifecycle(state,
                    request != null ? request.StepId : "");
            if (adapter == null || correlation == null)
                return Failure(request, fact, observationId,
                    interactionState, permissionSnapshot, before,
                    BodyShadowRunStatus.RuntimeAdapterFailed,
                    "BODY_SHADOW_DEPENDENCY_UNAVAILABLE", null);

            BodyRuntimeAdapterResult adapterResult =
                adapter.Observe(request, fact);
            if (adapterResult.Status !=
                    BodyRuntimeAdapterStatus.Observed ||
                adapterResult.LifecycleResult == null)
                return Failure(request, fact, observationId,
                    interactionState, permissionSnapshot, before,
                    BodyShadowRunStatus.RuntimeAdapterFailed,
                    adapterResult.FailureReason, adapterResult);

            BodyRuntimeCorrelationResult correlationResult =
                correlation.Observe(request, fact);
            if (correlationResult.Status !=
                BodyRuntimeCorrelationStatus.Accepted)
            {
                BodyShadowRunStatus status =
                    correlationResult.Status ==
                        BodyRuntimeCorrelationStatus.Duplicate
                        ? BodyShadowRunStatus.Duplicate
                        : correlationResult.Status ==
                            BodyRuntimeCorrelationStatus.Stale
                            ? BodyShadowRunStatus.Stale
                            : BodyShadowRunStatus.CorrelationFailed;
                return Failure(request, fact, observationId,
                    interactionState, permissionSnapshot, before,
                    status, correlationResult.FailureReason,
                    adapterResult);
            }

            string idFailure = ValidateEventIds(
                fact.Lifecycle, eventIds);
            if (idFailure.Length > 0)
                return Failure(request, fact, observationId,
                    interactionState, permissionSnapshot, before,
                    BodyShadowRunStatus.EventTranslationFailed,
                    idFailure, adapterResult);

            AdapterTranslationResult<ExecutionCoordinatorEvent>
                translation = ExecutionRuntimeAdapterTranslator
                    .ToBodyEvent(request,
                        adapterResult.LifecycleResult,
                        eventIds.LifecycleEventId,
                        generation, eventSource);
            if (!translation.IsValid)
                return Failure(request, fact, observationId,
                    interactionState, permissionSnapshot, before,
                    BodyShadowRunStatus.EventTranslationFailed,
                    FirstFailure(translation.FailureCodes),
                    adapterResult);

            ExecutionCoordinatorEvent primary = translation.Value;
            var events = new List<ExecutionCoordinatorEvent>();
            var reductions =
                new List<ExecutionCoordinatorReductionResult>();

            if (plan == null || state == null || reducerContext == null)
            {
                events.Add(primary);
                return Success(request, fact, observationId,
                    interactionState, permissionSnapshot, before, before,
                    false, BodyShadowRunStatus.ObservationOnly, "",
                    adapterResult, primary.EventType, events, reductions);
            }

            ExecutionCoordinatorRuntimeState current = state;

            // An explicit unverified terminal needs completion evidence that
            // was established by a prior transport-written observation.
            if (fact.Lifecycle ==
                BodyRuntimeLifecycle.PhysicalCompletionUnverified)
            {
                ExecutionCoordinatorEvent completion = Event(
                    eventIds.CompletionPolicyEventId,
                    ExecutionCoordinatorEventType.CompletionPolicyReached,
                    request, fact.OccurredAtUtc, eventSource,
                    generation, null, ExecutionTerminalOutcome.None);
                if (!Apply(plan, ref current, completion,
                        reducerContext, events, reductions))
                    return ReductionFailure(request, fact,
                        observationId, interactionState,
                        permissionSnapshot, before, adapterResult,
                        primary.EventType, events, reductions,
                        "BODY_COMPLETION_POLICY_NOT_APPLIED");
            }

            if (!Apply(plan, ref current, primary, reducerContext,
                    events, reductions))
                return ReductionFailure(request, fact, observationId,
                    interactionState, permissionSnapshot, before,
                    adapterResult, primary.EventType, events, reductions,
                    "BODY_PRIMARY_REDUCTION_NOT_APPLIED");

            bool completeUnverified =
                ((fact.Lifecycle ==
                    BodyRuntimeLifecycle.PhysicalTransportWritten &&
                  AllowsUnverifiedCompletion(plan, request.StepId)) ||
                 (fact.Lifecycle ==
                    BodyRuntimeLifecycle.PhysicalDispatchRequested &&
                  IsLookAroundDispatchOnlyCompletion(
                      plan, request.StepId)));
            bool completeVerified = fact.Lifecycle ==
                BodyRuntimeLifecycle.PhysicalCompletionVerified;

            if (completeUnverified || completeVerified)
            {
                ExecutionCoordinatorEvent completion = Event(
                    eventIds.CompletionPolicyEventId,
                    ExecutionCoordinatorEventType.CompletionPolicyReached,
                    request, fact.OccurredAtUtc, eventSource,
                    generation, null, ExecutionTerminalOutcome.None);
                if (!Apply(plan, ref current, completion,
                        reducerContext, events, reductions))
                    return ReductionFailure(request, fact,
                        observationId, interactionState,
                        permissionSnapshot, before, adapterResult,
                        primary.EventType, events, reductions,
                        "BODY_COMPLETION_POLICY_NOT_APPLIED");

                ExecutionCoordinatorEvent terminal = Event(
                    eventIds.TerminalEventId,
                    ExecutionCoordinatorEventType.ExecutorTerminal,
                    request, fact.OccurredAtUtc, eventSource,
                    generation, null,
                    completeVerified
                        ? ExecutionTerminalOutcome.Succeeded
                        : ExecutionTerminalOutcome.SucceededUnverified);
                if (!Apply(plan, ref current, terminal,
                        reducerContext, events, reductions))
                    return ReductionFailure(request, fact,
                        observationId, interactionState,
                        permissionSnapshot, before, adapterResult,
                        primary.EventType, events, reductions,
                        "BODY_TERMINAL_REDUCTION_NOT_APPLIED");
            }

            ExecutionCoordinatorStepState finalStep =
                current.FindStep(request.StepId);
            return Success(request, fact, observationId,
                interactionState, permissionSnapshot, before,
                finalStep != null
                    ? (ExecutionCoordinatorStepLifecycle?)
                        finalStep.Lifecycle : null,
                finalStep != null &&
                    finalStep.CompletionPolicySatisfied,
                BodyShadowRunStatus.Completed, "", adapterResult,
                primary.EventType, events, reductions);
        }

        public void ClearCorrelationHistory()
        {
            if (correlation != null) correlation.Clear();
        }

        private static bool Apply(
            ValidatedExecutionPlan plan,
            ref ExecutionCoordinatorRuntimeState state,
            ExecutionCoordinatorEvent value,
            ExecutionCoordinatorReducerContext context,
            List<ExecutionCoordinatorEvent> events,
            List<ExecutionCoordinatorReductionResult> reductions)
        {
            events.Add(value);
            ExecutionCoordinatorReductionResult reduction =
                ExecutionCoordinatorReducer.Reduce(
                    plan, state, value, context);
            reductions.Add(reduction);
            if (reduction == null ||
                reduction.EventDisposition !=
                    ExecutionCoordinatorEventDisposition.Applied)
                return false;
            state = reduction.State;
            return true;
        }

        private static bool AllowsUnverifiedCompletion(
            ValidatedExecutionPlan plan, string stepId)
        {
            if (plan == null || plan.SourcePlan == null)
                return false;
            for (int i = 0; i < plan.SourcePlan.Steps.Count; i++)
            {
                ReactionExecutionStep step = plan.SourcePlan.Steps[i];
                if (step != null && step.StepId == stepId &&
                    step.CompletionPolicy != null)
                    return step.CompletionPolicy
                            .AllowsUnverifiedCompletion ||
                        step.CompletionPolicy.Kind ==
                            ExecutionCompletionKind
                                .PhysicalCompletionUnverifiedAllowed;
            }
            return false;
        }

        private static bool IsLookAroundDispatchOnlyCompletion(
            ValidatedExecutionPlan plan,
            string stepId)
        {
            if (plan == null || plan.SourcePlan == null)
                return false;
            for (int i = 0; i < plan.SourcePlan.Steps.Count; i++)
            {
                ReactionExecutionStep step = plan.SourcePlan.Steps[i];
                if (step != null && step.StepId == stepId)
                {
                    return ExecutionLookAroundContract
                        .IsLookAroundStep(step) &&
                        ExecutionLookAroundContract
                            .MatchesCompletionPolicy(
                                step.CompletionPolicy) &&
                        ExecutionLookAroundContract
                            .CreateDelegatedBodyActionContract()
                            .AllowsUnverifiedCompletion;
                }
            }
            return false;
        }

        private static BodyShadowExecutionResult ReductionFailure(
            BodyExecutionRequest request,
            BodyExecutionRuntimeFact fact,
            string observationId,
            InteractionState interactionState,
            BodyPermissionSnapshot permissionSnapshot,
            ExecutionCoordinatorStepLifecycle? before,
            BodyRuntimeAdapterResult adapterResult,
            ExecutionCoordinatorEventType primaryType,
            List<ExecutionCoordinatorEvent> events,
            List<ExecutionCoordinatorReductionResult> reductions,
            string reason)
        {
            ExecutionCoordinatorRuntimeState last =
                reductions.Count > 0 &&
                reductions[reductions.Count - 1] != null
                    ? reductions[reductions.Count - 1].State : null;
            ExecutionCoordinatorStepState step = last != null &&
                    request != null
                ? last.FindStep(request.StepId) : null;
            return Success(request, fact, observationId,
                interactionState, permissionSnapshot, before,
                step != null
                    ? (ExecutionCoordinatorStepLifecycle?)step.Lifecycle
                    : before,
                step != null && step.CompletionPolicySatisfied,
                BodyShadowRunStatus.ReductionRejected, reason,
                adapterResult, primaryType, events, reductions);
        }

        private static BodyShadowExecutionResult Failure(
            BodyExecutionRequest request,
            BodyExecutionRuntimeFact fact,
            string observationId,
            InteractionState interactionState,
            BodyPermissionSnapshot permissionSnapshot,
            ExecutionCoordinatorStepLifecycle? before,
            BodyShadowRunStatus status,
            string reason,
            BodyRuntimeAdapterResult adapterResult)
        {
            return Success(request, fact, observationId,
                interactionState, permissionSnapshot, before, before,
                false, status, reason, adapterResult,
                ExecutionCoordinatorEventType.None,
                new ExecutionCoordinatorEvent[0],
                new ExecutionCoordinatorReductionResult[0]);
        }

        private static BodyShadowExecutionResult Success(
            BodyExecutionRequest request,
            BodyExecutionRuntimeFact fact,
            string observationId,
            InteractionState interactionState,
            BodyPermissionSnapshot permissionSnapshot,
            ExecutionCoordinatorStepLifecycle? before,
            ExecutionCoordinatorStepLifecycle? after,
            bool completionSatisfied,
            BodyShadowRunStatus status,
            string reason,
            BodyRuntimeAdapterResult adapterResult,
            ExecutionCoordinatorEventType primaryType,
            IEnumerable<ExecutionCoordinatorEvent> events,
            IEnumerable<ExecutionCoordinatorReductionResult> reductions)
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

            BodyShadowObservation observation =
                new BodyShadowObservation(
                    observationId,
                    request != null ? request.PlanId : "",
                    request != null ? request.StepId : "",
                    request != null ? request.ExecutionAttemptId : "",
                    request != null ? request.RequestId : "",
                    fact != null ? fact.ActionId : "",
                    fact != null ? fact.RuntimeExecutionToken : "",
                    fact != null ? fact.RuntimeGeneration : 0,
                    fact != null ? fact.Lifecycle :
                        BodyRuntimeLifecycle.RequestAccepted,
                    adapterResult != null &&
                        adapterResult.LifecycleResult != null
                        ? adapterResult.LifecycleResult.DomainStage : null,
                    adapterResult != null &&
                        adapterResult.LifecycleResult != null
                        ? adapterResult.LifecycleResult.TerminalOutcome
                        : ExecutionTerminalOutcome.None,
                    interactionState, permissionSnapshot,
                    fact != null ? fact.VerificationLevel :
                        BodyPhysicalVerificationLevel.None,
                    primaryType, before, after, completionSatisfied,
                    effectTypes, reason,
                    fact != null ? fact.Diagnostic : "",
                    fact != null ? fact.OccurredAtUtc : default(DateTime),
                    status);
            return new BodyShadowExecutionResult(
                observation, adapterResult, eventList,
                reductionList, false);
        }

        private static string ValidateEventIds(
            BodyRuntimeLifecycle lifecycle,
            BodyShadowEventIds eventIds)
        {
            if (eventIds == null || string.IsNullOrWhiteSpace(
                    eventIds.LifecycleEventId))
                return "BODY_LIFECYCLE_EVENT_ID_MISSING";
            bool completion =
                lifecycle == BodyRuntimeLifecycle
                    .PhysicalTransportWritten ||
                lifecycle == BodyRuntimeLifecycle
                    .PhysicalCompletionUnverified ||
                lifecycle == BodyRuntimeLifecycle
                    .PhysicalCompletionVerified;
            if (completion &&
                (string.IsNullOrWhiteSpace(
                    eventIds.CompletionPolicyEventId) ||
                 string.IsNullOrWhiteSpace(eventIds.TerminalEventId)))
                return "BODY_COMPLETION_EVENT_IDS_MISSING";
            return "";
        }

        private static ExecutionCoordinatorEvent Event(
            string eventId,
            ExecutionCoordinatorEventType type,
            BodyExecutionRequest request,
            DateTime occurredAtUtc,
            string source,
            int generation,
            ExecutionDomainStage stage,
            ExecutionTerminalOutcome outcome)
        {
            return new ExecutionCoordinatorEvent(
                eventId, type, request.PlanId, request.StepId,
                request.ExecutionAttemptId, "", occurredAtUtc,
                source, generation, stage, outcome,
                new string[0], "");
        }

        private static ExecutionCoordinatorStepLifecycle? StepLifecycle(
            ExecutionCoordinatorRuntimeState state, string stepId)
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
                ? failures[0] : "BODY_EVENT_TRANSLATION_FAILED";
        }
    }
}
