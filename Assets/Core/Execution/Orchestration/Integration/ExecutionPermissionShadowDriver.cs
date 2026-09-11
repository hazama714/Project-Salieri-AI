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
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.Execution.Orchestration.Validation;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>
    /// Permission-only shadow connection. Generated reducer effects are
    /// copied into the observation and are never dispatched.
    /// </summary>
    public sealed class ExecutionPermissionShadowDriver
    {
        public const string DriverVersion =
            "execution-permission-shadow-driver-3c2.1";
        public const int MaxProcessedRequestIds = 256;

        private readonly ExecutionPermissionRuntimeAdapter adapter;
        private readonly HashSet<string> processedRequestIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> processedRequestOrder =
            new Queue<string>();

        public int ProcessedRequestCount => processedRequestIds.Count;

        public ExecutionPermissionShadowDriver(
            ExecutionPermissionRuntimeAdapter adapter)
        {
            this.adapter = adapter;
        }

        public PermissionShadowExecutionResult Observe(
            ExecutionCoordinatorEffectIntent intent,
            string permissionRequestId,
            string permissionKind,
            string coordinatorEventId,
            string observationId,
            DateTime occurredAtUtc,
            string eventSource,
            int generation,
            InteractionState interactionStateAtQuery,
            ValidatedExecutionPlan plan,
            ExecutionCoordinatorRuntimeState state,
            ExecutionCoordinatorReducerContext reducerContext)
        {
            ExecutionCoordinatorStepLifecycle? before = StepLifecycle(
                state, intent != null ? intent.StepId : string.Empty);
            AdapterTranslationResult<PermissionCheckRequest> requestResult =
                ExecutionRuntimeAdapterTranslator.FromPermissionIntent(
                    intent, permissionRequestId, permissionKind);
            if (!requestResult.IsValid)
                return Failure(
                    observationId, intent, permissionRequestId,
                    permissionKind, interactionStateAtQuery, before,
                    occurredAtUtc,
                    PermissionShadowRunStatus.RequestTranslationFailed,
                    FirstFailureCode(requestResult.FailureCodes),
                    null, null, null, false);

            PermissionCheckRequest request = requestResult.Value;
            if (processedRequestIds.Contains(request.RequestId))
                return Failure(
                    observationId, intent, permissionRequestId,
                    permissionKind, interactionStateAtQuery, before,
                    occurredAtUtc, PermissionShadowRunStatus.Duplicate,
                    "DUPLICATE_PERMISSION_REQUEST", request, null, null,
                    false);

            RememberProcessedRequest(request.RequestId);
            if (adapter == null)
                return Failure(
                    observationId, intent, permissionRequestId,
                    permissionKind, interactionStateAtQuery, before,
                    occurredAtUtc,
                    PermissionShadowRunStatus.RuntimeAdapterFailed,
                    "PERMISSION_ADAPTER_UNAVAILABLE", request, null, null,
                    false);

            PermissionAdapterResult adapterResult =
                adapter.Query(request, occurredAtUtc);
            if (adapterResult == null || adapterResult.Status ==
                    PermissionAdapterStatus.Failed)
            {
                return Failure(
                    observationId, intent, permissionRequestId,
                    permissionKind, interactionStateAtQuery, before,
                    occurredAtUtc,
                    PermissionShadowRunStatus.RuntimeAdapterFailed,
                    adapterResult != null
                        ? adapterResult.Reason
                        : "PERMISSION_ADAPTER_RESULT_NULL",
                    request, adapterResult, null, true);
            }

            AdapterTranslationResult<ExecutionCoordinatorEvent>
                eventResult = ExecutionRuntimeAdapterTranslator
                    .ToPermissionEvent(
                        request, adapterResult, coordinatorEventId,
                        generation, eventSource);
            if (!eventResult.IsValid)
                return Failure(
                    observationId, intent, permissionRequestId,
                    permissionKind, interactionStateAtQuery, before,
                    occurredAtUtc,
                    PermissionShadowRunStatus.EventTranslationFailed,
                    FirstFailureCode(eventResult.FailureCodes),
                    request, adapterResult, null,
                    true);

            ExecutionCoordinatorEvent coordinatorEvent =
                eventResult.Value;
            ExecutionCoordinatorReductionResult reduction =
                ExecutionCoordinatorReducer.Reduce(
                    plan, state, coordinatorEvent, reducerContext);
            PermissionShadowRunStatus runStatus =
                reduction != null &&
                reduction.EventDisposition ==
                    ExecutionCoordinatorEventDisposition.Applied
                    ? PermissionShadowRunStatus.Completed
                    : PermissionShadowRunStatus.ReductionRejected;
            string failure = runStatus ==
                    PermissionShadowRunStatus.Completed
                ? string.Empty
                : "PERMISSION_REDUCTION_NOT_APPLIED";
            return Completed(
                observationId, request, adapterResult,
                coordinatorEvent, reduction,
                interactionStateAtQuery, before, occurredAtUtc,
                runStatus, failure);
        }

        public void ClearDuplicateHistory()
        {
            processedRequestIds.Clear();
            processedRequestOrder.Clear();
        }

        private void RememberProcessedRequest(string requestId)
        {
            processedRequestIds.Add(requestId);
            processedRequestOrder.Enqueue(requestId);
            while (processedRequestOrder.Count > MaxProcessedRequestIds)
            {
                string expired = processedRequestOrder.Dequeue();
                processedRequestIds.Remove(expired);
            }
        }

        private static PermissionShadowExecutionResult Completed(
            string observationId,
            PermissionCheckRequest request,
            PermissionAdapterResult adapterResult,
            ExecutionCoordinatorEvent coordinatorEvent,
            ExecutionCoordinatorReductionResult reduction,
            InteractionState interactionState,
            ExecutionCoordinatorStepLifecycle? before,
            DateTime observedAtUtc,
            PermissionShadowRunStatus runStatus,
            string failureReason)
        {
            List<ExecutionCoordinatorEffectIntentType> effects =
                EffectTypes(reduction);
            ExecutionCoordinatorStepLifecycle? after = StepLifecycle(
                reduction != null ? reduction.State : null,
                request.StepId);
            PermissionShadowObservation observation =
                new PermissionShadowObservation(
                    observationId, request.PlanId, request.StepId,
                    request.ExecutionAttemptId, request.RequestId,
                    request.PermissionKind, interactionState,
                    true,
                    adapterResult.Status ==
                        PermissionAdapterStatus.Granted,
                    adapterResult.Reason, adapterResult.Status,
                    coordinatorEvent.EventType, before, after,
                    effects, observedAtUtc, runStatus, failureReason);
            return new PermissionShadowExecutionResult(
                observation, request, adapterResult,
                coordinatorEvent, reduction, true);
        }

        private static PermissionShadowExecutionResult Failure(
            string observationId,
            ExecutionCoordinatorEffectIntent intent,
            string permissionRequestId,
            string permissionKind,
            InteractionState interactionState,
            ExecutionCoordinatorStepLifecycle? before,
            DateTime observedAtUtc,
            PermissionShadowRunStatus status,
            string failureReason,
            PermissionCheckRequest request,
            PermissionAdapterResult adapterResult,
            ExecutionCoordinatorEvent coordinatorEvent,
            bool queryInvoked)
        {
            PermissionShadowObservation observation =
                new PermissionShadowObservation(
                    observationId,
                    request != null
                        ? request.PlanId
                        : intent != null ? intent.PlanId : string.Empty,
                    request != null
                        ? request.StepId
                        : intent != null ? intent.StepId : string.Empty,
                    request != null
                        ? request.ExecutionAttemptId
                        : intent != null
                            ? intent.ExecutionAttemptId : string.Empty,
                    request != null
                        ? request.RequestId : permissionRequestId,
                    permissionKind, interactionState,
                    adapterResult != null &&
                        adapterResult.Status !=
                            PermissionAdapterStatus.Failed,
                    adapterResult != null &&
                        adapterResult.Status ==
                            PermissionAdapterStatus.Granted,
                    adapterResult != null
                        ? adapterResult.Reason : string.Empty,
                    adapterResult != null
                        ? adapterResult.Status
                        : PermissionAdapterStatus.Failed,
                    coordinatorEvent != null
                        ? coordinatorEvent.EventType
                        : ExecutionCoordinatorEventType.None,
                    before, before,
                    new ExecutionCoordinatorEffectIntentType[0],
                    observedAtUtc, status, failureReason);
            return new PermissionShadowExecutionResult(
                observation, request, adapterResult,
                coordinatorEvent, null, queryInvoked);
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

        private static List<ExecutionCoordinatorEffectIntentType>
            EffectTypes(ExecutionCoordinatorReductionResult reduction)
        {
            List<ExecutionCoordinatorEffectIntentType> result =
                new List<ExecutionCoordinatorEffectIntentType>();
            if (reduction == null) return result;
            for (int i = 0; i < reduction.EffectIntents.Count; i++)
                result.Add(reduction.EffectIntents[i].IntentType);
            return result;
        }

        private static string FirstFailureCode(
            IReadOnlyList<string> failureCodes)
        {
            return failureCodes != null && failureCodes.Count > 0
                ? failureCodes[0]
                : "ADAPTER_TRANSLATION_FAILED";
        }
    }
}
