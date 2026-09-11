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

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    public enum ResourceShadowAvailabilityStatus
    {
        Available = 0,
        Unavailable = 1,
        Unknown = 2,
        Failed = 3
    }

    public enum ResourceShadowRunStatus
    {
        Completed = 0,
        Duplicate = 1,
        RequestTranslationFailed = 2,
        RuntimeAdapterFailed = 3,
        CorrelationFailed = 4
    }

    public sealed class ResourceShadowAvailabilityResult
    {
        public string RequestId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public ResourceShadowAvailabilityStatus Status { get; }
        public string DecisionReason { get; }
        public DateTime ObservedAtUtc { get; }

        public ResourceShadowAvailabilityResult(
            string requestId, string planId, string stepId,
            string executionAttemptId, string resourceId,
            string resourceType, ExecutionResourceAccessMode accessMode,
            ResourceShadowAvailabilityStatus status,
            string decisionReason, DateTime observedAtUtc)
        {
            RequestId = requestId ?? string.Empty;
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            ExecutionAttemptId = executionAttemptId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            ResourceType = resourceType ?? string.Empty;
            AccessMode = accessMode;
            Status = status;
            DecisionReason = decisionReason ?? string.Empty;
            ObservedAtUtc = observedAtUtc;
        }
    }

    public sealed class ResourceShadowObservation
    {
        public string ObservationId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string RequestId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public ExecutionResourceAccessMode AccessMode { get; }
        public ResourceShadowAvailabilityStatus RuntimeAvailability
            { get; }
        public string RuntimeDecisionReason { get; }
        public string ExternalLeaseId { get; }
        public ResourceShadowAvailabilityStatus AdapterStatus { get; }
        public ExecutionCoordinatorEventType
            SimulatedCoordinatorEventType { get; }
        public ExecutionCoordinatorStepLifecycle? ReducerStateBefore
            { get; }
        public ExecutionCoordinatorStepLifecycle? ReducerStateAfter
            { get; }
        public IReadOnlyList<ExecutionCoordinatorEffectIntentType>
            GeneratedEffectIntentTypes { get; }
        public DateTime ObservedAtUtc { get; }
        public ResourceShadowRunStatus RunStatus { get; }
        public string FailureReason { get; }
        public bool RuntimeLeaseAcquired { get; }
        public bool ReducerSimulationPerformed { get; }

        public ResourceShadowObservation(
            string observationId, string planId, string stepId,
            string executionAttemptId, string requestId,
            string resourceId, string resourceType,
            ExecutionResourceAccessMode accessMode,
            ResourceShadowAvailabilityStatus runtimeAvailability,
            string runtimeDecisionReason,
            ExecutionCoordinatorStepLifecycle? reducerStateBefore,
            DateTime observedAtUtc, ResourceShadowRunStatus runStatus,
            string failureReason)
        {
            ObservationId = observationId ?? string.Empty;
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            ExecutionAttemptId = executionAttemptId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            ResourceType = resourceType ?? string.Empty;
            AccessMode = accessMode;
            RuntimeAvailability = runtimeAvailability;
            RuntimeDecisionReason =
                runtimeDecisionReason ?? string.Empty;
            ExternalLeaseId = string.Empty;
            AdapterStatus = runtimeAvailability;
            SimulatedCoordinatorEventType =
                ExecutionCoordinatorEventType.None;
            ReducerStateBefore = reducerStateBefore;
            ReducerStateAfter = reducerStateBefore;
            GeneratedEffectIntentTypes =
                new ReadOnlyCollection<
                    ExecutionCoordinatorEffectIntentType>(
                    new List<ExecutionCoordinatorEffectIntentType>());
            ObservedAtUtc = observedAtUtc;
            RunStatus = runStatus;
            FailureReason = failureReason ?? string.Empty;
            RuntimeLeaseAcquired = false;
            ReducerSimulationPerformed = false;
        }
    }

    public sealed class ResourceShadowExecutionResult
    {
        public ResourceShadowObservation Observation { get; }
        public ResourceAcquireRequest Request { get; }
        public ResourceShadowAvailabilityResult AvailabilityResult
            { get; }
        public bool QueryInvoked { get; }

        public ResourceShadowExecutionResult(
            ResourceShadowObservation observation,
            ResourceAcquireRequest request,
            ResourceShadowAvailabilityResult availabilityResult,
            bool queryInvoked)
        {
            Observation = observation;
            Request = request;
            AvailabilityResult = availabilityResult;
            QueryInvoked = queryInvoked;
        }
    }

    public static class ResourceShadowContractValidator
    {
        public static string ValidateCorrelation(
            ResourceAcquireRequest request,
            ResourceShadowAvailabilityResult result)
        {
            if (request == null || result == null)
                return ExecutionAdapterFailureCodes.NullSource;
            if (string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.PlanId) ||
                string.IsNullOrWhiteSpace(request.StepId) ||
                string.IsNullOrWhiteSpace(request.ResourceId))
                return ExecutionAdapterFailureCodes.MissingId;
            if (!Same(request.RequestId, result.RequestId) ||
                !Same(request.PlanId, result.PlanId) ||
                !Same(request.StepId, result.StepId) ||
                !Same(request.ExecutionAttemptId,
                    result.ExecutionAttemptId) ||
                !Same(request.ResourceId, result.ResourceId) ||
                !Same(request.ResourceType, result.ResourceType) ||
                request.AccessMode != result.AccessMode)
                return ExecutionAdapterFailureCodes.CorrelationMismatch;
            return string.Empty;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(
                left, right, StringComparison.Ordinal);
        }
    }
}
