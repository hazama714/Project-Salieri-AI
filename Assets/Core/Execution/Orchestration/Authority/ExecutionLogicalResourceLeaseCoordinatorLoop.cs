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
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Resources;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.Execution.Orchestration.Validation;

namespace SalieriAI.Core.Execution.Orchestration.Authority
{
    public enum ExecutionLogicalLeaseLoopStopReason
    {
        None = 0,
        InvalidInput = 1,
        NoEffectIntent = 2,
        NoLiveLogicalLeaseEffect = 3,
        AuthorityBlocked = 4,
        ServiceProducedNoEvent = 5,
        TerminalState = 6,
        ExplicitInputExhausted = 7,
        MaximumPumpCountReached = 8,
        DuplicateEventInput = 9,
        AvailabilityUnknown = 10,
        AvailabilityFailed = 11,
        AvailabilityCorrelationFailed = 12
    }

    /// <summary>
    /// All changing values required by one loop iteration are explicit.
    /// The loop never creates an event ID, timestamp, or reducer context.
    /// </summary>
    public sealed class ExecutionLogicalLeaseLoopCycleInput
    {
        public string EventId { get; }
        public string EventSource { get; }
        public string RequestId { get; }
        public string LeaseId { get; }
        public ExecutionAuthorityEvaluationContext AuthorityContext { get; }
        public ExecutionCoordinatorReducerContext ReducerContext { get; }
        public ResourceShadowAvailabilityStatus Availability { get; }
        public bool StepIsTerminal { get; }
        public bool ReleaseConfirmed { get; }
        public string ReleaseFailureReason { get; }

        public ExecutionLogicalLeaseLoopCycleInput(
            string eventId,
            string eventSource,
            string requestId,
            string leaseId,
            ExecutionAuthorityEvaluationContext authorityContext,
            ExecutionCoordinatorReducerContext reducerContext,
            ResourceShadowAvailabilityStatus availability,
            bool stepIsTerminal,
            bool releaseConfirmed,
            string releaseFailureReason)
        {
            EventId = eventId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            LeaseId = leaseId ?? string.Empty;
            AuthorityContext = authorityContext;
            ReducerContext = reducerContext;
            Availability = availability;
            StepIsTerminal = stepIsTerminal;
            ReleaseConfirmed = releaseConfirmed;
            ReleaseFailureReason = releaseFailureReason ?? string.Empty;
        }
    }

    public sealed class ExecutionLogicalLeaseLoopTrace
    {
        public int PumpIndex { get; }
        public ExecutionCoordinatorRuntimeState PreviousRuntimeState { get; }
        public ExecutionCoordinatorEffectIntent EffectIntent { get; }
        public ExecutionLogicalLeaseDispatchResult DispatchResult { get; }
        public ExecutionCoordinatorEvent GeneratedEvent { get; }
        public ExecutionCoordinatorReductionResult ReappliedReduction { get; }
        public ExecutionResourceAvailabilityLeaseResult
            AvailabilityCorrelation { get; }

        public ExecutionLogicalLeaseLoopTrace(
            int pumpIndex,
            ExecutionCoordinatorRuntimeState previousRuntimeState,
            ExecutionCoordinatorEffectIntent effectIntent,
            ExecutionLogicalLeaseDispatchResult dispatchResult,
            ExecutionCoordinatorEvent generatedEvent,
            ExecutionCoordinatorReductionResult reappliedReduction)
            : this(pumpIndex, previousRuntimeState, effectIntent,
                dispatchResult, generatedEvent, reappliedReduction, null)
        {
        }

        public ExecutionLogicalLeaseLoopTrace(
            int pumpIndex,
            ExecutionCoordinatorRuntimeState previousRuntimeState,
            ExecutionCoordinatorEffectIntent effectIntent,
            ExecutionLogicalLeaseDispatchResult dispatchResult,
            ExecutionCoordinatorEvent generatedEvent,
            ExecutionCoordinatorReductionResult reappliedReduction,
            ExecutionResourceAvailabilityLeaseResult
                availabilityCorrelation)
        {
            PumpIndex = pumpIndex;
            PreviousRuntimeState = previousRuntimeState;
            EffectIntent = effectIntent;
            DispatchResult = dispatchResult;
            GeneratedEvent = generatedEvent;
            ReappliedReduction = reappliedReduction;
            AvailabilityCorrelation = availabilityCorrelation;
        }
    }

    public sealed class ExecutionLogicalLeaseLoopResult
    {
        public ExecutionCoordinatorReductionResult FinalReduction { get; }
        public LogicalResourceLeaseState FinalLeaseState { get; }
        public IReadOnlyList<ExecutionLogicalLeaseLoopTrace> Traces { get; }
        public ExecutionLogicalLeaseLoopStopReason StopReason { get; }
        public int PumpCount { get; }

        public ExecutionLogicalLeaseLoopResult(
            ExecutionCoordinatorReductionResult finalReduction,
            LogicalResourceLeaseState finalLeaseState,
            IEnumerable<ExecutionLogicalLeaseLoopTrace> traces,
            ExecutionLogicalLeaseLoopStopReason stopReason,
            int pumpCount)
        {
            FinalReduction = finalReduction;
            FinalLeaseState = finalLeaseState;
            Traces = new ReadOnlyCollection<ExecutionLogicalLeaseLoopTrace>(
                traces != null
                    ? new List<ExecutionLogicalLeaseLoopTrace>(traces)
                    : new List<ExecutionLogicalLeaseLoopTrace>());
            StopReason = stopReason;
            PumpCount = pumpCount;
        }
    }

    /// <summary>
    /// Phase 3D-2 integration around the existing pure reducer. It pumps
    /// only logical lease acquire/release effects through Phase 3D-1 and
    /// feeds only the resulting service fact back to the reducer.
    /// </summary>
    public sealed class ExecutionLogicalResourceLeaseCoordinatorLoop
    {
        public const string LoopVersion =
            "execution-logical-lease-coordinator-loop-3d4b.0";

        private readonly ExecutionLogicalResourceLeaseLiveDispatcher
            dispatcher =
                new ExecutionLogicalResourceLeaseLiveDispatcher();

        public ExecutionLogicalLeaseLoopResult Pump(
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorReductionResult initialReduction,
            LogicalResourceLeaseState initialLeaseState,
            ExecutionAuthorityPolicySnapshot policy,
            ExecutionRuntimeCapabilitySnapshot capability,
            IEnumerable<ExecutionLogicalLeaseLoopCycleInput> cycleInputs,
            int maximumPumpCount)
        {
            return PumpCore(plan, initialReduction, initialLeaseState,
                policy, capability, cycleInputs, maximumPumpCount, null);
        }

        /// <summary>
        /// Phase 3D-4B acquire path. The read-only adapter emits a formal
        /// availability event first. Only an Acquire intent returned by the
        /// reducer after Available may reach the logical lease dispatcher.
        /// Release remains the Phase 3D-2 path.
        /// </summary>
        public ExecutionLogicalLeaseLoopResult PumpWithAvailability(
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorReductionResult initialReduction,
            LogicalResourceLeaseState initialLeaseState,
            ExecutionAuthorityPolicySnapshot policy,
            ExecutionRuntimeCapabilitySnapshot capability,
            IEnumerable<ExecutionLogicalLeaseLoopCycleInput> cycleInputs,
            int maximumPumpCount,
            ExecutionResourceRuntimeAdapter resourceAdapter)
        {
            return PumpCore(plan, initialReduction, initialLeaseState,
                policy, capability, cycleInputs, maximumPumpCount,
                new ExecutionResourceAvailabilityLeaseCorrelation(
                    resourceAdapter));
        }

        private ExecutionLogicalLeaseLoopResult PumpCore(
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorReductionResult initialReduction,
            LogicalResourceLeaseState initialLeaseState,
            ExecutionAuthorityPolicySnapshot policy,
            ExecutionRuntimeCapabilitySnapshot capability,
            IEnumerable<ExecutionLogicalLeaseLoopCycleInput> cycleInputs,
            int maximumPumpCount,
            ExecutionResourceAvailabilityLeaseCorrelation correlation)
        {
            var traces = new List<ExecutionLogicalLeaseLoopTrace>();
            List<ExecutionLogicalLeaseLoopCycleInput> inputs = cycleInputs != null
                ? new List<ExecutionLogicalLeaseLoopCycleInput>(cycleInputs)
                : new List<ExecutionLogicalLeaseLoopCycleInput>();

            if (plan == null || plan.SourcePlan == null ||
                initialReduction == null || initialReduction.State == null ||
                initialLeaseState == null || policy == null ||
                capability == null || maximumPumpCount <= 0)
                return Result(initialReduction, initialLeaseState, traces,
                    ExecutionLogicalLeaseLoopStopReason.InvalidInput, 0);

            ExecutionCoordinatorReductionResult current = initialReduction;
            LogicalResourceLeaseState leaseState = initialLeaseState;
            var pending = new List<PendingLogicalLeaseEffect>();
            AddLiveLogicalLeaseEffects(pending,
                initialReduction.EffectIntents, false);
            int pumpCount = 0;
            int inputIndex = 0;

            while (true)
            {
                if (current.State.PlanLifecycle ==
                    ExecutionCoordinatorPlanLifecycle.Terminal)
                    return Result(current, leaseState, traces,
                        ExecutionLogicalLeaseLoopStopReason.TerminalState,
                        pumpCount);

                if (pending.Count == 0)
                    return Result(current, leaseState, traces,
                        current.EffectIntents.Count == 0
                            ? ExecutionLogicalLeaseLoopStopReason
                                .NoEffectIntent
                            : ExecutionLogicalLeaseLoopStopReason
                                .NoLiveLogicalLeaseEffect,
                        pumpCount);

                PendingLogicalLeaseEffect pendingEffect = pending[0];
                pending.RemoveAt(0);
                ExecutionCoordinatorEffectIntent intent =
                    pendingEffect.Intent;

                if (pumpCount >= maximumPumpCount)
                    return Result(current, leaseState, traces,
                        ExecutionLogicalLeaseLoopStopReason
                            .MaximumPumpCountReached,
                        pumpCount);

                if (inputIndex >= inputs.Count ||
                    !ValidCycleInput(inputs[inputIndex], current.State))
                    return Result(current, leaseState, traces,
                        ExecutionLogicalLeaseLoopStopReason
                            .ExplicitInputExhausted,
                        pumpCount);

                ExecutionLogicalLeaseLoopCycleInput cycle =
                    inputs[inputIndex++];
                if (Contains(current.State.ProcessedEventIds,
                        cycle.EventId))
                    return Result(current, leaseState, traces,
                        ExecutionLogicalLeaseLoopStopReason
                            .DuplicateEventInput,
                        pumpCount);
                ExecutionDomain stepDomain = FindStepDomain(
                    plan.SourcePlan, intent.StepId);
                ReactionExecutionStep stepDefinition = FindStep(
                    plan.SourcePlan, intent.StepId);
                ExecutionCoordinatorEffectIntent dispatchIntent =
                    CorrelateIntent(intent, cycle);
                ExecutionCoordinatorRuntimeState previousState =
                    current.State;
                ExecutionResourceAvailabilityLeaseResult
                    availabilityCorrelation = null;
                ResourceShadowAvailabilityStatus effectiveAvailability =
                    cycle.Availability;

                if (correlation != null && !pendingEffect.AvailabilityEvaluated &&
                    dispatchIntent != null &&
                    dispatchIntent.IntentType ==
                        ExecutionCoordinatorEffectIntentType
                            .AcquireResourceLease)
                {
                    availabilityCorrelation = correlation.Evaluate(
                        dispatchIntent, stepDomain, policy, capability,
                        cycle.AuthorityContext,
                        cycle.ReducerContext.NowUtc,
                        current.State.Generation, cycle.EventId,
                        cycle.EventSource,
                        stepDefinition != null
                            ? stepDefinition.RequestId : string.Empty,
                        previousState.FindStep(intent.StepId) != null
                            ? previousState.FindStep(intent.StepId)
                                .ResourceWaitCycleId
                            : string.Empty);

                    if (availabilityCorrelation == null ||
                        availabilityCorrelation.Disposition ==
                            ExecutionResourceAvailabilityLeaseDisposition
                                .Invalid)
                    {
                        pumpCount++;
                        traces.Add(new ExecutionLogicalLeaseLoopTrace(
                            pumpCount, previousState, intent, null, null,
                            null, availabilityCorrelation));
                        return Result(current, leaseState, traces,
                            ExecutionLogicalLeaseLoopStopReason
                                .AvailabilityCorrelationFailed,
                            pumpCount);
                    }

                    if (availabilityCorrelation.Disposition ==
                        ExecutionResourceAvailabilityLeaseDisposition
                            .AuthorityBlocked)
                    {
                        pumpCount++;
                        traces.Add(new ExecutionLogicalLeaseLoopTrace(
                            pumpCount, previousState, intent, null, null,
                            null, availabilityCorrelation));
                        return Result(current, leaseState, traces,
                            ExecutionLogicalLeaseLoopStopReason
                                .AuthorityBlocked,
                            pumpCount);
                    }

                    ExecutionCoordinatorEvent availabilityEvent =
                        availabilityCorrelation.CoordinatorEvent;
                    if (availabilityEvent == null)
                    {
                        pumpCount++;
                        traces.Add(new ExecutionLogicalLeaseLoopTrace(
                            pumpCount, previousState, intent, null, null,
                            null, availabilityCorrelation));
                        return Result(current, leaseState, traces,
                            ExecutionLogicalLeaseLoopStopReason
                                .AvailabilityCorrelationFailed,
                            pumpCount);
                    }

                    pumpCount++;
                    ExecutionCoordinatorReductionResult evaluated =
                        ExecutionCoordinatorReducer.Reduce(plan,
                            previousState, availabilityEvent,
                            cycle.ReducerContext);
                    traces.Add(new ExecutionLogicalLeaseLoopTrace(
                        pumpCount, previousState, intent, null,
                        availabilityEvent, evaluated,
                        availabilityCorrelation));
                    if (evaluated.EventDisposition !=
                        ExecutionCoordinatorEventDisposition.Applied)
                    {
                        return Result(evaluated, leaseState, traces,
                            ExecutionLogicalLeaseLoopStopReason
                                .AvailabilityCorrelationFailed,
                            pumpCount);
                    }

                    current = evaluated;
                    if (availabilityCorrelation.Disposition ==
                        ExecutionResourceAvailabilityLeaseDisposition
                            .Available)
                    {
                        AddAvailabilityEvaluatedEffectsFirst(pending,
                            evaluated.EffectIntents);
                    }
                    else if (inputIndex < inputs.Count)
                    {
                        pending.Add(new PendingLogicalLeaseEffect(
                            intent, false));
                    }

                    if (pending.Count == 0 &&
                        availabilityCorrelation.Disposition ==
                            ExecutionResourceAvailabilityLeaseDisposition
                                .Unknown)
                    {
                        return Result(current, leaseState, traces,
                            ExecutionLogicalLeaseLoopStopReason
                                .AvailabilityUnknown, pumpCount);
                    }
                    if (pending.Count == 0 &&
                        availabilityCorrelation.Disposition ==
                            ExecutionResourceAvailabilityLeaseDisposition
                                .Failed)
                    {
                        return Result(current, leaseState, traces,
                            ExecutionLogicalLeaseLoopStopReason
                                .AvailabilityFailed, pumpCount);
                    }
                    continue;
                }
                if (pendingEffect.AvailabilityEvaluated)
                    effectiveAvailability =
                        ResourceShadowAvailabilityStatus.Available;
                ExecutionLogicalLeaseDispatchResult dispatch =
                    dispatcher.Dispatch(dispatchIntent, stepDomain, policy,
                        capability, cycle.AuthorityContext, leaseState,
                        effectiveAvailability, cycle.StepIsTerminal,
                        cycle.ReleaseConfirmed,
                        cycle.ReleaseFailureReason,
                        current.State.Generation, cycle.EventId,
                        cycle.EventSource);
                pumpCount++;

                if (dispatch == null || dispatch.CoordinatorEvent == null)
                {
                    traces.Add(new ExecutionLogicalLeaseLoopTrace(
                        pumpCount, previousState, intent, dispatch, null,
                        null, availabilityCorrelation));
                    return Result(current,
                        dispatch != null && dispatch.State != null
                            ? dispatch.State : leaseState,
                        traces,
                        dispatch == null ||
                            dispatch.AuthorityDecision == null ||
                            !dispatch.AuthorityDecision.DispatchAllowed
                            ? ExecutionLogicalLeaseLoopStopReason
                                .AuthorityBlocked
                            : ExecutionLogicalLeaseLoopStopReason
                                .ServiceProducedNoEvent,
                        pumpCount);
                }

                ExecutionCoordinatorReductionResult reapplied =
                    ExecutionCoordinatorReducer.Reduce(plan,
                        previousState, dispatch.CoordinatorEvent,
                        cycle.ReducerContext);
                traces.Add(new ExecutionLogicalLeaseLoopTrace(
                    pumpCount, previousState, intent, dispatch,
                    dispatch.CoordinatorEvent, reapplied,
                    availabilityCorrelation));
                leaseState = dispatch.State;
                current = reapplied;
                AddLiveLogicalLeaseEffects(pending,
                    reapplied.EffectIntents, false);
            }
        }

        private static void AddLiveLogicalLeaseEffects(
            IList<PendingLogicalLeaseEffect> target,
            IReadOnlyList<ExecutionCoordinatorEffectIntent> source,
            bool availabilityEvaluated)
        {
            for (int i = 0; i < source.Count; i++)
            {
                ExecutionCoordinatorEffectIntent intent = source[i];
                if (intent != null &&
                    (intent.IntentType ==
                        ExecutionCoordinatorEffectIntentType
                            .AcquireResourceLease ||
                     intent.IntentType ==
                        ExecutionCoordinatorEffectIntentType
                            .ReleaseResourceLease))
                    target.Add(new PendingLogicalLeaseEffect(
                        intent, availabilityEvaluated));
            }
        }

        private static void AddAvailabilityEvaluatedEffectsFirst(
            IList<PendingLogicalLeaseEffect> target,
            IReadOnlyList<ExecutionCoordinatorEffectIntent> source)
        {
            for (int i = source.Count - 1; i >= 0; i--)
            {
                ExecutionCoordinatorEffectIntent intent = source[i];
                if (intent != null && intent.IntentType ==
                    ExecutionCoordinatorEffectIntentType
                        .AcquireResourceLease)
                {
                    target.Insert(0, new PendingLogicalLeaseEffect(
                        intent, true));
                }
            }
        }

        private static ExecutionCoordinatorEffectIntent CorrelateIntent(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionLogicalLeaseLoopCycleInput cycle)
        {
            if (intent == null || cycle == null ||
                intent.ResourceLease == null)
                return intent;

            string requestId = string.IsNullOrWhiteSpace(intent.RequestId)
                ? cycle.RequestId : intent.RequestId;
            string leaseId = string.IsNullOrWhiteSpace(
                    intent.ResourceLease.LeaseId)
                ? cycle.LeaseId : intent.ResourceLease.LeaseId;

            if ((!string.IsNullOrWhiteSpace(intent.RequestId) &&
                    !string.Equals(intent.RequestId, cycle.RequestId,
                        StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(intent.ResourceLease.LeaseId) &&
                    !string.Equals(intent.ResourceLease.LeaseId,
                        cycle.LeaseId, StringComparison.Ordinal)))
                return intent;

            return new ExecutionCoordinatorEffectIntent(
                intent.IntentId, intent.IntentType, intent.PlanId,
                intent.StepId, intent.ExecutionAttemptId, requestId,
                intent.ControlRequestId, intent.CreatedAtUtc,
                intent.ResourceRequirements, intent.TimeoutPolicy,
                intent.DiagnosticMessage,
                new ExecutionResourceLeaseIntentPayload(
                    leaseId, intent.ResourceLease.ResourceId,
                    intent.ResourceLease.ResourceType,
                    intent.ResourceLease.AccessMode));
        }

        private static ExecutionDomain FindStepDomain(
            ReactionExecutionPlan plan,
            string stepId)
        {
            ReactionExecutionStep step = FindStep(plan, stepId);
            return step != null ? step.Domain : ExecutionDomain.None;
        }

        private static ReactionExecutionStep FindStep(
            ReactionExecutionPlan plan,
            string stepId)
        {
            if (plan == null) return null;
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                ReactionExecutionStep step = plan.Steps[i];
                if (step != null && string.Equals(step.StepId, stepId,
                        StringComparison.Ordinal))
                    return step;
            }
            return null;
        }

        private sealed class PendingLogicalLeaseEffect
        {
            public ExecutionCoordinatorEffectIntent Intent { get; }
            public bool AvailabilityEvaluated { get; }

            public PendingLogicalLeaseEffect(
                ExecutionCoordinatorEffectIntent intent,
                bool availabilityEvaluated)
            {
                Intent = intent;
                AvailabilityEvaluated = availabilityEvaluated;
            }
        }

        private static bool ValidCycleInput(
            ExecutionLogicalLeaseLoopCycleInput cycle,
            ExecutionCoordinatorRuntimeState state)
        {
            return cycle != null && cycle.AuthorityContext != null &&
                cycle.ReducerContext != null &&
                !string.IsNullOrWhiteSpace(cycle.EventId) &&
                !string.IsNullOrWhiteSpace(cycle.EventSource) &&
                !string.IsNullOrWhiteSpace(cycle.RequestId) &&
                !string.IsNullOrWhiteSpace(cycle.LeaseId) &&
                cycle.ReducerContext.Generation == state.Generation &&
                cycle.ReducerContext.NowUtc != default(DateTime);
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string expected)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], expected,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static ExecutionLogicalLeaseLoopResult Result(
            ExecutionCoordinatorReductionResult reduction,
            LogicalResourceLeaseState leaseState,
            IEnumerable<ExecutionLogicalLeaseLoopTrace> traces,
            ExecutionLogicalLeaseLoopStopReason reason,
            int pumpCount)
        {
            return new ExecutionLogicalLeaseLoopResult(reduction,
                leaseState, traces, reason, pumpCount);
        }
    }
}
