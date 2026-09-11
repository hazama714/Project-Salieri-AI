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
    public enum SafetyRuntimeAdapterStatus
    {
        Observed = 0,
        RejectedInvalid = 1
    }

    public enum SafetyRuntimeCorrelationStatus
    {
        Accepted = 0,
        Duplicate = 1,
        Stale = 2,
        Mismatch = 3,
        Failed = 4
    }

    public enum SafetyShadowRunStatus
    {
        Completed = 0,
        ObservationOnly = 1,
        Duplicate = 2,
        Stale = 3,
        CorrelationFailed = 4,
        RuntimeAdapterFailed = 5,
        EventTranslationFailed = 6,
        ReductionRejected = 7
    }

    public sealed class SafetyRuntimeAdapterResult
    {
        public SafetyRuntimeAdapterStatus Status { get; }
        public SafetyRuntimeLifecycle Lifecycle { get; }
        public SafetyControlResult ControlResult { get; }
        public bool CanTranslateToCoordinatorEvent => ControlResult != null;
        public bool Accepted { get; }
        public bool EffectConfirmed { get; }
        public bool PhysicalStopVerified { get; }
        public string FailureReason { get; }

        public SafetyRuntimeAdapterResult(
            SafetyRuntimeAdapterStatus status,
            SafetyRuntimeLifecycle lifecycle,
            SafetyControlResult controlResult, bool accepted,
            bool effectConfirmed, bool physicalStopVerified,
            string failureReason)
        {
            Status = status;
            Lifecycle = lifecycle;
            ControlResult = controlResult;
            Accepted = accepted;
            EffectConfirmed = effectConfirmed;
            PhysicalStopVerified = physicalStopVerified;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    public sealed class SafetyRuntimeCorrelationResult
    {
        public SafetyRuntimeCorrelationStatus Status { get; }
        public string FailureReason { get; }
        public SafetyRuntimeCorrelationResult(
            SafetyRuntimeCorrelationStatus status, string failureReason)
        {
            Status = status;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    public sealed class SafetyShadowObservation
    {
        public string ObservationId { get; }
        public string PlanId { get; }
        public string TargetStepId { get; }
        public string TargetAttemptId { get; }
        public string ControlRequestId { get; }
        public ExecutionControlType ControlKind { get; }
        public ExecutionControlScope RequestedScope { get; }
        public SafetyRuntimeActualScope ActualRuntimeScope { get; }
        public string RuntimeControlToken { get; }
        public int RuntimeGeneration { get; }
        public SafetyRuntimeLifecycle Lifecycle { get; }
        public bool Accepted { get; }
        public bool EffectConfirmed { get; }
        public bool PhysicalStopVerified { get; }
        public SafetyRuntimeVerificationLevel VerificationLevel { get; }
        public InteractionState InteractionStateAtObservation { get; }
        public ExecutionCoordinatorPlanLifecycle? ReducerStateBefore { get; }
        public ExecutionCoordinatorPlanLifecycle? ReducerStateAfter { get; }
        public ExecutionCoordinatorEventType CoordinatorEventType { get; }
        public IReadOnlyList<ExecutionCoordinatorEffectIntentType>
            GeneratedEffectIntentTypes { get; }
        public string FailureReason { get; }
        public string Diagnostic { get; }
        public DateTime ObservedAtUtc { get; }
        public SafetyShadowRunStatus RunStatus { get; }

        public SafetyShadowObservation(
            string observationId, SafetyControlRequest request,
            SafetyControlRuntimeFact fact,
            SafetyRuntimeAdapterResult adapter,
            InteractionState interactionState,
            ExecutionCoordinatorPlanLifecycle? before,
            ExecutionCoordinatorPlanLifecycle? after,
            ExecutionCoordinatorEventType eventType,
            IEnumerable<ExecutionCoordinatorEffectIntentType> effects,
            string failureReason, SafetyShadowRunStatus runStatus)
        {
            ObservationId = observationId ?? string.Empty;
            PlanId = request != null ? request.PlanId : string.Empty;
            TargetStepId = request != null ? request.TargetStepId : string.Empty;
            TargetAttemptId = request != null ? request.TargetAttemptId : string.Empty;
            ControlRequestId = request != null ? request.ControlRequestId : string.Empty;
            ControlKind = request != null ? request.ControlKind : ExecutionControlType.None;
            RequestedScope = request != null ? request.Scope : ExecutionControlScope.None;
            ActualRuntimeScope = fact != null ? fact.RuntimeScope : SafetyRuntimeActualScope.Unknown;
            RuntimeControlToken = fact != null ? fact.RuntimeControlToken : string.Empty;
            RuntimeGeneration = fact != null ? fact.RuntimeGeneration : 0;
            Lifecycle = fact != null ? fact.Lifecycle : SafetyRuntimeLifecycle.RequestObserved;
            Accepted = adapter != null && adapter.Accepted;
            EffectConfirmed = adapter != null && adapter.EffectConfirmed;
            PhysicalStopVerified = adapter != null && adapter.PhysicalStopVerified;
            VerificationLevel = fact != null ? fact.VerificationLevel : SafetyRuntimeVerificationLevel.RequestOnly;
            InteractionStateAtObservation = interactionState;
            ReducerStateBefore = before;
            ReducerStateAfter = after;
            CoordinatorEventType = eventType;
            GeneratedEffectIntentTypes = new ReadOnlyCollection<ExecutionCoordinatorEffectIntentType>(
                effects != null ? new List<ExecutionCoordinatorEffectIntentType>(effects) :
                new List<ExecutionCoordinatorEffectIntentType>());
            FailureReason = failureReason ?? string.Empty;
            Diagnostic = fact != null ? fact.Diagnostic : string.Empty;
            ObservedAtUtc = fact != null ? fact.OccurredAtUtc : default(DateTime);
            RunStatus = runStatus;
        }
    }

    public sealed class SafetyShadowExecutionResult
    {
        public SafetyShadowObservation Observation { get; }
        public SafetyRuntimeAdapterResult AdapterResult { get; }
        public IReadOnlyList<ExecutionCoordinatorEvent> CoordinatorEvents { get; }
        public IReadOnlyList<ExecutionCoordinatorReductionResult> ReductionResults { get; }
        public bool SafetyExecutionInvoked { get; }

        public SafetyShadowExecutionResult(
            SafetyShadowObservation observation,
            SafetyRuntimeAdapterResult adapterResult,
            IEnumerable<ExecutionCoordinatorEvent> events,
            IEnumerable<ExecutionCoordinatorReductionResult> reductions)
        {
            Observation = observation;
            AdapterResult = adapterResult;
            CoordinatorEvents = new ReadOnlyCollection<ExecutionCoordinatorEvent>(
                events != null ? new List<ExecutionCoordinatorEvent>(events) : new List<ExecutionCoordinatorEvent>());
            ReductionResults = new ReadOnlyCollection<ExecutionCoordinatorReductionResult>(
                reductions != null ? new List<ExecutionCoordinatorReductionResult>(reductions) :
                new List<ExecutionCoordinatorReductionResult>());
            SafetyExecutionInvoked = false;
        }
    }
}
