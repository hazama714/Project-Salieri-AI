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
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Adapters
{
    public static class ExecutionRuntimeAdapterTranslator
    {
        public const string TranslatorVersion =
            "execution-runtime-adapter-translator-3c1.1";

        public static AdapterTranslationResult<PermissionCheckRequest>
            FromPermissionIntent(
                ExecutionCoordinatorEffectIntent intent,
                string requestId,
                string permissionKind)
        {
            string failure = ExecutionAdapterContractValidator
                .ValidateIntent(intent,
                    ExecutionCoordinatorEffectIntentType.CheckPermission);
            if (!string.IsNullOrEmpty(failure) ||
                string.IsNullOrWhiteSpace(requestId) ||
                string.IsNullOrWhiteSpace(permissionKind))
                return Fail<PermissionCheckRequest>(
                    string.IsNullOrEmpty(failure)
                        ? ExecutionAdapterFailureCodes.MissingId : failure);
            return AdapterTranslationResult<PermissionCheckRequest>.Success(
                new PermissionCheckRequest(
                    requestId, intent.IntentId, intent.PlanId,
                    intent.StepId, intent.ExecutionAttemptId,
                    permissionKind, intent.CreatedAtUtc,
                    intent.TimeoutPolicy, intent.DiagnosticMessage));
        }

        public static AdapterTranslationResult<ExecutionCoordinatorEvent>
            ToPermissionEvent(
                PermissionCheckRequest request,
                PermissionAdapterResult result,
                string eventId, int generation, string source)
        {
            if (request == null || result == null)
                return Fail<ExecutionCoordinatorEvent>(
                    ExecutionAdapterFailureCodes.NullSource);
            string failure = ExecutionAdapterContractValidator
                .ValidateCorrelation(
                    request.PlanId, request.StepId,
                    request.ExecutionAttemptId, request.RequestId,
                    result.PlanId, result.StepId,
                    result.ExecutionAttemptId, result.RequestId);
            if (!string.IsNullOrEmpty(failure))
                return Fail<ExecutionCoordinatorEvent>(failure);
            if (result.Status == PermissionAdapterStatus.Pending)
                return Fail<ExecutionCoordinatorEvent>(
                    ExecutionAdapterFailureCodes.PendingHasNoEvent);
            if (result.Status == PermissionAdapterStatus.Failed)
                return Fail<ExecutionCoordinatorEvent>(
                    ExecutionAdapterFailureCodes
                        .RuntimeFailureHasNoCoordinatorFact);
            ExecutionCoordinatorEventType type = result.Status ==
                PermissionAdapterStatus.Granted
                    ? ExecutionCoordinatorEventType.PermissionGranted
                    : ExecutionCoordinatorEventType.PermissionDenied;
            return Event(eventId, type, request.PlanId, request.StepId,
                request.ExecutionAttemptId, string.Empty,
                result.OccurredAtUtc, source, generation, null,
                ExecutionTerminalOutcome.None,
                string.IsNullOrEmpty(result.Reason)
                    ? new string[0] : new[] { result.Reason },
                result.Reason, null);
        }

        public static AdapterTranslationResult<ResourceAcquireRequest>
            FromAcquireIntent(
                ExecutionCoordinatorEffectIntent intent,
                string requestId)
        {
            string failure = ExecutionAdapterContractValidator
                .ValidateIntent(intent,
                    ExecutionCoordinatorEffectIntentType
                        .AcquireResourceLease);
            if (!string.IsNullOrEmpty(failure) ||
                string.IsNullOrWhiteSpace(requestId) ||
                intent.ResourceLease == null)
                return Fail<ResourceAcquireRequest>(
                    !string.IsNullOrEmpty(failure) ? failure :
                    ExecutionAdapterFailureCodes.InvalidPayload);
            return AdapterTranslationResult<ResourceAcquireRequest>.Success(
                new ResourceAcquireRequest(
                    requestId, intent.IntentId, intent.PlanId,
                    intent.StepId, intent.ExecutionAttemptId,
                    intent.ResourceLease.ResourceId,
                    intent.ResourceLease.ResourceType,
                    intent.ResourceLease.AccessMode,
                    intent.CreatedAtUtc));
        }

        public static AdapterTranslationResult<ExecutionCoordinatorEvent>
            ToAcquireEvent(
                ResourceAcquireRequest request,
                ResourceAcquireResult result,
                string eventId, int generation, string source)
        {
            string failure = ExecutionAdapterContractValidator
                .ValidateResourceCorrelation(request, result);
            if (!string.IsNullOrEmpty(failure))
                return Fail<ExecutionCoordinatorEvent>(failure);
            ExecutionCoordinatorEventType type = result.Status ==
                ExecutionResourceLeaseStatus.Acquired
                    ? ExecutionCoordinatorEventType.LeaseAcquired
                    : ExecutionCoordinatorEventType.LeaseRejected;
            ResourceLeaseReference lease = new ResourceLeaseReference(
                result.ExternalLeaseId, result.ResourceId,
                result.ResourceType, result.PlanId, result.StepId,
                result.ExecutionAttemptId, result.AccessMode,
                result.OccurredAtUtc, result.Status);
            return Event(eventId, type, result.PlanId, result.StepId,
                result.ExecutionAttemptId, string.Empty,
                result.OccurredAtUtc, source, generation, null,
                ExecutionTerminalOutcome.None, new string[0],
                result.FailureReason,
                new ExecutionResourceLeaseEventPayload(
                    lease, result.FailureReason));
        }

        public static AdapterTranslationResult<ResourceReleaseRequest>
            FromReleaseIntent(
                ExecutionCoordinatorEffectIntent intent,
                string requestId)
        {
            string failure = ExecutionAdapterContractValidator
                .ValidateIntent(intent,
                    ExecutionCoordinatorEffectIntentType
                        .ReleaseResourceLease);
            if (!string.IsNullOrEmpty(failure) ||
                string.IsNullOrWhiteSpace(requestId) ||
                intent.ResourceLease == null ||
                string.IsNullOrWhiteSpace(intent.ResourceLease.LeaseId))
                return Fail<ResourceReleaseRequest>(
                    !string.IsNullOrEmpty(failure) ? failure :
                    ExecutionAdapterFailureCodes.InvalidPayload);
            return AdapterTranslationResult<ResourceReleaseRequest>.Success(
                new ResourceReleaseRequest(
                    requestId, intent.IntentId, intent.PlanId,
                    intent.StepId, intent.ResourceLease.LeaseId,
                    intent.ResourceLease.ResourceId,
                    intent.ResourceLease.ResourceType,
                    intent.ResourceLease.AccessMode,
                    intent.CreatedAtUtc));
        }

        public static AdapterTranslationResult<ExecutionCoordinatorEvent>
            ToReleaseEvent(
                ResourceReleaseRequest request,
                ResourceReleaseResult result,
                string eventId, int generation, string source)
        {
            string failure = ExecutionAdapterContractValidator
                .ValidateResourceCorrelation(request, result);
            if (!string.IsNullOrEmpty(failure))
                return Fail<ExecutionCoordinatorEvent>(failure);
            ExecutionCoordinatorEventType type = result.Status ==
                ExecutionResourceLeaseStatus.Released
                    ? ExecutionCoordinatorEventType.LeaseReleased
                    : ExecutionCoordinatorEventType.LeaseReleaseFailed;
            ResourceLeaseReference lease = new ResourceLeaseReference(
                result.ExternalLeaseId, result.ResourceId,
                result.ResourceType, result.PlanId, result.StepId,
                string.Empty, result.AccessMode, result.OccurredAtUtc,
                result.Status);
            return Event(eventId, type, result.PlanId, result.StepId,
                string.Empty, string.Empty, result.OccurredAtUtc,
                source, generation, null, ExecutionTerminalOutcome.None,
                new string[0], result.FailureReason,
                new ExecutionResourceLeaseEventPayload(
                    lease, result.FailureReason));
        }

        public static AdapterTranslationResult<SpeechExecutionRequest>
            FromSpeechIntent(
                ExecutionCoordinatorEffectIntent intent,
                string text, string voiceProfileId,
                ExecutionCompletionPolicy completionPolicy)
        {
            string failure = ExecutionAdapterContractValidator
                .ValidateIntent(intent,
                    ExecutionCoordinatorEffectIntentType.StartExecutor);
            if (!string.IsNullOrEmpty(failure) ||
                string.IsNullOrWhiteSpace(intent.RequestId) ||
                string.IsNullOrWhiteSpace(intent.ExecutionAttemptId) ||
                string.IsNullOrWhiteSpace(text))
                return Fail<SpeechExecutionRequest>(
                    !string.IsNullOrEmpty(failure) ? failure :
                    ExecutionAdapterFailureCodes.MissingId);
            return AdapterTranslationResult<SpeechExecutionRequest>.Success(
                new SpeechExecutionRequest(
                    intent.RequestId, intent.IntentId, intent.PlanId,
                    intent.StepId, intent.ExecutionAttemptId, text,
                    voiceProfileId, intent.CreatedAtUtc,
                    completionPolicy));
        }

        public static AdapterTranslationResult<ExecutionCoordinatorEvent>
            ToSpeechEvent(
                SpeechExecutionRequest request,
                SpeechLifecycleResult result,
                string eventId, int generation, string source)
        {
            string failure = request == null || result == null
                ? ExecutionAdapterFailureCodes.NullSource
                : ExecutionAdapterContractValidator.ValidateCorrelation(
                    request.PlanId, request.StepId,
                    request.ExecutionAttemptId, request.RequestId,
                    result.PlanId, result.StepId,
                    result.ExecutionAttemptId, result.RequestId);
            if (!string.IsNullOrEmpty(failure))
                return Fail<ExecutionCoordinatorEvent>(failure);

            ExecutionCoordinatorEventType type;
            ExecutionDomainStage stage = null;
            ExecutionTerminalOutcome outcome =
                ExecutionTerminalOutcome.None;
            switch (result.Lifecycle)
            {
                case SpeechAdapterLifecycle.RequestAccepted:
                    type = ExecutionCoordinatorEventType
                        .ExecutorStartAccepted;
                    break;
                case SpeechAdapterLifecycle.PlaybackStarted:
                    type = ExecutionCoordinatorEventType.DomainStageChanged;
                    stage = new ExecutionDomainStage(
                        ExecutionDomainStageIds.PlaybackStarted);
                    break;
                case SpeechAdapterLifecycle.PlaybackCompleted:
                    type = ExecutionCoordinatorEventType.DomainStageChanged;
                    stage = new ExecutionDomainStage(
                        ExecutionDomainStageIds.PlaybackCompleted);
                    break;
                case SpeechAdapterLifecycle.PlaybackFailed:
                    type = ExecutionCoordinatorEventType.ExecutorTerminal;
                    outcome = ExecutionTerminalOutcome.Failed;
                    break;
                case SpeechAdapterLifecycle.PlaybackInterrupted:
                    type = ExecutionCoordinatorEventType.ExecutorTerminal;
                    outcome = ExecutionTerminalOutcome.Interrupted;
                    break;
                default:
                    return Fail<ExecutionCoordinatorEvent>(
                        ExecutionAdapterFailureCodes.InvalidStatus);
            }
            return Event(eventId, type, request.PlanId, request.StepId,
                request.ExecutionAttemptId, string.Empty,
                result.OccurredAtUtc, source, generation, stage, outcome,
                string.IsNullOrEmpty(result.FailureReason)
                    ? new string[0]
                    : new[] { result.FailureReason },
                result.FailureReason, null);
        }

        public static AdapterTranslationResult<BodyExecutionRequest>
            FromBodyIntent(
                ExecutionCoordinatorEffectIntent intent,
                string actionId, string skillId,
                ExecutionRequestReference parameterReference,
                ExecutionCompletionPolicy completionPolicy)
        {
            string failure = ExecutionAdapterContractValidator
                .ValidateIntent(intent,
                    ExecutionCoordinatorEffectIntentType.StartExecutor);
            if (!string.IsNullOrEmpty(failure) ||
                string.IsNullOrWhiteSpace(intent.RequestId) ||
                string.IsNullOrWhiteSpace(intent.ExecutionAttemptId) ||
                (string.IsNullOrWhiteSpace(actionId) &&
                 string.IsNullOrWhiteSpace(skillId)))
                return Fail<BodyExecutionRequest>(
                    !string.IsNullOrEmpty(failure) ? failure :
                    ExecutionAdapterFailureCodes.MissingId);
            return AdapterTranslationResult<BodyExecutionRequest>.Success(
                new BodyExecutionRequest(
                    intent.RequestId, intent.IntentId, intent.PlanId,
                    intent.StepId, intent.ExecutionAttemptId,
                    actionId, skillId, parameterReference,
                    intent.CreatedAtUtc, completionPolicy));
        }

        public static AdapterTranslationResult<ExecutionCoordinatorEvent>
            ToBodyEvent(
                BodyExecutionRequest request,
                BodyExecutionLifecycleResult result,
                string eventId, int generation, string source)
        {
            string failure = request == null || result == null
                ? ExecutionAdapterFailureCodes.NullSource
                : ExecutionAdapterContractValidator.ValidateCorrelation(
                    request.PlanId, request.StepId,
                    request.ExecutionAttemptId, request.RequestId,
                    result.PlanId, result.StepId,
                    result.ExecutionAttemptId, result.RequestId);
            if (!string.IsNullOrEmpty(failure))
                return Fail<ExecutionCoordinatorEvent>(failure);
            int bodyFactCount = (result.RequestAccepted ? 1 : 0) +
                (result.DomainStage != null &&
                 !string.IsNullOrWhiteSpace(result.DomainStage.StageId)
                    ? 1 : 0) +
                (result.TerminalOutcome != ExecutionTerminalOutcome.None
                    ? 1 : 0);
            if (bodyFactCount != 1)
                return Fail<ExecutionCoordinatorEvent>(
                    ExecutionAdapterFailureCodes.InvalidStatus);
            if (result.TerminalOutcome != ExecutionTerminalOutcome.None)
                return Event(eventId,
                    ExecutionCoordinatorEventType.ExecutorTerminal,
                    request.PlanId, request.StepId,
                    request.ExecutionAttemptId, string.Empty,
                    result.OccurredAtUtc, source, generation, null,
                    result.TerminalOutcome,
                    string.IsNullOrEmpty(result.FailureReason)
                        ? new string[0]
                        : new[] { result.FailureReason },
                    result.FailureReason, null);
            if (result.RequestAccepted)
                return Event(eventId,
                    ExecutionCoordinatorEventType.ExecutorStartAccepted,
                    request.PlanId, request.StepId,
                    request.ExecutionAttemptId, string.Empty,
                    result.OccurredAtUtc, source, generation, null,
                    ExecutionTerminalOutcome.None, new string[0],
                    string.Empty, null);
            if (result.DomainStage != null &&
                !string.IsNullOrWhiteSpace(result.DomainStage.StageId))
                return Event(eventId,
                    ExecutionCoordinatorEventType.DomainStageChanged,
                    request.PlanId, request.StepId,
                    request.ExecutionAttemptId, string.Empty,
                    result.OccurredAtUtc, source, generation,
                    result.DomainStage, ExecutionTerminalOutcome.None,
                    new string[0], string.Empty, null);
            return Fail<ExecutionCoordinatorEvent>(
                ExecutionAdapterFailureCodes.InvalidStatus);
        }

        public static AdapterTranslationResult<SafetyControlRequest>
            FromSafetyIntent(
                ExecutionCoordinatorEffectIntent intent,
                ExecutionControlScope scope,
                ExecutionControlType controlKind,
                int priority, string targetResourceId)
        {
            if (intent == null || !IsControlIntent(intent.IntentType))
                return Fail<SafetyControlRequest>(
                    intent == null
                        ? ExecutionAdapterFailureCodes.NullSource
                        : ExecutionAdapterFailureCodes.WrongDomain);
            SafetyControlRequest request = new SafetyControlRequest(
                intent.IntentId, intent.ControlRequestId, intent.PlanId,
                intent.StepId, intent.ExecutionAttemptId,
                targetResourceId, scope, controlKind, priority,
                intent.CreatedAtUtc);
            string failure = ExecutionAdapterContractValidator
                .ValidateScope(request);
            return string.IsNullOrEmpty(failure)
                ? AdapterTranslationResult<SafetyControlRequest>
                    .Success(request)
                : Fail<SafetyControlRequest>(failure);
        }

        public static AdapterTranslationResult<ExecutionCoordinatorEvent>
            ToSafetyEvent(
                SafetyControlRequest request,
                SafetyControlResult result,
                string eventId, int generation, string source)
        {
            if (request == null || result == null)
                return Fail<ExecutionCoordinatorEvent>(
                    ExecutionAdapterFailureCodes.NullSource);
            if (!ExecutionAdapterContractValidator.Same(
                    request.ControlRequestId,
                    result.ControlRequestId) ||
                !ExecutionAdapterContractValidator.Same(
                    request.PlanId, result.PlanId))
                return Fail<ExecutionCoordinatorEvent>(
                    ExecutionAdapterFailureCodes.CorrelationMismatch);
            if (result.EffectConfirmed && !result.Accepted)
                return Fail<ExecutionCoordinatorEvent>(
                    ExecutionAdapterFailureCodes.InvalidStatus);
            ExecutionCoordinatorEventType type;
            if (result.EffectConfirmed)
                type = ExecutionCoordinatorEventType
                    .ControlEffectConfirmed;
            else if (result.Accepted)
                type = ExecutionCoordinatorEventType.ControlDispatched;
            else
                type = ExecutionCoordinatorEventType.ControlEffectFailed;
            return Event(eventId, type, request.PlanId, string.Empty,
                string.Empty, request.ControlRequestId,
                result.OccurredAtUtc, source, generation, null,
                ExecutionTerminalOutcome.None,
                string.IsNullOrEmpty(result.FailureReason)
                    ? new string[0]
                    : new[] { result.FailureReason },
                result.FailureReason, null);
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

        private static AdapterTranslationResult<T> Fail<T>(string code)
            where T : class
        {
            return AdapterTranslationResult<T>.Failure(
                code, TranslatorVersion);
        }

        private static AdapterTranslationResult<ExecutionCoordinatorEvent>
            Event(
                string eventId, ExecutionCoordinatorEventType type,
                string planId, string stepId, string attemptId,
                string controlRequestId, DateTime occurredAtUtc,
                string source, int generation,
                ExecutionDomainStage stage,
                ExecutionTerminalOutcome outcome,
                string[] failureCodes, string diagnostic,
                ExecutionResourceLeaseEventPayload resourceLease)
        {
            if (string.IsNullOrWhiteSpace(eventId) ||
                occurredAtUtc == default(DateTime))
                return Fail<ExecutionCoordinatorEvent>(
                    ExecutionAdapterFailureCodes.MissingId);
            return AdapterTranslationResult<ExecutionCoordinatorEvent>
                .Success(new ExecutionCoordinatorEvent(
                    eventId, type, planId, stepId, attemptId,
                    controlRequestId, occurredAtUtc, source, generation,
                    stage, outcome, failureCodes, diagnostic,
                    resourceLease));
        }
    }
}
