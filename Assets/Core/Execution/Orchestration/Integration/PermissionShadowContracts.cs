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
    public enum PermissionShadowRunStatus
    {
        Completed = 0,
        Duplicate = 1,
        RequestTranslationFailed = 2,
        RuntimeAdapterFailed = 3,
        EventTranslationFailed = 4,
        ReductionRejected = 5
    }

    public sealed class PermissionShadowObservation
    {
        public string ObservationId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string PermissionRequestId { get; }
        public string PermissionKind { get; }
        public InteractionState InteractionStateAtQuery { get; }
        public bool HasLimboDecision { get; }
        public bool LimboDecision { get; }
        public string DenyReason { get; }
        public PermissionAdapterStatus AdapterStatus { get; }
        public ExecutionCoordinatorEventType CoordinatorEventType
            { get; }
        public ExecutionCoordinatorStepLifecycle?
            ReducerStepStateBefore { get; }
        public ExecutionCoordinatorStepLifecycle?
            ReducerStepStateAfter { get; }
        public IReadOnlyList<ExecutionCoordinatorEffectIntentType>
            GeneratedEffectIntentTypes { get; }
        public DateTime ObservedAtUtc { get; }
        public PermissionShadowRunStatus RunStatus { get; }
        public string FailureReason { get; }

        public PermissionShadowObservation(
            string observationId,
            string planId,
            string stepId,
            string executionAttemptId,
            string permissionRequestId,
            string permissionKind,
            InteractionState interactionStateAtQuery,
            bool hasLimboDecision,
            bool limboDecision,
            string denyReason,
            PermissionAdapterStatus adapterStatus,
            ExecutionCoordinatorEventType coordinatorEventType,
            ExecutionCoordinatorStepLifecycle? reducerStepStateBefore,
            ExecutionCoordinatorStepLifecycle? reducerStepStateAfter,
            IEnumerable<ExecutionCoordinatorEffectIntentType>
                generatedEffectIntentTypes,
            DateTime observedAtUtc,
            PermissionShadowRunStatus runStatus,
            string failureReason)
        {
            ObservationId = observationId ?? string.Empty;
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            ExecutionAttemptId = executionAttemptId ?? string.Empty;
            PermissionRequestId = permissionRequestId ?? string.Empty;
            PermissionKind = permissionKind ?? string.Empty;
            InteractionStateAtQuery = interactionStateAtQuery;
            HasLimboDecision = hasLimboDecision;
            LimboDecision = limboDecision;
            DenyReason = denyReason ?? string.Empty;
            AdapterStatus = adapterStatus;
            CoordinatorEventType = coordinatorEventType;
            ReducerStepStateBefore = reducerStepStateBefore;
            ReducerStepStateAfter = reducerStepStateAfter;
            GeneratedEffectIntentTypes =
                new ReadOnlyCollection<
                    ExecutionCoordinatorEffectIntentType>(
                    generatedEffectIntentTypes != null
                        ? new List<ExecutionCoordinatorEffectIntentType>(
                            generatedEffectIntentTypes)
                        : new List<
                            ExecutionCoordinatorEffectIntentType>());
            ObservedAtUtc = observedAtUtc;
            RunStatus = runStatus;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    public sealed class PermissionShadowExecutionResult
    {
        public PermissionShadowObservation Observation { get; }
        public PermissionCheckRequest Request { get; }
        public PermissionAdapterResult AdapterResult { get; }
        public ExecutionCoordinatorEvent CoordinatorEvent { get; }
        public ExecutionCoordinatorReductionResult ReductionResult
            { get; }
        public bool QueryInvoked { get; }

        public PermissionShadowExecutionResult(
            PermissionShadowObservation observation,
            PermissionCheckRequest request,
            PermissionAdapterResult adapterResult,
            ExecutionCoordinatorEvent coordinatorEvent,
            ExecutionCoordinatorReductionResult reductionResult,
            bool queryInvoked)
        {
            Observation = observation;
            Request = request;
            AdapterResult = adapterResult;
            CoordinatorEvent = coordinatorEvent;
            ReductionResult = reductionResult;
            QueryInvoked = queryInvoked;
        }
    }
}
