// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Integration;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Validation
{
    public static class ExecutionCoordinatorContractFailureCodes
    {
        public const string NullEvent = "NullEvent";
        public const string NullIntent = "NullIntent";
        public const string MissingEventId = "MissingEventId";
        public const string MissingIntentId = "MissingIntentId";
        public const string InvalidEventType = "InvalidEventType";
        public const string InvalidIntentType = "InvalidIntentType";
        public const string MissingPlanId = "MissingPlanId";
        public const string PlanIdMismatch = "PlanIdMismatch";
        public const string MissingStepId = "MissingStepId";
        public const string MissingAttemptId = "MissingAttemptId";
        public const string MissingRequestId = "MissingRequestId";
        public const string MissingControlRequestId =
            "MissingControlRequestId";
        public const string MissingDomainStage =
            "MissingDomainStage";
        public const string MissingTerminalOutcome =
            "MissingTerminalOutcome";
        public const string UnexpectedTerminalOutcome =
            "UnexpectedTerminalOutcome";
        public const string InvalidGeneration = "InvalidGeneration";
        public const string MissingResourceRequirement =
            "MissingResourceRequirement";
        public const string InvalidResourceRequirement =
            "InvalidResourceRequirement";
        public const string ConflictingResourceAccess =
            "ConflictingResourceAccess";
        public const string MissingTimeoutPolicy =
            "MissingTimeoutPolicy";
        public const string MissingLeasePayload = "MissingLeasePayload";
        public const string MissingLeaseId = "MissingLeaseId";
        public const string MissingResourceId = "MissingResourceId";
        public const string MissingResourceType = "MissingResourceType";
        public const string LeasePlanMismatch = "LeasePlanMismatch";
        public const string LeaseStepMismatch = "LeaseStepMismatch";
        public const string LeaseStatusMismatch = "LeaseStatusMismatch";
        public const string LeaseResourceMismatch =
            "LeaseResourceMismatch";
        public const string LeaseAccessModeMismatch =
            "LeaseAccessModeMismatch";
        public const string MissingLeaseReason = "MissingLeaseReason";
        public const string LeaseEventIdCollision =
            "LeaseEventIdCollision";
        public const string MissingAvailabilityPayload =
            "MissingAvailabilityPayload";
        public const string InvalidAvailabilityStatus =
            "InvalidAvailabilityStatus";
        public const string MissingSnapshotRevision =
            "MissingSnapshotRevision";
        public const string MissingTriggerId = "MissingTriggerId";
        public const string MissingResourceWaitCycleId =
            "MissingResourceWaitCycleId";
        public const string MissingObservedAtUtc =
            "MissingObservedAtUtc";
        public const string MissingResourceWaitTimeoutPayload =
            "MissingResourceWaitTimeoutPayload";
        public const string MissingEvaluationTimeUtc =
            "MissingEvaluationTimeUtc";
        public const string MissingResourceWaitDeadlineUtc =
            "MissingResourceWaitDeadlineUtc";
        public const string UnexpectedAttemptId =
            "UnexpectedAttemptId";
        public const string UnexpectedResourceWaitPayload =
            "UnexpectedResourceWaitPayload";
        public const string InternalValidationError =
            "InternalValidationError";
    }

    public sealed class ExecutionCoordinatorContractValidationResult
    {
        public string ValidatorVersion { get; }
        public bool IsValid { get; }
        public IReadOnlyList<string> FailureCodes { get; }
        public IReadOnlyList<string> InvalidFields { get; }
        public string DiagnosticMessage { get; }

        internal ExecutionCoordinatorContractValidationResult(
            string validatorVersion,
            IEnumerable<string> failureCodes,
            IEnumerable<string> invalidFields,
            string diagnosticMessage)
        {
            ValidatorVersion = validatorVersion ?? string.Empty;
            FailureCodes = Copy(failureCodes);
            InvalidFields = Copy(invalidFields);
            DiagnosticMessage = diagnosticMessage ?? string.Empty;
            IsValid = FailureCodes.Count == 0;
        }

        public bool ContainsFailure(string failureCode)
        {
            for (int i = 0; i < FailureCodes.Count; i++)
            {
                if (FailureCodes[i] == failureCode)
                    return true;
            }

            return false;
        }

        private static IReadOnlyList<string> Copy(
            IEnumerable<string> source)
        {
            return new ReadOnlyCollection<string>(
                source != null
                    ? new List<string>(source)
                    : new List<string>()
            );
        }
    }

    /// <summary>
    /// Pure validation for reducer input and output contracts. It never
    /// changes an event or intent and never executes an effect.
    /// </summary>
    public static class ExecutionCoordinatorContractValidator
    {
        public const string ValidatorVersion =
            "execution-coordinator-contract-3c0a.1";

        public static ExecutionCoordinatorContractValidationResult
            ValidateEvent(
                ExecutionCoordinatorEvent coordinatorEvent,
                string expectedPlanId)
        {
            Issues issues = new Issues();

            try
            {
                if (coordinatorEvent == null)
                {
                    issues.Add(
                        ExecutionCoordinatorContractFailureCodes
                            .NullEvent,
                        "event"
                    );
                }
                else
                {
                    Required(
                        coordinatorEvent.EventId,
                        ExecutionCoordinatorContractFailureCodes
                            .MissingEventId,
                        "eventId",
                        issues
                    );
                    RequiredPlan(
                        coordinatorEvent.PlanId,
                        expectedPlanId,
                        issues
                    );

                    if (coordinatorEvent.EventType ==
                            ExecutionCoordinatorEventType.None ||
                        !Enum.IsDefined(
                            typeof(ExecutionCoordinatorEventType),
                            coordinatorEvent.EventType))
                    {
                        issues.Add(
                            ExecutionCoordinatorContractFailureCodes
                                .InvalidEventType,
                            "eventType"
                        );
                    }

                    if (coordinatorEvent.Generation < 0)
                    {
                        issues.Add(
                            ExecutionCoordinatorContractFailureCodes
                                .InvalidGeneration,
                            "generation"
                        );
                    }

                    ValidateEventIds(coordinatorEvent, issues);
                    ValidateEventPayload(coordinatorEvent, issues);
                }
            }
            catch (Exception exception)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .InternalValidationError,
                    "event"
                );
                return Result(
                    issues,
                    exception.GetType().Name + ": " +
                    exception.Message
                );
            }

            return Result(issues, "Event validation completed.");
        }

        public static ExecutionCoordinatorContractValidationResult
            ValidateIntent(
                ExecutionCoordinatorEffectIntent intent,
                string expectedPlanId)
        {
            Issues issues = new Issues();

            try
            {
                if (intent == null)
                {
                    issues.Add(
                        ExecutionCoordinatorContractFailureCodes
                            .NullIntent,
                        "intent"
                    );
                }
                else
                {
                    Required(
                        intent.IntentId,
                        ExecutionCoordinatorContractFailureCodes
                            .MissingIntentId,
                        "intentId",
                        issues
                    );
                    RequiredPlan(
                        intent.PlanId,
                        expectedPlanId,
                        issues
                    );

                    if (intent.IntentType ==
                            ExecutionCoordinatorEffectIntentType.None ||
                        !Enum.IsDefined(
                            typeof(
                                ExecutionCoordinatorEffectIntentType),
                            intent.IntentType))
                    {
                        issues.Add(
                            ExecutionCoordinatorContractFailureCodes
                                .InvalidIntentType,
                            "intentType"
                        );
                    }

                    ValidateIntentIds(intent, issues);
                    ValidateIntentPayload(intent, issues);
                }
            }
            catch (Exception exception)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .InternalValidationError,
                    "intent"
                );
                return Result(
                    issues,
                    exception.GetType().Name + ": " +
                    exception.Message
                );
            }

            return Result(issues, "Effect intent validation completed.");
        }

        private static void ValidateEventIds(
            ExecutionCoordinatorEvent coordinatorEvent,
            Issues issues)
        {
            ExecutionCoordinatorEventType type =
                coordinatorEvent.EventType;

            if (RequiresEventStep(type))
            {
                Required(
                    coordinatorEvent.StepId,
                    ExecutionCoordinatorContractFailureCodes
                        .MissingStepId,
                    "stepId",
                    issues
                );
            }

            if (RequiresEventAttempt(type))
            {
                Required(
                    coordinatorEvent.ExecutionAttemptId,
                    ExecutionCoordinatorContractFailureCodes
                        .MissingAttemptId,
                    "executionAttemptId",
                    issues
                );
            }

            if (IsControlEvent(type))
            {
                Required(
                    coordinatorEvent.ControlRequestId,
                    ExecutionCoordinatorContractFailureCodes
                        .MissingControlRequestId,
                    "controlRequestId",
                    issues
                );
            }
        }

        private static void ValidateEventPayload(
            ExecutionCoordinatorEvent coordinatorEvent,
            Issues issues)
        {
            if (IsLeaseEvent(coordinatorEvent.EventType))
                ValidateLeaseEvent(coordinatorEvent, issues);

            if (coordinatorEvent.EventType ==
                ExecutionCoordinatorEventType.ResourceAvailabilityEvaluated)
            {
                ValidateAvailabilityEvent(coordinatorEvent, issues);
            }

            if (coordinatorEvent.EventType ==
                ExecutionCoordinatorEventType.ResourceWaitTimedOut)
            {
                ValidateResourceWaitTimeoutEvent(coordinatorEvent, issues);
            }

            if (coordinatorEvent.EventType !=
                    ExecutionCoordinatorEventType
                        .ResourceAvailabilityEvaluated &&
                coordinatorEvent.ResourceAvailability != null)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .UnexpectedResourceWaitPayload,
                    "resourceAvailability"
                );
            }

            if (coordinatorEvent.EventType !=
                    ExecutionCoordinatorEventType.ResourceWaitTimedOut &&
                coordinatorEvent.ResourceWaitTimeout != null)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .UnexpectedResourceWaitPayload,
                    "resourceWaitTimeout"
                );
            }

            if (coordinatorEvent.EventType ==
                    ExecutionCoordinatorEventType.DomainStageChanged &&
                (coordinatorEvent.DomainStage == null ||
                 string.IsNullOrWhiteSpace(
                     coordinatorEvent.DomainStage.StageId)))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .MissingDomainStage,
                    "domainStage"
                );
            }

            if (coordinatorEvent.EventType ==
                ExecutionCoordinatorEventType.ExecutorTerminal)
            {
                if (coordinatorEvent.TerminalOutcome ==
                    ExecutionTerminalOutcome.None)
                {
                    issues.Add(
                        ExecutionCoordinatorContractFailureCodes
                            .MissingTerminalOutcome,
                        "terminalOutcome"
                    );
                }
            }
            else if (coordinatorEvent.TerminalOutcome !=
                     ExecutionTerminalOutcome.None)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .UnexpectedTerminalOutcome,
                    "terminalOutcome"
                );
            }
        }

        private static void ValidateIntentIds(
            ExecutionCoordinatorEffectIntent intent,
            Issues issues)
        {
            ExecutionCoordinatorEffectIntentType type =
                intent.IntentType;

            if (RequiresIntentStep(type))
            {
                Required(
                    intent.StepId,
                    ExecutionCoordinatorContractFailureCodes
                        .MissingStepId,
                    "stepId",
                    issues
                );
            }

            if (type ==
                ExecutionCoordinatorEffectIntentType.StartExecutor)
            {
                Required(
                    intent.ExecutionAttemptId,
                    ExecutionCoordinatorContractFailureCodes
                        .MissingAttemptId,
                    "executionAttemptId",
                    issues
                );
                Required(
                    intent.RequestId,
                    ExecutionCoordinatorContractFailureCodes
                        .MissingRequestId,
                    "requestId",
                    issues
                );
            }

            if (IsControlIntent(type))
            {
                Required(
                    intent.ControlRequestId,
                    ExecutionCoordinatorContractFailureCodes
                        .MissingControlRequestId,
                    "controlRequestId",
                    issues
                );
            }
        }

        private static void ValidateIntentPayload(
            ExecutionCoordinatorEffectIntent intent,
            Issues issues)
        {
            if (IsResourceIntent(intent.IntentType))
            {
                if (intent.ResourceRequirements.Count == 0)
                {
                    issues.Add(
                        ExecutionCoordinatorContractFailureCodes
                            .MissingResourceRequirement,
                        "resourceRequirements"
                    );
                }

                ValidateResources(intent.ResourceRequirements, issues);
                ValidateLeaseIntent(intent, issues);
            }

            if (intent.IntentType ==
                    ExecutionCoordinatorEffectIntentType
                        .StartTimeoutWatch &&
                (intent.TimeoutPolicy == null ||
                 intent.TimeoutPolicy.TimeoutSeconds <= 0d))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .MissingTimeoutPolicy,
                    "timeoutPolicy"
                );
            }
        }

        private static void ValidateLeaseEvent(
            ExecutionCoordinatorEvent coordinatorEvent,
            Issues issues)
        {
            ExecutionResourceLeaseEventPayload payload =
                coordinatorEvent.ResourceLease;
            ResourceLeaseReference lease =
                payload != null ? payload.Lease : null;
            if (lease == null)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .MissingLeasePayload,
                    "resourceLease"
                );
                return;
            }

            Required(
                lease.ResourceId,
                ExecutionCoordinatorContractFailureCodes
                    .MissingResourceId,
                "resourceLease.lease.resourceId",
                issues
            );
            Required(
                lease.ResourceType,
                ExecutionCoordinatorContractFailureCodes
                    .MissingResourceType,
                "resourceLease.lease.resourceType",
                issues
            );
            if (!string.Equals(
                    lease.PlanId,
                    coordinatorEvent.PlanId,
                    StringComparison.Ordinal))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .LeasePlanMismatch,
                    "resourceLease.lease.planId"
                );
            }
            if (!string.Equals(
                    lease.StepId,
                    coordinatorEvent.StepId,
                    StringComparison.Ordinal))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .LeaseStepMismatch,
                    "resourceLease.lease.stepId"
                );
            }

            ExecutionResourceLeaseStatus expectedStatus =
                ExpectedLeaseStatus(coordinatorEvent.EventType);
            if (lease.Status != expectedStatus)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .LeaseStatusMismatch,
                    "resourceLease.lease.status"
                );
            }

            if (coordinatorEvent.EventType !=
                    ExecutionCoordinatorEventType.LeaseRejected)
            {
                Required(
                    lease.LeaseId,
                    ExecutionCoordinatorContractFailureCodes
                        .MissingLeaseId,
                    "resourceLease.lease.leaseId",
                    issues
                );
                if (string.Equals(
                        lease.LeaseId,
                        coordinatorEvent.EventId,
                        StringComparison.Ordinal))
                {
                    issues.Add(
                        ExecutionCoordinatorContractFailureCodes
                            .LeaseEventIdCollision,
                        "resourceLease.lease.leaseId"
                    );
                }
            }

            if ((coordinatorEvent.EventType ==
                    ExecutionCoordinatorEventType.LeaseRejected ||
                 coordinatorEvent.EventType ==
                    ExecutionCoordinatorEventType.LeaseReleaseFailed) &&
                string.IsNullOrWhiteSpace(payload.Reason))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .MissingLeaseReason,
                    "resourceLease.reason"
                );
            }
        }

        private static void ValidateAvailabilityEvent(
            ExecutionCoordinatorEvent coordinatorEvent,
            Issues issues)
        {
            ExecutionResourceAvailabilityEventPayload payload =
                coordinatorEvent.ResourceAvailability;
            if (payload == null)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .MissingAvailabilityPayload,
                    "resourceAvailability"
                );
                return;
            }

            Required(
                payload.ResourceId,
                ExecutionCoordinatorContractFailureCodes.MissingResourceId,
                "resourceAvailability.resourceId",
                issues
            );
            Required(
                payload.SnapshotRevision,
                ExecutionCoordinatorContractFailureCodes
                    .MissingSnapshotRevision,
                "resourceAvailability.snapshotRevision",
                issues
            );
            Required(
                payload.RequestId,
                ExecutionCoordinatorContractFailureCodes.MissingRequestId,
                "resourceAvailability.requestId",
                issues
            );
            Required(
                payload.TriggerId,
                ExecutionCoordinatorContractFailureCodes.MissingTriggerId,
                "resourceAvailability.triggerId",
                issues
            );
            Required(
                payload.ResourceWaitCycleId,
                ExecutionCoordinatorContractFailureCodes
                    .MissingResourceWaitCycleId,
                "resourceAvailability.resourceWaitCycleId",
                issues
            );
            if (!Enum.IsDefined(
                    typeof(ResourceShadowAvailabilityStatus),
                    payload.Availability))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .InvalidAvailabilityStatus,
                    "resourceAvailability.availability"
                );
            }
            if (payload.ObservedAtUtc == default(DateTime))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .MissingObservedAtUtc,
                    "resourceAvailability.observedAtUtc"
                );
            }
            if (!string.IsNullOrEmpty(
                    coordinatorEvent.ExecutionAttemptId))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .UnexpectedAttemptId,
                    "executionAttemptId"
                );
            }
        }

        private static void ValidateResourceWaitTimeoutEvent(
            ExecutionCoordinatorEvent coordinatorEvent,
            Issues issues)
        {
            ExecutionResourceWaitTimeoutEventPayload payload =
                coordinatorEvent.ResourceWaitTimeout;
            if (payload == null)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .MissingResourceWaitTimeoutPayload,
                    "resourceWaitTimeout"
                );
                return;
            }

            Required(
                payload.ResourceWaitCycleId,
                ExecutionCoordinatorContractFailureCodes
                    .MissingResourceWaitCycleId,
                "resourceWaitTimeout.resourceWaitCycleId",
                issues
            );
            if (payload.EvaluationTimeUtc == default(DateTime))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .MissingEvaluationTimeUtc,
                    "resourceWaitTimeout.evaluationTimeUtc"
                );
            }
            if (payload.DeadlineUtc == default(DateTime))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .MissingResourceWaitDeadlineUtc,
                    "resourceWaitTimeout.deadlineUtc"
                );
            }
            if (!string.IsNullOrEmpty(
                    coordinatorEvent.ExecutionAttemptId))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .UnexpectedAttemptId,
                    "executionAttemptId"
                );
            }
        }

        private static void ValidateLeaseIntent(
            ExecutionCoordinatorEffectIntent intent,
            Issues issues)
        {
            ExecutionResourceLeaseIntentPayload payload =
                intent.ResourceLease;
            if (payload == null)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .MissingLeasePayload,
                    "resourceLease"
                );
                return;
            }

            Required(
                payload.ResourceId,
                ExecutionCoordinatorContractFailureCodes
                    .MissingResourceId,
                "resourceLease.resourceId",
                issues
            );
            Required(
                payload.ResourceType,
                ExecutionCoordinatorContractFailureCodes
                    .MissingResourceType,
                "resourceLease.resourceType",
                issues
            );
            if (intent.IntentType ==
                    ExecutionCoordinatorEffectIntentType
                        .ReleaseResourceLease)
            {
                Required(
                    payload.LeaseId,
                    ExecutionCoordinatorContractFailureCodes
                        .MissingLeaseId,
                    "resourceLease.leaseId",
                    issues
                );
            }

            bool match = false;
            for (int i = 0; i < intent.ResourceRequirements.Count; i++)
            {
                ExecutionResourceRequirement requirement =
                    intent.ResourceRequirements[i];
                if (requirement != null &&
                    string.Equals(
                        requirement.ResourceId,
                        payload.ResourceId,
                        StringComparison.Ordinal))
                {
                    match = true;
                    if (requirement.AccessMode != payload.AccessMode)
                    {
                        issues.Add(
                            ExecutionCoordinatorContractFailureCodes
                                .LeaseAccessModeMismatch,
                            "resourceLease.accessMode"
                        );
                    }
                }
            }
            if (!match)
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .LeaseResourceMismatch,
                    "resourceLease.resourceId"
                );
            }
        }

        private static bool IsLeaseEvent(
            ExecutionCoordinatorEventType type)
        {
            return type == ExecutionCoordinatorEventType.LeaseAcquired ||
                type == ExecutionCoordinatorEventType.LeaseRejected ||
                type == ExecutionCoordinatorEventType.LeaseReleased ||
                type == ExecutionCoordinatorEventType.LeaseReleaseFailed;
        }

        private static ExecutionResourceLeaseStatus ExpectedLeaseStatus(
            ExecutionCoordinatorEventType type)
        {
            switch (type)
            {
                case ExecutionCoordinatorEventType.LeaseAcquired:
                    return ExecutionResourceLeaseStatus.Acquired;
                case ExecutionCoordinatorEventType.LeaseRejected:
                    return ExecutionResourceLeaseStatus.Rejected;
                case ExecutionCoordinatorEventType.LeaseReleased:
                    return ExecutionResourceLeaseStatus.Released;
                default:
                    return ExecutionResourceLeaseStatus.ReleaseFailed;
            }
        }

        private static void ValidateResources(
            IReadOnlyList<ExecutionResourceRequirement> resources,
            Issues issues)
        {
            Dictionary<string, ExecutionResourceAccessMode> seen =
                new Dictionary<
                    string,
                    ExecutionResourceAccessMode>(
                    StringComparer.Ordinal
                );

            for (int i = 0; i < resources.Count; i++)
            {
                ExecutionResourceRequirement resource = resources[i];
                if (resource == null ||
                    string.IsNullOrWhiteSpace(resource.ResourceId))
                {
                    issues.Add(
                        ExecutionCoordinatorContractFailureCodes
                            .InvalidResourceRequirement,
                        "resourceRequirements[" + i + "]"
                    );
                    continue;
                }

                ExecutionResourceAccessMode previous;
                if (seen.TryGetValue(
                        resource.ResourceId,
                        out previous) &&
                    previous != resource.AccessMode)
                {
                    issues.Add(
                        ExecutionCoordinatorContractFailureCodes
                            .ConflictingResourceAccess,
                        "resourceRequirements[" + i + "]"
                    );
                }
                else if (!seen.ContainsKey(resource.ResourceId))
                {
                    seen.Add(resource.ResourceId, resource.AccessMode);
                }
            }
        }

        private static bool RequiresEventStep(
            ExecutionCoordinatorEventType type)
        {
            switch (type)
            {
                case ExecutionCoordinatorEventType.PermissionGranted:
                case ExecutionCoordinatorEventType.PermissionDenied:
                case ExecutionCoordinatorEventType.PermissionWaitTimedOut:
                case ExecutionCoordinatorEventType.LeaseAcquired:
                case ExecutionCoordinatorEventType.LeaseRejected:
                case ExecutionCoordinatorEventType.LeaseReleased:
                case ExecutionCoordinatorEventType.LeaseReleaseFailed:
                case ExecutionCoordinatorEventType.ExecutorStartAccepted:
                case ExecutionCoordinatorEventType.ExecutorStartRejected:
                case ExecutionCoordinatorEventType.DomainStageChanged:
                case ExecutionCoordinatorEventType.CompletionPolicyReached:
                case ExecutionCoordinatorEventType.ExecutorTerminal:
                case ExecutionCoordinatorEventType.StepTimedOut:
                case ExecutionCoordinatorEventType.AttemptTimedOut:
                case ExecutionCoordinatorEventType.CleanupCompleted:
                case ExecutionCoordinatorEventType.CleanupFailed:
                case ExecutionCoordinatorEventType
                    .ResourceAvailabilityEvaluated:
                case ExecutionCoordinatorEventType.ResourceWaitTimedOut:
                    return true;
                default:
                    return false;
            }
        }

        private static bool RequiresEventAttempt(
            ExecutionCoordinatorEventType type)
        {
            switch (type)
            {
                case ExecutionCoordinatorEventType.ExecutorStartAccepted:
                case ExecutionCoordinatorEventType.ExecutorStartRejected:
                case ExecutionCoordinatorEventType.DomainStageChanged:
                case ExecutionCoordinatorEventType.CompletionPolicyReached:
                case ExecutionCoordinatorEventType.ExecutorTerminal:
                case ExecutionCoordinatorEventType.AttemptTimedOut:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsControlEvent(
            ExecutionCoordinatorEventType type)
        {
            return type == ExecutionCoordinatorEventType.CancelRequested ||
                type == ExecutionCoordinatorEventType.InterruptRequested ||
                type == ExecutionCoordinatorEventType.SafetyStopRequested ||
                type == ExecutionCoordinatorEventType
                    .EmergencyPreemptRequested ||
                type == ExecutionCoordinatorEventType.ControlDispatched ||
                type == ExecutionCoordinatorEventType
                    .ControlEffectConfirmed ||
                type == ExecutionCoordinatorEventType.ControlEffectFailed;
        }

        private static bool RequiresIntentStep(
            ExecutionCoordinatorEffectIntentType type)
        {
            switch (type)
            {
                case ExecutionCoordinatorEffectIntentType.CheckPermission:
                case ExecutionCoordinatorEffectIntentType
                    .AcquireResourceLease:
                case ExecutionCoordinatorEffectIntentType
                    .ReleaseResourceLease:
                case ExecutionCoordinatorEffectIntentType.StartExecutor:
                case ExecutionCoordinatorEffectIntentType.StartTimeoutWatch:
                case ExecutionCoordinatorEffectIntentType.CancelTimeoutWatch:
                case ExecutionCoordinatorEffectIntentType.BeginCleanup:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsControlIntent(
            ExecutionCoordinatorEffectIntentType type)
        {
            return type == ExecutionCoordinatorEffectIntentType
                    .RequestExecutorCancel ||
                type == ExecutionCoordinatorEffectIntentType
                    .RequestExecutorInterrupt ||
                type == ExecutionCoordinatorEffectIntentType
                    .RequestSafetyPreempt;
        }

        private static bool IsResourceIntent(
            ExecutionCoordinatorEffectIntentType type)
        {
            return type == ExecutionCoordinatorEffectIntentType
                    .AcquireResourceLease ||
                type == ExecutionCoordinatorEffectIntentType
                    .ReleaseResourceLease;
        }

        private static void RequiredPlan(
            string actualPlanId,
            string expectedPlanId,
            Issues issues)
        {
            Required(
                actualPlanId,
                ExecutionCoordinatorContractFailureCodes
                    .MissingPlanId,
                "planId",
                issues
            );

            if (!string.IsNullOrWhiteSpace(expectedPlanId) &&
                !string.Equals(
                    actualPlanId,
                    expectedPlanId,
                    StringComparison.Ordinal))
            {
                issues.Add(
                    ExecutionCoordinatorContractFailureCodes
                        .PlanIdMismatch,
                    "planId"
                );
            }
        }

        private static void Required(
            string value,
            string code,
            string field,
            Issues issues)
        {
            if (string.IsNullOrWhiteSpace(value))
                issues.Add(code, field);
        }

        private static ExecutionCoordinatorContractValidationResult
            Result(Issues issues, string diagnostic)
        {
            return new ExecutionCoordinatorContractValidationResult(
                ValidatorVersion,
                issues.FailureCodes,
                issues.InvalidFields,
                diagnostic
            );
        }

        private sealed class Issues
        {
            private readonly List<string> failureCodes =
                new List<string>();
            private readonly List<string> invalidFields =
                new List<string>();

            public IReadOnlyList<string> FailureCodes => failureCodes;
            public IReadOnlyList<string> InvalidFields => invalidFields;

            public void Add(string code, string field)
            {
                if (!failureCodes.Contains(code))
                    failureCodes.Add(code);

                if (!string.IsNullOrEmpty(field) &&
                    !invalidFields.Contains(field))
                {
                    invalidFields.Add(field);
                }
            }
        }
    }
}
