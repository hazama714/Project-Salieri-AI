// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Resources;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.Execution.Orchestration.Validation;

namespace SalieriAI.Core.Execution.Orchestration.Authority
{
    public enum ExecutionLogicalLeaseShadowDisposition
    {
        Invalid = 0,
        WouldAcquire = 1,
        WouldRejectAcquire = 2,
        WouldRelease = 3,
        WouldRejectRelease = 4
    }

    /// <summary>
    /// Explicit outer-runtime correlation for one read-only lease
    /// evaluation. IDs and timestamps are never generated here.
    /// </summary>
    public sealed class ExecutionLogicalLeaseShadowEvaluationContext
    {
        public string RequestId { get; }
        public string LeaseId { get; }
        public string EventId { get; }
        public string EventSource { get; }
        public ResourceShadowAvailabilityStatus Availability { get; }
        public bool StepIsTerminal { get; }
        public bool ReleaseConfirmed { get; }
        public string ReleaseFailureReason { get; }
        public int Generation { get; }

        public ExecutionLogicalLeaseShadowEvaluationContext(
            string requestId,
            string leaseId,
            string eventId,
            string eventSource,
            ResourceShadowAvailabilityStatus availability,
            bool stepIsTerminal,
            bool releaseConfirmed,
            string releaseFailureReason,
            int generation)
        {
            RequestId = requestId ?? string.Empty;
            LeaseId = leaseId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            Availability = availability;
            StepIsTerminal = stepIsTerminal;
            ReleaseConfirmed = releaseConfirmed;
            ReleaseFailureReason = releaseFailureReason ?? string.Empty;
            Generation = generation;
        }
    }

    /// <summary>
    /// Read-only prediction result. ProjectedCoordinatorEvent and
    /// ProjectedShadowState may be applied only inside a shadow evaluation.
    /// The observed live state is never modified.
    /// </summary>
    public sealed class ExecutionLogicalLeaseShadowEvaluationResult
    {
        public ExecutionCoordinatorEffectIntent Intent { get; }
        public ExecutionLogicalLeaseShadowDisposition Disposition { get; }
        public LogicalResourceLeaseState ObservedState { get; }
        public LogicalResourceLeaseState ProjectedShadowState { get; }
        public ResourceLeaseAcquireResult AcquireResult { get; }
        public ResourceLeaseReleaseResult ReleaseResult { get; }
        public ExecutionCoordinatorEvent ProjectedCoordinatorEvent { get; }
        public string FailureReason { get; }
        public int LiveOwnershipMutationCount => 0;

        public ExecutionLogicalLeaseShadowEvaluationResult(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionLogicalLeaseShadowDisposition disposition,
            LogicalResourceLeaseState observedState,
            LogicalResourceLeaseState projectedShadowState,
            ResourceLeaseAcquireResult acquireResult,
            ResourceLeaseReleaseResult releaseResult,
            ExecutionCoordinatorEvent projectedCoordinatorEvent,
            string failureReason)
        {
            Intent = intent;
            Disposition = disposition;
            ObservedState = observedState;
            ProjectedShadowState = projectedShadowState;
            AcquireResult = acquireResult;
            ReleaseResult = releaseResult;
            ProjectedCoordinatorEvent = projectedCoordinatorEvent;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    /// <summary>
    /// Side-effect-free logical lease boundary for orchestration shadow
    /// evaluation. It reuses the existing pure lease service, discards the
    /// predicted next lease state, and returns only the would-* decision plus
    /// an event projection for a shadow reducer state.
    /// </summary>
    public sealed class ExecutionLogicalResourceLeaseShadowEvaluator
    {
        public const string EvaluatorVersion =
            "execution-logical-lease-shadow-evaluator-v0.1";

        private readonly LogicalResourceLeaseService leaseService;

        public ExecutionLogicalResourceLeaseShadowEvaluator()
            : this(new LogicalResourceLeaseService())
        {
        }

        internal ExecutionLogicalResourceLeaseShadowEvaluator(
            LogicalResourceLeaseService leaseService)
        {
            this.leaseService = leaseService ??
                new LogicalResourceLeaseService();
        }

        public ExecutionLogicalLeaseShadowEvaluationResult Evaluate(
            ExecutionCoordinatorEffectIntent intent,
            LogicalResourceLeaseState currentState,
            ExecutionLogicalLeaseShadowEvaluationContext context)
        {
            string invalid = Validate(intent, currentState, context);
            if (invalid.Length > 0)
                return Result(intent, currentState,
                    ExecutionLogicalLeaseShadowDisposition.Invalid,
                    currentState, null, null, null, invalid);

            ExecutionCoordinatorEffectIntent correlated = Correlate(
                intent, context.RequestId, context.LeaseId);
            if (correlated == null)
                return Result(intent, currentState,
                    ExecutionLogicalLeaseShadowDisposition.Invalid,
                    currentState, null, null, null,
                    "Intent correlation conflicts with shadow context.");

            if (correlated.IntentType ==
                ExecutionCoordinatorEffectIntentType.AcquireResourceLease)
                return EvaluateAcquire(correlated, currentState, context);

            return EvaluateRelease(correlated, currentState, context);
        }

        private ExecutionLogicalLeaseShadowEvaluationResult EvaluateAcquire(
            ExecutionCoordinatorEffectIntent intent,
            LogicalResourceLeaseState currentState,
            ExecutionLogicalLeaseShadowEvaluationContext context)
        {
            ExecutionResourceLeaseIntentPayload payload =
                intent.ResourceLease;
            var request = new ResourceLeaseAcquireRequest(
                payload.LeaseId,
                intent.PlanId,
                intent.StepId,
                intent.ExecutionAttemptId,
                intent.RequestId,
                payload.ResourceId,
                payload.ResourceType,
                payload.AccessMode,
                context.Availability,
                intent.CreatedAtUtc,
                context.Generation,
                context.StepIsTerminal);

            LogicalResourceLeaseTransition<ResourceLeaseAcquireResult>
                prediction = leaseService.Acquire(currentState, request);
            ResourceLeaseAcquireResult serviceResult = prediction.Result;
            bool acquired = serviceResult != null &&
                serviceResult.Status ==
                    LogicalResourceLeaseAcquireStatus.Acquired;
            string reason = acquired ? string.Empty : Failure(
                serviceResult != null ? serviceResult.FailureReason : "",
                "Logical lease acquire would be rejected.");
            ExecutionCoordinatorEvent projected = ProjectEvent(
                intent,
                context,
                acquired ? ExecutionCoordinatorEventType.LeaseAcquired
                    : ExecutionCoordinatorEventType.LeaseRejected,
                acquired && serviceResult != null
                    ? serviceResult.LeaseId : string.Empty,
                acquired ? ExecutionResourceLeaseStatus.Acquired
                    : ExecutionResourceLeaseStatus.Rejected,
                reason);

            return Result(intent, currentState,
                acquired
                    ? ExecutionLogicalLeaseShadowDisposition.WouldAcquire
                    : ExecutionLogicalLeaseShadowDisposition
                        .WouldRejectAcquire,
                prediction.State, serviceResult, null, projected, reason);
        }

        private ExecutionLogicalLeaseShadowEvaluationResult EvaluateRelease(
            ExecutionCoordinatorEffectIntent intent,
            LogicalResourceLeaseState currentState,
            ExecutionLogicalLeaseShadowEvaluationContext context)
        {
            ExecutionResourceLeaseIntentPayload payload =
                intent.ResourceLease;
            var request = new ResourceLeaseReleaseRequest(
                payload.LeaseId,
                intent.PlanId,
                intent.StepId,
                intent.RequestId,
                payload.ResourceId,
                intent.CreatedAtUtc,
                context.Generation,
                context.ReleaseConfirmed,
                context.ReleaseFailureReason);

            LogicalResourceLeaseTransition<ResourceLeaseReleaseResult>
                prediction = leaseService.Release(currentState, request);
            ResourceLeaseReleaseResult serviceResult = prediction.Result;
            bool released = serviceResult != null &&
                serviceResult.Status ==
                    LogicalResourceLeaseReleaseStatus.Released;
            string reason = released ? string.Empty : Failure(
                serviceResult != null ? serviceResult.FailureReason : "",
                "Logical lease release would be rejected.");
            ExecutionCoordinatorEvent projected = ProjectEvent(
                intent,
                context,
                released ? ExecutionCoordinatorEventType.LeaseReleased
                    : ExecutionCoordinatorEventType.LeaseReleaseFailed,
                payload.LeaseId,
                released ? ExecutionResourceLeaseStatus.Released
                    : ExecutionResourceLeaseStatus.ReleaseFailed,
                reason);

            return Result(intent, currentState,
                released
                    ? ExecutionLogicalLeaseShadowDisposition.WouldRelease
                    : ExecutionLogicalLeaseShadowDisposition
                        .WouldRejectRelease,
                prediction.State, null, serviceResult, projected, reason);
        }

        private static string Validate(
            ExecutionCoordinatorEffectIntent intent,
            LogicalResourceLeaseState currentState,
            ExecutionLogicalLeaseShadowEvaluationContext context)
        {
            if (intent == null || currentState == null || context == null)
                return "Shadow lease input is missing.";
            if (intent.IntentType !=
                    ExecutionCoordinatorEffectIntentType
                        .AcquireResourceLease &&
                intent.IntentType !=
                    ExecutionCoordinatorEffectIntentType
                        .ReleaseResourceLease)
                return "Effect is not a logical lease operation.";
            if (intent.ResourceLease == null ||
                string.IsNullOrWhiteSpace(intent.IntentId) ||
                string.IsNullOrWhiteSpace(intent.PlanId) ||
                string.IsNullOrWhiteSpace(intent.StepId) ||
                string.IsNullOrWhiteSpace(intent.ResourceLease.ResourceId) ||
                string.IsNullOrWhiteSpace(
                    intent.ResourceLease.ResourceType) ||
                intent.CreatedAtUtc == default(DateTime) ||
                string.IsNullOrWhiteSpace(context.RequestId) ||
                string.IsNullOrWhiteSpace(context.LeaseId) ||
                string.IsNullOrWhiteSpace(context.EventId) ||
                string.IsNullOrWhiteSpace(context.EventSource) ||
                context.Generation < 0 ||
                currentState.Generation != context.Generation)
                return "Shadow lease correlation is invalid.";
            return string.Empty;
        }

        private static ExecutionCoordinatorEffectIntent Correlate(
            ExecutionCoordinatorEffectIntent intent,
            string requestId,
            string leaseId)
        {
            if ((!string.IsNullOrWhiteSpace(intent.RequestId) &&
                    !string.Equals(intent.RequestId, requestId,
                        StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(intent.ResourceLease.LeaseId) &&
                    !string.Equals(intent.ResourceLease.LeaseId, leaseId,
                        StringComparison.Ordinal)))
                return null;

            return new ExecutionCoordinatorEffectIntent(
                intent.IntentId,
                intent.IntentType,
                intent.PlanId,
                intent.StepId,
                intent.ExecutionAttemptId,
                requestId,
                intent.ControlRequestId,
                intent.CreatedAtUtc,
                intent.ResourceRequirements,
                intent.TimeoutPolicy,
                intent.DiagnosticMessage,
                new ExecutionResourceLeaseIntentPayload(
                    leaseId,
                    intent.ResourceLease.ResourceId,
                    intent.ResourceLease.ResourceType,
                    intent.ResourceLease.AccessMode));
        }

        private static ExecutionCoordinatorEvent ProjectEvent(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionLogicalLeaseShadowEvaluationContext context,
            ExecutionCoordinatorEventType eventType,
            string leaseId,
            ExecutionResourceLeaseStatus status,
            string reason)
        {
            var lease = new ResourceLeaseReference(
                leaseId,
                intent.ResourceLease.ResourceId,
                intent.ResourceLease.ResourceType,
                intent.PlanId,
                intent.StepId,
                intent.ExecutionAttemptId,
                intent.ResourceLease.AccessMode,
                intent.CreatedAtUtc,
                status);
            var projected = new ExecutionCoordinatorEvent(
                context.EventId,
                eventType,
                intent.PlanId,
                intent.StepId,
                intent.ExecutionAttemptId,
                string.Empty,
                intent.CreatedAtUtc,
                context.EventSource,
                context.Generation,
                null,
                ExecutionTerminalOutcome.None,
                string.IsNullOrEmpty(reason)
                    ? new string[0] : new[] { reason },
                reason,
                new ExecutionResourceLeaseEventPayload(lease, reason));
            ExecutionCoordinatorContractValidationResult validation =
                ExecutionCoordinatorContractValidator.ValidateEvent(
                    projected, intent.PlanId);
            return validation != null && validation.IsValid
                ? projected : null;
        }

        private static string Failure(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static ExecutionLogicalLeaseShadowEvaluationResult Result(
            ExecutionCoordinatorEffectIntent intent,
            LogicalResourceLeaseState observedState,
            ExecutionLogicalLeaseShadowDisposition disposition,
            LogicalResourceLeaseState projectedShadowState,
            ResourceLeaseAcquireResult acquireResult,
            ResourceLeaseReleaseResult releaseResult,
            ExecutionCoordinatorEvent projectedEvent,
            string reason)
        {
            if (disposition !=
                    ExecutionLogicalLeaseShadowDisposition.Invalid &&
                projectedEvent == null)
            {
                disposition = ExecutionLogicalLeaseShadowDisposition.Invalid;
                reason = "Projected coordinator event is invalid.";
            }
            return new ExecutionLogicalLeaseShadowEvaluationResult(
                intent, disposition, observedState, projectedShadowState,
                acquireResult, releaseResult, projectedEvent, reason);
        }
    }
}
