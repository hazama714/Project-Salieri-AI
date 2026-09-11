// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Authority
{
    public enum ExecutionResourceAvailabilityLeaseDisposition
    {
        Invalid = 0,
        AuthorityBlocked = 1,
        Available = 2,
        Unavailable = 3,
        Unknown = 4,
        Failed = 5
    }

    /// <summary>
    /// Audit record for the read-only availability check performed before a
    /// logical lease acquire. Availability and ownership remain separate:
    /// an Available result only permits the logical lease dispatcher to try.
    /// </summary>
    public sealed class ExecutionResourceAvailabilityLeaseResult
    {
        public ExecutionCoordinatorEffectIntent Intent { get; }
        public ExecutionAuthorityDecision LogicalLeaseAuthority { get; }
        public ExecutionAuthorityDecision ResourceProfileAuthority { get; }
        public ResourceAcquireRequest AvailabilityRequest { get; }
        public ResourceShadowAvailabilityResult AvailabilityResult { get; }
        public bool AvailabilityQueried { get; }
        public ExecutionCoordinatorEvent CoordinatorEvent { get; }
        public ExecutionResourceAvailabilityLeaseDisposition Disposition
            { get; }
        public string FailureReason { get; }

        public bool MayDispatchLogicalLease =>
            Disposition ==
                ExecutionResourceAvailabilityLeaseDisposition.Available;

        public ExecutionResourceAvailabilityLeaseResult(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionAuthorityDecision logicalLeaseAuthority,
            ExecutionAuthorityDecision resourceProfileAuthority,
            ResourceAcquireRequest availabilityRequest,
            ResourceShadowAvailabilityResult availabilityResult,
            bool availabilityQueried,
            ExecutionCoordinatorEvent coordinatorEvent,
            ExecutionResourceAvailabilityLeaseDisposition disposition,
            string failureReason)
        {
            Intent = intent;
            LogicalLeaseAuthority = logicalLeaseAuthority;
            ResourceProfileAuthority = resourceProfileAuthority;
            AvailabilityRequest = availabilityRequest;
            AvailabilityResult = availabilityResult;
            AvailabilityQueried = availabilityQueried;
            CoordinatorEvent = coordinatorEvent;
            Disposition = disposition;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    /// <summary>
    /// Phase 3D-4B correlation boundary. It authorizes and performs exactly
    /// one read-only ResourceProfile query for an acquire intent, then emits
    /// the corresponding availability fact for every formal status. It never
    /// mutates logical lease ownership and never interprets InteractionState.
    /// </summary>
    public sealed class ExecutionResourceAvailabilityLeaseCorrelation
    {
        public const string CorrelationVersion =
            "execution-resource-availability-lease-correlation-3d4b.0";

        private readonly ExecutionLimitedLiveAuthorityGate authorityGate;
        private readonly ExecutionResourceRuntimeAdapter resourceAdapter;

        public ExecutionResourceAvailabilityLeaseCorrelation(
            ExecutionResourceRuntimeAdapter resourceAdapter)
            : this(new ExecutionLimitedLiveAuthorityGate(), resourceAdapter)
        {
        }

        internal ExecutionResourceAvailabilityLeaseCorrelation(
            ExecutionLimitedLiveAuthorityGate authorityGate,
            ExecutionResourceRuntimeAdapter resourceAdapter)
        {
            this.authorityGate = authorityGate ??
                new ExecutionLimitedLiveAuthorityGate();
            this.resourceAdapter = resourceAdapter;
        }

        public ExecutionResourceAvailabilityLeaseResult Evaluate(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionDomain stepDomain,
            ExecutionAuthorityPolicySnapshot policy,
            ExecutionRuntimeCapabilitySnapshot capability,
            ExecutionAuthorityEvaluationContext authorityContext,
            DateTime observedAtUtc,
            int generation,
            string eventId,
            string eventSource,
            string resourceRequestId,
            string resourceWaitCycleId)
        {
            if (intent == null || intent.IntentType !=
                    ExecutionCoordinatorEffectIntentType
                        .AcquireResourceLease ||
                intent.ResourceLease == null ||
                string.IsNullOrWhiteSpace(intent.RequestId) ||
                string.IsNullOrWhiteSpace(eventId) ||
                string.IsNullOrWhiteSpace(eventSource) ||
                string.IsNullOrWhiteSpace(resourceRequestId) ||
                string.IsNullOrWhiteSpace(resourceWaitCycleId) ||
                observedAtUtc == default(DateTime) || generation < 0)
                return Result(intent, null, null, null, null, false, null,
                    ExecutionResourceAvailabilityLeaseDisposition.Invalid,
                    "RESOURCE_AVAILABILITY_CORRELATION_INVALID_INPUT");

            ExecutionAuthorityDecision logicalAuthority =
                authorityGate.Evaluate(policy,
                    CreateLogicalLeaseAuthorityRequest(intent, stepDomain),
                    capability, authorityContext);
            if (!AllowsDispatch(logicalAuthority))
                return Result(intent, logicalAuthority, null, null, null,
                    false, null,
                    ExecutionResourceAvailabilityLeaseDisposition
                        .AuthorityBlocked,
                    Reason(logicalAuthority,
                        "LOGICAL_LEASE_NOT_AUTHORIZED"));

            ExecutionAuthorityDecision profileAuthority =
                authorityGate.Evaluate(policy,
                    CreateResourceProfileAuthorityRequest(intent),
                    capability, authorityContext);
            if (!AllowsRead(profileAuthority))
                return Result(intent, logicalAuthority, profileAuthority,
                    null, null, false, null,
                    ExecutionResourceAvailabilityLeaseDisposition
                        .AuthorityBlocked,
                    Reason(profileAuthority,
                        "RESOURCE_PROFILE_READ_NOT_AUTHORIZED"));

            AdapterTranslationResult<ResourceAcquireRequest> translated =
                ExecutionRuntimeAdapterTranslator.FromAcquireIntent(
                    intent, intent.RequestId);
            if (translated == null || !translated.IsValid ||
                translated.Value == null)
                return Result(intent, logicalAuthority, profileAuthority,
                    null, null, false, null,
                    ExecutionResourceAvailabilityLeaseDisposition.Invalid,
                    translated != null ? FirstFailure(translated) :
                        "RESOURCE_ACQUIRE_TRANSLATION_FAILED");

            ResourceAcquireRequest request = translated.Value;
            ResourceShadowAvailabilityResult availability =
                resourceAdapter != null
                    ? resourceAdapter.QueryAvailability(
                        request, observedAtUtc)
                    : FailedAvailability(request, observedAtUtc,
                        "RESOURCE_ADAPTER_UNAVAILABLE");

            string correlationFailure =
                ResourceShadowContractValidator.ValidateCorrelation(
                    request, availability);
            if (!string.IsNullOrEmpty(correlationFailure))
                return Result(intent, logicalAuthority, profileAuthority,
                    request, availability, true, null,
                    ExecutionResourceAvailabilityLeaseDisposition.Failed,
                    correlationFailure);

            switch (availability.Status)
            {
                case ResourceShadowAvailabilityStatus.Available:
                    ExecutionCoordinatorEvent availableEvent =
                        CreateAvailabilityEvent(intent, request,
                            availability, generation, eventId, eventSource,
                            resourceRequestId, resourceWaitCycleId);
                    return Result(intent, logicalAuthority,
                        profileAuthority, request, availability,
                        resourceAdapter != null, availableEvent,
                        ExecutionResourceAvailabilityLeaseDisposition
                            .Available,
                        availability.DecisionReason);
                case ResourceShadowAvailabilityStatus.Unavailable:
                    ExecutionCoordinatorEvent unavailableEvent =
                        CreateAvailabilityEvent(intent, request,
                            availability, generation, eventId, eventSource,
                            resourceRequestId, resourceWaitCycleId);
                    return Result(intent, logicalAuthority,
                        profileAuthority, request, availability, true,
                        unavailableEvent,
                        ExecutionResourceAvailabilityLeaseDisposition
                            .Unavailable,
                        availability.DecisionReason);
                case ResourceShadowAvailabilityStatus.Unknown:
                    ExecutionCoordinatorEvent unknownEvent =
                        CreateAvailabilityEvent(intent, request,
                            availability, generation, eventId, eventSource,
                            resourceRequestId, resourceWaitCycleId);
                    return Result(intent, logicalAuthority,
                        profileAuthority, request, availability, true,
                        unknownEvent,
                        ExecutionResourceAvailabilityLeaseDisposition.Unknown,
                        availability.DecisionReason);
                case ResourceShadowAvailabilityStatus.Failed:
                default:
                    ExecutionCoordinatorEvent failedEvent =
                        CreateAvailabilityEvent(intent, request,
                            availability, generation, eventId, eventSource,
                            resourceRequestId, resourceWaitCycleId);
                    return Result(intent, logicalAuthority,
                        profileAuthority, request, availability,
                        resourceAdapter != null, failedEvent,
                        ExecutionResourceAvailabilityLeaseDisposition.Failed,
                        availability.DecisionReason);
            }
        }

        private static ExecutionCoordinatorEvent CreateAvailabilityEvent(
            ExecutionCoordinatorEffectIntent intent,
            ResourceAcquireRequest request,
            ResourceShadowAvailabilityResult availability,
            int generation,
            string eventId,
            string eventSource,
            string resourceRequestId,
            string resourceWaitCycleId)
        {
            string triggerId = intent != null &&
                !string.IsNullOrWhiteSpace(intent.IntentId)
                    ? intent.IntentId : eventId;
            var payload = new ExecutionResourceAvailabilityEventPayload(
                request.ResourceId, availability.Status,
                "resource-profile:" + eventId,
                availability.ObservedAtUtc, resourceRequestId, triggerId,
                resourceWaitCycleId, availability.DecisionReason);
            return new ExecutionCoordinatorEvent(
                eventId,
                ExecutionCoordinatorEventType.ResourceAvailabilityEvaluated,
                request.PlanId, request.StepId, string.Empty, string.Empty,
                availability.ObservedAtUtc, eventSource, generation, null,
                ExecutionTerminalOutcome.None, new string[0],
                availability.DecisionReason, null, payload, null);
        }

        private static ResourceShadowAvailabilityResult FailedAvailability(
            ResourceAcquireRequest request,
            DateTime observedAtUtc,
            string reason)
        {
            return new ResourceShadowAvailabilityResult(
                request.RequestId, request.PlanId, request.StepId,
                request.ExecutionAttemptId, request.ResourceId,
                request.ResourceType, request.AccessMode,
                ResourceShadowAvailabilityStatus.Failed, reason,
                observedAtUtc);
        }

        private static ExecutionAuthorityRequest
            CreateLogicalLeaseAuthorityRequest(
                ExecutionCoordinatorEffectIntent intent,
                ExecutionDomain stepDomain)
        {
            ExecutionAuthorityDomain domain;
            if (!ExecutionAuthorityIntentDomainMapper.TryMap(
                    intent.IntentType, stepDomain, out domain))
                domain = ExecutionAuthorityDomain.Unknown;
            return new ExecutionAuthorityRequest(
                intent.PlanId, intent.StepId, intent.ExecutionAttemptId,
                intent.RequestId, domain, intent.IntentType, string.Empty,
                string.Empty, intent.ResourceLease.ResourceId,
                ExecutionControlScope.None, false, intent.CreatedAtUtc);
        }

        private static ExecutionAuthorityRequest
            CreateResourceProfileAuthorityRequest(
                ExecutionCoordinatorEffectIntent intent)
        {
            return new ExecutionAuthorityRequest(
                intent.PlanId, intent.StepId, intent.ExecutionAttemptId,
                intent.RequestId, ExecutionAuthorityDomain.ResourceProfile,
                ExecutionCoordinatorEffectIntentType.None, string.Empty,
                string.Empty, intent.ResourceLease.ResourceId,
                ExecutionControlScope.None, true, intent.CreatedAtUtc);
        }

        private static bool AllowsDispatch(
            ExecutionAuthorityDecision decision)
        {
            return decision != null && decision.DispatchAllowed &&
                decision.Disposition ==
                    ExecutionAuthorityDisposition.AllowedDispatch &&
                decision.EffectiveMode ==
                    ExecutionAuthorityMode.LiveDispatch;
        }

        private static bool AllowsRead(ExecutionAuthorityDecision decision)
        {
            return decision != null && decision.ReadOnlyAllowed &&
                decision.Disposition ==
                    ExecutionAuthorityDisposition.AllowedReadOnly &&
                decision.EffectiveMode ==
                    ExecutionAuthorityMode.LiveReadOnly;
        }

        private static string Reason(
            ExecutionAuthorityDecision decision,
            string fallback)
        {
            return decision != null &&
                !string.IsNullOrWhiteSpace(decision.DenialReason)
                    ? decision.DenialReason : fallback;
        }

        private static string FirstFailure<T>(
            AdapterTranslationResult<T> result) where T : class
        {
            return result != null && result.FailureCodes.Count > 0
                ? result.FailureCodes[0]
                : "RESOURCE_ADAPTER_TRANSLATION_FAILED";
        }

        private static ExecutionResourceAvailabilityLeaseResult Result(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionAuthorityDecision logicalAuthority,
            ExecutionAuthorityDecision profileAuthority,
            ResourceAcquireRequest request,
            ResourceShadowAvailabilityResult availability,
            bool queried,
            ExecutionCoordinatorEvent coordinatorEvent,
            ExecutionResourceAvailabilityLeaseDisposition disposition,
            string failureReason)
        {
            return new ExecutionResourceAvailabilityLeaseResult(
                intent, logicalAuthority, profileAuthority, request,
                availability, queried, coordinatorEvent, disposition,
                failureReason);
        }
    }
}
