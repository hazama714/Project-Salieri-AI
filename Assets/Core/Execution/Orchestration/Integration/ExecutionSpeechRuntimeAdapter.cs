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

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>
    /// Observation-only translator from one actual playback fact to the
    /// existing Phase 3C-1 SpeechLifecycleResult contract.
    /// </summary>
    public sealed class ExecutionSpeechRuntimeAdapter
    {
        public const string AdapterVersion =
            "execution-speech-runtime-adapter-3c4.1";

        public SpeechRuntimeAdapterResult Observe(
            SpeechExecutionRequest request,
            SpeechPlaybackRuntimeFact runtimeFact,
            DateTime occurredAtUtc)
        {
            if (request == null)
                return Failed(runtimeFact,
                    "SPEECH_REQUEST_NULL");
            if (runtimeFact == null)
                return Failed(null, "SPEECH_RUNTIME_FACT_NULL");
            if (string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.PlanId) ||
                string.IsNullOrWhiteSpace(request.StepId) ||
                string.IsNullOrWhiteSpace(
                    request.ExecutionAttemptId) ||
                string.IsNullOrWhiteSpace(
                    runtimeFact.RuntimeSpeechId) ||
                occurredAtUtc == default(DateTime) ||
                !Enum.IsDefined(typeof(SpeechAdapterLifecycle),
                    runtimeFact.Lifecycle))
            {
                return new SpeechRuntimeAdapterResult(
                    runtimeFact.RuntimeSpeechId,
                    runtimeFact.RuntimeGeneration,
                    SpeechRuntimeAdapterStatus.RejectedInvalid,
                    null,
                    "SPEECH_RUNTIME_OBSERVATION_INVALID");
            }

            bool terminalFailure =
                runtimeFact.Lifecycle ==
                    SpeechAdapterLifecycle.PlaybackFailed ||
                runtimeFact.Lifecycle ==
                    SpeechAdapterLifecycle.PlaybackInterrupted;
            string reason = runtimeFact.FailureReason;
            if (terminalFailure && string.IsNullOrWhiteSpace(reason))
                reason = "unknown/runtime-unreported";

            SpeechLifecycleResult result = new SpeechLifecycleResult(
                request.RequestId,
                request.PlanId,
                request.StepId,
                request.ExecutionAttemptId,
                runtimeFact.Lifecycle,
                occurredAtUtc,
                reason);
            return new SpeechRuntimeAdapterResult(
                runtimeFact.RuntimeSpeechId,
                runtimeFact.RuntimeGeneration,
                SpeechRuntimeAdapterStatus.Observed,
                result,
                string.Empty);
        }

        private static SpeechRuntimeAdapterResult Failed(
            SpeechPlaybackRuntimeFact runtimeFact,
            string failureReason)
        {
            return new SpeechRuntimeAdapterResult(
                runtimeFact != null
                    ? runtimeFact.RuntimeSpeechId : string.Empty,
                runtimeFact != null
                    ? runtimeFact.RuntimeGeneration : 0,
                SpeechRuntimeAdapterStatus.Failed,
                null,
                failureReason);
        }
    }
}
