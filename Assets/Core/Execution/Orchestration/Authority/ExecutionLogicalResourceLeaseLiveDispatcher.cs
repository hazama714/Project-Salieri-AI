// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Resources;
using SalieriAI.Core.Execution.Orchestration.Runtime;
using SalieriAI.Core.Execution.Orchestration.Validation;

namespace SalieriAI.Core.Execution.Orchestration.Authority
{
    public static class ExecutionLogicalLeaseDispatchFailureCodes
    {
        public const string NotAuthorized = "LOGICAL_LEASE_NOT_AUTHORIZED";
        public const string InvalidInput = "LOGICAL_LEASE_INVALID_INPUT";
        public const string UnsupportedEffect = "LOGICAL_LEASE_UNSUPPORTED_EFFECT";
        public const string EventInvalid = "LOGICAL_LEASE_EVENT_INVALID";
    }

    /// <summary>
    /// Immutable audit boundary for one authority-aware logical lease
    /// dispatch. The original intent retains request correlation; the
    /// service result is kept separate from the authority decision.
    /// </summary>
    public sealed class ExecutionLogicalLeaseDispatchResult
    {
        public ExecutionCoordinatorEffectIntent Intent { get; }
        public ExecutionAuthorityDecision AuthorityDecision { get; }
        public bool ServiceCalled { get; }
        public LogicalResourceLeaseState State { get; }
        public ResourceLeaseAcquireResult AcquireResult { get; }
        public ResourceLeaseReleaseResult ReleaseResult { get; }
        public ExecutionCoordinatorEvent CoordinatorEvent { get; }
        public string FailureReason { get; }

        public ExecutionLogicalLeaseDispatchResult(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionAuthorityDecision authorityDecision,
            bool serviceCalled,
            LogicalResourceLeaseState state,
            ResourceLeaseAcquireResult acquireResult,
            ResourceLeaseReleaseResult releaseResult,
            ExecutionCoordinatorEvent coordinatorEvent,
            string failureReason)
        {
            Intent = intent;
            AuthorityDecision = authorityDecision;
            ServiceCalled = serviceCalled;
            State = state;
            AcquireResult = acquireResult;
            ReleaseResult = releaseResult;
            CoordinatorEvent = coordinatorEvent;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    /// <summary>
    /// Phase 3D-1 authority-aware dispatcher. Only AcquireResourceLease and
    /// ReleaseResourceLease may reach the pure logical lease service. It
    /// never calls a physical runtime and never generates IDs or timestamps.
    /// </summary>
    public sealed class ExecutionLogicalResourceLeaseLiveDispatcher
    {
        public const string DispatcherVersion =
            "execution-logical-lease-live-dispatcher-3d1.0";

        private readonly ExecutionLimitedLiveAuthorityGate authorityGate;
        private readonly LogicalResourceLeaseService leaseService;

        public ExecutionLogicalResourceLeaseLiveDispatcher()
            : this(new ExecutionLimitedLiveAuthorityGate(),
                new LogicalResourceLeaseService())
        {
        }

        internal ExecutionLogicalResourceLeaseLiveDispatcher(
            ExecutionLimitedLiveAuthorityGate authorityGate,
            LogicalResourceLeaseService leaseService)
        {
            this.authorityGate = authorityGate ??
                new ExecutionLimitedLiveAuthorityGate();
            this.leaseService = leaseService ??
                new LogicalResourceLeaseService();
        }

        public ExecutionLogicalLeaseDispatchResult Dispatch(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionDomain stepDomain,
            ExecutionAuthorityPolicySnapshot policy,
            ExecutionRuntimeCapabilitySnapshot capability,
            ExecutionAuthorityEvaluationContext authorityContext,
            LogicalResourceLeaseState state,
            ResourceShadowAvailabilityStatus availability,
            bool stepIsTerminal,
            bool releaseConfirmed,
            string releaseFailureReason,
            int generation,
            string eventId,
            string eventSource)
        {
            ExecutionAuthorityRequest authorityRequest =
                CreateAuthorityRequest(intent, stepDomain);
            ExecutionAuthorityDecision decision = authorityGate.Evaluate(
                policy, authorityRequest, capability, authorityContext);

            if (decision == null || !decision.DispatchAllowed ||
                decision.Disposition !=
                    ExecutionAuthorityDisposition.AllowedDispatch ||
                decision.EffectiveMode !=
                    ExecutionAuthorityMode.LiveDispatch)
            {
                return Result(intent, decision, false, state, null, null,
                    null, decision != null &&
                        !string.IsNullOrEmpty(decision.DenialReason)
                            ? decision.DenialReason
                            : ExecutionLogicalLeaseDispatchFailureCodes
                                .NotAuthorized);
            }

            string invalid = ValidateDispatchInput(intent, decision, state,
                generation, eventId, eventSource);
            if (invalid.Length > 0)
                return Result(intent, decision, false, state, null, null,
                    null, invalid);

            if (intent.IntentType ==
                ExecutionCoordinatorEffectIntentType.AcquireResourceLease)
            {
                return DispatchAcquire(intent, decision, state,
                    availability, stepIsTerminal, generation,
                    eventId, eventSource);
            }

            if (intent.IntentType ==
                ExecutionCoordinatorEffectIntentType.ReleaseResourceLease)
            {
                return DispatchRelease(intent, decision, state,
                    releaseConfirmed, releaseFailureReason, generation,
                    eventId, eventSource);
            }

            return Result(intent, decision, false, state, null, null, null,
                ExecutionLogicalLeaseDispatchFailureCodes.UnsupportedEffect);
        }

        private ExecutionLogicalLeaseDispatchResult DispatchAcquire(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionAuthorityDecision decision,
            LogicalResourceLeaseState state,
            ResourceShadowAvailabilityStatus availability,
            bool stepIsTerminal,
            int generation,
            string eventId,
            string eventSource)
        {
            ExecutionResourceLeaseIntentPayload payload =
                intent.ResourceLease;
            var request = new ResourceLeaseAcquireRequest(
                payload.LeaseId, intent.PlanId, intent.StepId,
                intent.ExecutionAttemptId, intent.RequestId,
                payload.ResourceId, payload.ResourceType,
                payload.AccessMode, availability, intent.CreatedAtUtc,
                generation, stepIsTerminal);

            LogicalResourceLeaseTransition<ResourceLeaseAcquireResult>
                transition = leaseService.Acquire(state, request);
            ResourceLeaseAcquireResult serviceResult = transition.Result;
            bool acquired = serviceResult != null &&
                serviceResult.Status ==
                    LogicalResourceLeaseAcquireStatus.Acquired;
            string reason = acquired ? string.Empty : Failure(
                serviceResult != null ? serviceResult.FailureReason : "",
                "Logical lease acquire was rejected.");
            string leaseId = acquired && serviceResult != null
                ? serviceResult.LeaseId : string.Empty;
            ExecutionCoordinatorEvent coordinatorEvent = CreateEvent(
                intent, eventId, eventSource, generation,
                acquired ? ExecutionCoordinatorEventType.LeaseAcquired
                    : ExecutionCoordinatorEventType.LeaseRejected,
                leaseId,
                acquired ? ExecutionResourceLeaseStatus.Acquired
                    : ExecutionResourceLeaseStatus.Rejected,
                reason);

            if (!EventValid(coordinatorEvent, intent.PlanId))
                return Result(intent, decision, true, state, serviceResult,
                    null, null,
                    ExecutionLogicalLeaseDispatchFailureCodes.EventInvalid);

            return Result(intent, decision, true, transition.State,
                serviceResult, null, coordinatorEvent, reason);
        }

        private ExecutionLogicalLeaseDispatchResult DispatchRelease(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionAuthorityDecision decision,
            LogicalResourceLeaseState state,
            bool releaseConfirmed,
            string releaseFailureReason,
            int generation,
            string eventId,
            string eventSource)
        {
            ExecutionResourceLeaseIntentPayload payload =
                intent.ResourceLease;
            var request = new ResourceLeaseReleaseRequest(
                payload.LeaseId, intent.PlanId, intent.StepId,
                intent.RequestId, payload.ResourceId,
                intent.CreatedAtUtc, generation, releaseConfirmed,
                releaseFailureReason);

            LogicalResourceLeaseTransition<ResourceLeaseReleaseResult>
                transition = leaseService.Release(state, request);
            ResourceLeaseReleaseResult serviceResult = transition.Result;
            bool released = serviceResult != null &&
                serviceResult.Status ==
                    LogicalResourceLeaseReleaseStatus.Released;
            string reason = released ? string.Empty : Failure(
                serviceResult != null ? serviceResult.FailureReason : "",
                "Logical lease release failed.");
            ExecutionCoordinatorEvent coordinatorEvent = CreateEvent(
                intent, eventId, eventSource, generation,
                released ? ExecutionCoordinatorEventType.LeaseReleased
                    : ExecutionCoordinatorEventType.LeaseReleaseFailed,
                payload.LeaseId,
                released ? ExecutionResourceLeaseStatus.Released
                    : ExecutionResourceLeaseStatus.ReleaseFailed,
                reason);

            if (!EventValid(coordinatorEvent, intent.PlanId))
                return Result(intent, decision, true, state, null,
                    serviceResult, null,
                    ExecutionLogicalLeaseDispatchFailureCodes.EventInvalid);

            return Result(intent, decision, true, transition.State, null,
                serviceResult, coordinatorEvent, reason);
        }

        private static ExecutionAuthorityRequest CreateAuthorityRequest(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionDomain stepDomain)
        {
            if (intent == null) return null;
            ExecutionAuthorityDomain domain;
            if (!ExecutionAuthorityIntentDomainMapper.TryMap(
                    intent.IntentType, stepDomain, out domain))
                domain = ExecutionAuthorityDomain.Unknown;
            return new ExecutionAuthorityRequest(
                intent.PlanId, intent.StepId,
                intent.ExecutionAttemptId, intent.RequestId, domain,
                intent.IntentType, string.Empty, string.Empty,
                intent.ResourceLease != null
                    ? intent.ResourceLease.ResourceId : string.Empty,
                ExecutionControlScope.None, false,
                intent.CreatedAtUtc);
        }

        private static string ValidateDispatchInput(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionAuthorityDecision decision,
            LogicalResourceLeaseState state,
            int generation,
            string eventId,
            string eventSource)
        {
            if (intent == null || decision == null || state == null ||
                decision.Domain !=
                    ExecutionAuthorityDomain.LogicalResourceLease ||
                generation < 0 || state.Generation != generation ||
                string.IsNullOrWhiteSpace(eventId) ||
                string.IsNullOrWhiteSpace(eventSource) ||
                string.IsNullOrWhiteSpace(intent.IntentId) ||
                string.IsNullOrWhiteSpace(intent.PlanId) ||
                string.IsNullOrWhiteSpace(intent.StepId) ||
                string.IsNullOrWhiteSpace(intent.RequestId) ||
                intent.CreatedAtUtc == default(DateTime) ||
                intent.ResourceLease == null ||
                string.IsNullOrWhiteSpace(intent.ResourceLease.LeaseId) ||
                string.IsNullOrWhiteSpace(intent.ResourceLease.ResourceId) ||
                string.IsNullOrWhiteSpace(intent.ResourceLease.ResourceType) ||
                !Enum.IsDefined(typeof(ExecutionResourceAccessMode),
                    intent.ResourceLease.AccessMode))
                return ExecutionLogicalLeaseDispatchFailureCodes.InvalidInput;

            if (intent.IntentType !=
                    ExecutionCoordinatorEffectIntentType
                        .AcquireResourceLease &&
                intent.IntentType !=
                    ExecutionCoordinatorEffectIntentType
                        .ReleaseResourceLease)
                return ExecutionLogicalLeaseDispatchFailureCodes
                    .UnsupportedEffect;

            return string.Empty;
        }

        private static ExecutionCoordinatorEvent CreateEvent(
            ExecutionCoordinatorEffectIntent intent,
            string eventId,
            string eventSource,
            int generation,
            ExecutionCoordinatorEventType eventType,
            string leaseId,
            ExecutionResourceLeaseStatus leaseStatus,
            string reason)
        {
            var lease = new ResourceLeaseReference(
                leaseId, intent.ResourceLease.ResourceId,
                intent.ResourceLease.ResourceType, intent.PlanId,
                intent.StepId, intent.ExecutionAttemptId,
                intent.ResourceLease.AccessMode, intent.CreatedAtUtc,
                leaseStatus);
            return new ExecutionCoordinatorEvent(
                eventId, eventType, intent.PlanId, intent.StepId,
                intent.ExecutionAttemptId, string.Empty,
                intent.CreatedAtUtc, eventSource, generation, null,
                ExecutionTerminalOutcome.None,
                string.IsNullOrEmpty(reason)
                    ? new string[0] : new[] { reason },
                reason,
                new ExecutionResourceLeaseEventPayload(lease, reason));
        }

        private static bool EventValid(
            ExecutionCoordinatorEvent coordinatorEvent,
            string expectedPlanId)
        {
            ExecutionCoordinatorContractValidationResult validation =
                ExecutionCoordinatorContractValidator.ValidateEvent(
                    coordinatorEvent, expectedPlanId);
            return validation != null && validation.IsValid;
        }

        private static string Failure(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static ExecutionLogicalLeaseDispatchResult Result(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionAuthorityDecision decision,
            bool serviceCalled,
            LogicalResourceLeaseState state,
            ResourceLeaseAcquireResult acquireResult,
            ResourceLeaseReleaseResult releaseResult,
            ExecutionCoordinatorEvent coordinatorEvent,
            string failureReason)
        {
            return new ExecutionLogicalLeaseDispatchResult(intent, decision,
                serviceCalled, state, acquireResult, releaseResult,
                coordinatorEvent, failureReason);
        }
    }
}
