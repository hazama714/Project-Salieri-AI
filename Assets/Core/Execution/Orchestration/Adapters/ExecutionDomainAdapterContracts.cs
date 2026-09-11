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

namespace SalieriAI.Core.Execution.Orchestration.Adapters
{
    public enum SpeechAdapterLifecycle
    {
        RequestAccepted = 0,
        PlaybackStarted = 1,
        PlaybackCompleted = 2,
        PlaybackFailed = 3,
        PlaybackInterrupted = 4
    }

    public sealed class SpeechExecutionRequest
    {
        public string RequestId { get; }
        public string EffectIntentId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string Text { get; }
        public string VoiceProfileId { get; }
        public DateTime RequestedAtUtc { get; }
        public ExecutionCompletionPolicy CompletionPolicy { get; }

        public SpeechExecutionRequest(
            string requestId, string effectIntentId, string planId,
            string stepId, string executionAttemptId, string text,
            string voiceProfileId, DateTime requestedAtUtc,
            ExecutionCompletionPolicy completionPolicy)
        {
            RequestId = AdapterContractUtility.Text(requestId);
            EffectIntentId = AdapterContractUtility.Text(effectIntentId);
            PlanId = AdapterContractUtility.Text(planId);
            StepId = AdapterContractUtility.Text(stepId);
            ExecutionAttemptId =
                AdapterContractUtility.Text(executionAttemptId);
            Text = AdapterContractUtility.Text(text);
            VoiceProfileId =
                AdapterContractUtility.Text(voiceProfileId);
            RequestedAtUtc = AdapterContractUtility.Utc(requestedAtUtc);
            CompletionPolicy = completionPolicy;
        }
    }

    public sealed class SpeechLifecycleResult
    {
        public string RequestId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public SpeechAdapterLifecycle Lifecycle { get; }
        public DateTime OccurredAtUtc { get; }
        public string FailureReason { get; }

        public SpeechLifecycleResult(
            string requestId, string planId, string stepId,
            string executionAttemptId,
            SpeechAdapterLifecycle lifecycle,
            DateTime occurredAtUtc, string failureReason)
        {
            RequestId = AdapterContractUtility.Text(requestId);
            PlanId = AdapterContractUtility.Text(planId);
            StepId = AdapterContractUtility.Text(stepId);
            ExecutionAttemptId =
                AdapterContractUtility.Text(executionAttemptId);
            Lifecycle = lifecycle;
            OccurredAtUtc = AdapterContractUtility.Utc(occurredAtUtc);
            FailureReason =
                AdapterContractUtility.Text(failureReason);
        }
    }

    public sealed class BodyExecutionRequest
    {
        public string RequestId { get; }
        public string EffectIntentId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string ActionId { get; }
        public string SkillId { get; }
        public ExecutionRequestReference ParameterReference { get; }
        public DateTime RequestedAtUtc { get; }
        public ExecutionCompletionPolicy CompletionPolicy { get; }

        public BodyExecutionRequest(
            string requestId, string effectIntentId, string planId,
            string stepId, string executionAttemptId,
            string actionId, string skillId,
            ExecutionRequestReference parameterReference,
            DateTime requestedAtUtc,
            ExecutionCompletionPolicy completionPolicy)
        {
            RequestId = AdapterContractUtility.Text(requestId);
            EffectIntentId = AdapterContractUtility.Text(effectIntentId);
            PlanId = AdapterContractUtility.Text(planId);
            StepId = AdapterContractUtility.Text(stepId);
            ExecutionAttemptId =
                AdapterContractUtility.Text(executionAttemptId);
            ActionId = AdapterContractUtility.Text(actionId);
            SkillId = AdapterContractUtility.Text(skillId);
            ParameterReference = parameterReference;
            RequestedAtUtc = AdapterContractUtility.Utc(requestedAtUtc);
            CompletionPolicy = completionPolicy;
        }
    }

    public sealed class BodyExecutionLifecycleResult
    {
        public string RequestId { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public bool RequestAccepted { get; }
        public ExecutionDomainStage DomainStage { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }
        public DateTime OccurredAtUtc { get; }
        public string FailureReason { get; }

        public BodyExecutionLifecycleResult(
            string requestId, string planId, string stepId,
            string executionAttemptId, bool requestAccepted,
            ExecutionDomainStage domainStage,
            ExecutionTerminalOutcome terminalOutcome,
            DateTime occurredAtUtc, string failureReason)
        {
            RequestId = AdapterContractUtility.Text(requestId);
            PlanId = AdapterContractUtility.Text(planId);
            StepId = AdapterContractUtility.Text(stepId);
            ExecutionAttemptId =
                AdapterContractUtility.Text(executionAttemptId);
            RequestAccepted = requestAccepted;
            DomainStage = domainStage;
            TerminalOutcome = terminalOutcome;
            OccurredAtUtc = AdapterContractUtility.Utc(occurredAtUtc);
            FailureReason =
                AdapterContractUtility.Text(failureReason);
        }
    }

    public sealed class SafetyControlRequest
    {
        public string EffectIntentId { get; }
        public string ControlRequestId { get; }
        public string PlanId { get; }
        public string TargetStepId { get; }
        public string TargetAttemptId { get; }
        public string TargetResourceId { get; }
        public ExecutionControlScope Scope { get; }
        public ExecutionControlType ControlKind { get; }
        public int Priority { get; }
        public DateTime RequestedAtUtc { get; }

        public SafetyControlRequest(
            string effectIntentId, string controlRequestId,
            string planId, string targetStepId,
            string targetAttemptId, string targetResourceId,
            ExecutionControlScope scope,
            ExecutionControlType controlKind,
            int priority, DateTime requestedAtUtc)
        {
            EffectIntentId = AdapterContractUtility.Text(effectIntentId);
            ControlRequestId =
                AdapterContractUtility.Text(controlRequestId);
            PlanId = AdapterContractUtility.Text(planId);
            TargetStepId = AdapterContractUtility.Text(targetStepId);
            TargetAttemptId =
                AdapterContractUtility.Text(targetAttemptId);
            TargetResourceId =
                AdapterContractUtility.Text(targetResourceId);
            Scope = scope;
            ControlKind = controlKind;
            Priority = priority;
            RequestedAtUtc = AdapterContractUtility.Utc(requestedAtUtc);
        }
    }

    public sealed class SafetyControlResult
    {
        public string ControlRequestId { get; }
        public string PlanId { get; }
        public bool Accepted { get; }
        public bool EffectConfirmed { get; }
        public DateTime OccurredAtUtc { get; }
        public string FailureReason { get; }

        public SafetyControlResult(
            string controlRequestId, string planId,
            bool accepted, bool effectConfirmed,
            DateTime occurredAtUtc, string failureReason)
        {
            ControlRequestId =
                AdapterContractUtility.Text(controlRequestId);
            PlanId = AdapterContractUtility.Text(planId);
            Accepted = accepted;
            EffectConfirmed = effectConfirmed;
            OccurredAtUtc = AdapterContractUtility.Utc(occurredAtUtc);
            FailureReason =
                AdapterContractUtility.Text(failureReason);
        }
    }
}
