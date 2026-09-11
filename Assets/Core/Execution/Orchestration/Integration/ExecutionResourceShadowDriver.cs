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

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>
    /// Availability-only resource shadow connection. No lease is acquired,
    /// no release is issued, no event is synthesized, and no effect is
    /// dispatched.
    /// </summary>
    public sealed class ExecutionResourceShadowDriver
    {
        public const string DriverVersion =
            "execution-resource-shadow-driver-3c3.1";
        public const int MaxProcessedRequestIds = 256;

        private readonly ExecutionResourceRuntimeAdapter adapter;
        private readonly HashSet<string> processedRequestIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> processedRequestOrder =
            new Queue<string>();

        public int ProcessedRequestCount => processedRequestIds.Count;

        public ExecutionResourceShadowDriver(
            ExecutionResourceRuntimeAdapter adapter)
        {
            this.adapter = adapter;
        }

        public ResourceShadowExecutionResult Observe(
            ExecutionCoordinatorEffectIntent intent,
            string requestId,
            string observationId,
            DateTime observedAtUtc,
            ExecutionCoordinatorRuntimeState state)
        {
            ExecutionCoordinatorStepLifecycle? before =
                StepLifecycle(state,
                    intent != null ? intent.StepId : string.Empty);
            AdapterTranslationResult<ResourceAcquireRequest>
                requestTranslation =
                    ExecutionRuntimeAdapterTranslator.FromAcquireIntent(
                        intent, requestId);
            if (!requestTranslation.IsValid)
                return Failure(
                    observationId, intent, requestId, before,
                    observedAtUtc,
                    ResourceShadowRunStatus.RequestTranslationFailed,
                    FirstFailureCode(
                        requestTranslation.FailureCodes),
                    null, null, false);

            ResourceAcquireRequest request = requestTranslation.Value;
            if (processedRequestIds.Contains(request.RequestId))
                return Failure(
                    observationId, intent, requestId, before,
                    observedAtUtc, ResourceShadowRunStatus.Duplicate,
                    "DUPLICATE_RESOURCE_REQUEST",
                    request, null, false);

            Remember(request.RequestId);
            if (adapter == null)
                return Failure(
                    observationId, intent, requestId, before,
                    observedAtUtc,
                    ResourceShadowRunStatus.RuntimeAdapterFailed,
                    "RESOURCE_ADAPTER_UNAVAILABLE",
                    request, null, false);

            ResourceShadowAvailabilityResult result =
                adapter.QueryAvailability(request, observedAtUtc);
            string correlation =
                ResourceShadowContractValidator.ValidateCorrelation(
                    request, result);
            if (!string.IsNullOrEmpty(correlation))
                return Failure(
                    observationId, intent, requestId, before,
                    observedAtUtc,
                    ResourceShadowRunStatus.CorrelationFailed,
                    correlation, request, result, true);
            if (result.Status ==
                    ResourceShadowAvailabilityStatus.Failed)
                return Failure(
                    observationId, intent, requestId, before,
                    observedAtUtc,
                    ResourceShadowRunStatus.RuntimeAdapterFailed,
                    result.DecisionReason, request, result, true);

            ResourceShadowObservation observation =
                Observation(
                    observationId, request, result, before,
                    observedAtUtc, ResourceShadowRunStatus.Completed,
                    string.Empty);
            return new ResourceShadowExecutionResult(
                observation, request, result, true);
        }

        public void ClearDuplicateHistory()
        {
            processedRequestIds.Clear();
            processedRequestOrder.Clear();
        }

        private void Remember(string requestId)
        {
            processedRequestIds.Add(requestId);
            processedRequestOrder.Enqueue(requestId);
            while (processedRequestOrder.Count >
                MaxProcessedRequestIds)
            {
                string expired = processedRequestOrder.Dequeue();
                processedRequestIds.Remove(expired);
            }
        }

        private static ResourceShadowExecutionResult Failure(
            string observationId,
            ExecutionCoordinatorEffectIntent intent,
            string requestId,
            ExecutionCoordinatorStepLifecycle? before,
            DateTime observedAtUtc,
            ResourceShadowRunStatus status,
            string failureReason,
            ResourceAcquireRequest request,
            ResourceShadowAvailabilityResult result,
            bool queryInvoked)
        {
            ResourceShadowObservation observation =
                request != null
                    ? Observation(
                        observationId, request, result, before,
                        observedAtUtc, status, failureReason)
                    : new ResourceShadowObservation(
                        observationId,
                        intent != null ? intent.PlanId : string.Empty,
                        intent != null ? intent.StepId : string.Empty,
                        intent != null
                            ? intent.ExecutionAttemptId : string.Empty,
                        requestId,
                        intent != null &&
                            intent.ResourceLease != null
                            ? intent.ResourceLease.ResourceId
                            : string.Empty,
                        intent != null &&
                            intent.ResourceLease != null
                            ? intent.ResourceLease.ResourceType
                            : string.Empty,
                        intent != null &&
                            intent.ResourceLease != null
                            ? intent.ResourceLease.AccessMode
                            : ExecutionResourceAccessMode.Exclusive,
                        result != null
                            ? result.Status
                            : ResourceShadowAvailabilityStatus.Failed,
                        result != null
                            ? result.DecisionReason : string.Empty,
                        before, observedAtUtc, status, failureReason);
            return new ResourceShadowExecutionResult(
                observation, request, result, queryInvoked);
        }

        private static ResourceShadowObservation Observation(
            string observationId,
            ResourceAcquireRequest request,
            ResourceShadowAvailabilityResult result,
            ExecutionCoordinatorStepLifecycle? before,
            DateTime observedAtUtc,
            ResourceShadowRunStatus status,
            string failureReason)
        {
            return new ResourceShadowObservation(
                observationId, request.PlanId, request.StepId,
                request.ExecutionAttemptId, request.RequestId,
                request.ResourceId, request.ResourceType,
                request.AccessMode,
                result != null
                    ? result.Status
                    : ResourceShadowAvailabilityStatus.Failed,
                result != null
                    ? result.DecisionReason : string.Empty,
                before, observedAtUtc, status, failureReason);
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

        private static string FirstFailureCode(
            IReadOnlyList<string> failureCodes)
        {
            return failureCodes != null && failureCodes.Count > 0
                ? failureCodes[0]
                : "ADAPTER_TRANSLATION_FAILED";
        }
    }
}
