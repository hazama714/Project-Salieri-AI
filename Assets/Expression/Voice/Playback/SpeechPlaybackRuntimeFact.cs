// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using SalieriAI.Core.Execution.Orchestration.Adapters;

/// <summary>
/// Immutable fact emitted at the actual VoicePlaybackController lifecycle
/// boundary. Coordinator IDs and timestamps are intentionally absent; a
/// shadow context supplies them without changing the formal speech route.
/// </summary>
public sealed class SpeechPlaybackRuntimeFact
{
    public string RuntimeSpeechId { get; }
    public int RuntimeGeneration { get; }
    public SpeechAdapterLifecycle Lifecycle { get; }
    public string FailureReason { get; }
    public string ResponseCorrelationId { get; }

    public SpeechPlaybackRuntimeFact(
        string runtimeSpeechId,
        int runtimeGeneration,
        SpeechAdapterLifecycle lifecycle,
        string failureReason,
        string responseCorrelationId = null)
    {
        RuntimeSpeechId = runtimeSpeechId ?? string.Empty;
        RuntimeGeneration = runtimeGeneration;
        Lifecycle = lifecycle;
        FailureReason = failureReason ?? string.Empty;
        ResponseCorrelationId = responseCorrelationId ?? string.Empty;
    }
}
