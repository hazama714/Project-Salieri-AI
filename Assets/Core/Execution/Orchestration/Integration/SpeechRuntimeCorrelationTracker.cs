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

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>
    /// Bounded runtime-token correlation. Text is never used as identity.
    /// A terminal runtime instance rejects every delayed later lifecycle.
    /// </summary>
    public sealed class SpeechRuntimeCorrelationTracker
    {
        public const int MaxRuntimeInstances = 256;

        private readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Queue<string> order = new Queue<string>();

        public int TrackedRuntimeCount => entries.Count;

        public SpeechRuntimeCorrelationResult Observe(
            SpeechExecutionRequest request,
            SpeechPlaybackRuntimeFact fact)
        {
            if (request == null || fact == null ||
                string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.PlanId) ||
                string.IsNullOrWhiteSpace(request.StepId) ||
                string.IsNullOrWhiteSpace(
                    request.ExecutionAttemptId) ||
                string.IsNullOrWhiteSpace(fact.RuntimeSpeechId) ||
                !Enum.IsDefined(typeof(SpeechAdapterLifecycle),
                    fact.Lifecycle))
            {
                return Result(
                    SpeechRuntimeCorrelationStatus.Failed,
                    "SPEECH_CORRELATION_INPUT_INVALID");
            }

            Entry entry;
            if (fact.Lifecycle ==
                SpeechAdapterLifecycle.RequestAccepted)
            {
                if (entries.TryGetValue(
                        fact.RuntimeSpeechId, out entry))
                {
                    if (!Matches(entry, request, fact))
                        return Result(
                            SpeechRuntimeCorrelationStatus.Mismatch,
                            "RUNTIME_SPEECH_ID_ALREADY_BOUND");
                    return Result(
                        SpeechRuntimeCorrelationStatus.Duplicate,
                        "DUPLICATE_REQUEST_ACCEPTED");
                }

                if (!EnsureCapacity())
                    return Result(
                        SpeechRuntimeCorrelationStatus.Failed,
                        "SPEECH_CORRELATION_CAPACITY_REACHED");

                entry = new Entry(
                    request, fact.RuntimeGeneration,
                    Mask(SpeechAdapterLifecycle.RequestAccepted),
                    false);
                entries.Add(fact.RuntimeSpeechId, entry);
                order.Enqueue(fact.RuntimeSpeechId);
                return Result(
                    SpeechRuntimeCorrelationStatus.Accepted,
                    string.Empty);
            }

            if (!entries.TryGetValue(fact.RuntimeSpeechId, out entry))
                return Result(
                    SpeechRuntimeCorrelationStatus.Stale,
                    "RUNTIME_SPEECH_ID_NOT_BOUND");
            if (!Matches(entry, request, fact))
                return Result(
                    entry.RuntimeGeneration != fact.RuntimeGeneration
                        ? SpeechRuntimeCorrelationStatus.Stale
                        : SpeechRuntimeCorrelationStatus.Mismatch,
                    "SPEECH_CORRELATION_MISMATCH");
            if (entry.Terminal)
                return Result(
                    SpeechRuntimeCorrelationStatus.Stale,
                    "LIFECYCLE_AFTER_TERMINAL");

            int mask = Mask(fact.Lifecycle);
            if ((entry.ObservedMask & mask) != 0)
                return Result(
                    SpeechRuntimeCorrelationStatus.Duplicate,
                    "DUPLICATE_SPEECH_LIFECYCLE");
            if ((entry.ObservedMask &
                    Mask(SpeechAdapterLifecycle.RequestAccepted)) == 0)
                return Result(
                    SpeechRuntimeCorrelationStatus.Stale,
                    "REQUEST_ACCEPTED_NOT_OBSERVED");
            if (fact.Lifecycle ==
                    SpeechAdapterLifecycle.PlaybackCompleted &&
                (entry.ObservedMask &
                    Mask(SpeechAdapterLifecycle.PlaybackStarted)) == 0)
                return Result(
                    SpeechRuntimeCorrelationStatus.Stale,
                    "PLAYBACK_STARTED_NOT_OBSERVED");

            bool terminal =
                fact.Lifecycle ==
                    SpeechAdapterLifecycle.PlaybackCompleted ||
                fact.Lifecycle ==
                    SpeechAdapterLifecycle.PlaybackFailed ||
                fact.Lifecycle ==
                    SpeechAdapterLifecycle.PlaybackInterrupted;
            entries[fact.RuntimeSpeechId] = new Entry(
                entry.Request,
                entry.RuntimeGeneration,
                entry.ObservedMask | mask,
                terminal);
            return Result(
                SpeechRuntimeCorrelationStatus.Accepted,
                string.Empty);
        }

        public void Clear()
        {
            entries.Clear();
            order.Clear();
        }

        private bool EnsureCapacity()
        {
            if (entries.Count < MaxRuntimeInstances)
                return true;

            int remaining = order.Count;
            while (entries.Count >= MaxRuntimeInstances &&
                remaining-- > 0)
            {
                string runtimeId = order.Dequeue();
                Entry candidate;
                if (!entries.TryGetValue(runtimeId, out candidate))
                    continue;
                if (candidate.Terminal)
                {
                    entries.Remove(runtimeId);
                    continue;
                }
                order.Enqueue(runtimeId);
            }
            return entries.Count < MaxRuntimeInstances;
        }

        private static bool Matches(
            Entry entry,
            SpeechExecutionRequest request,
            SpeechPlaybackRuntimeFact fact)
        {
            return entry.RuntimeGeneration == fact.RuntimeGeneration &&
                Same(entry.Request.RequestId, request.RequestId) &&
                Same(entry.Request.PlanId, request.PlanId) &&
                Same(entry.Request.StepId, request.StepId) &&
                Same(entry.Request.ExecutionAttemptId,
                    request.ExecutionAttemptId);
        }

        private static int Mask(SpeechAdapterLifecycle lifecycle)
        {
            return 1 << (int)lifecycle;
        }

        private static SpeechRuntimeCorrelationResult Result(
            SpeechRuntimeCorrelationStatus status,
            string failureReason)
        {
            return new SpeechRuntimeCorrelationResult(
                status, failureReason);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(
                left, right, StringComparison.Ordinal);
        }

        private sealed class Entry
        {
            public SpeechExecutionRequest Request { get; }
            public int RuntimeGeneration { get; }
            public int ObservedMask { get; }
            public bool Terminal { get; }

            public Entry(
                SpeechExecutionRequest request,
                int runtimeGeneration,
                int observedMask,
                bool terminal)
            {
                Request = request;
                RuntimeGeneration = runtimeGeneration;
                ObservedMask = observedMask;
                Terminal = terminal;
            }
        }
    }
}
