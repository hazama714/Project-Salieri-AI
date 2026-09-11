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
using SalieriAI.Core.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>
    /// Bounded token correlation for body runtime observations. Action ID is
    /// checked but is never accepted as the correlation identity by itself.
    /// </summary>
    public sealed class BodyRuntimeCorrelationTracker
    {
        public const int MaxRuntimeInstances = 256;
        private readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Queue<string> order = new Queue<string>();

        public int TrackedRuntimeCount => entries.Count;

        public BodyRuntimeCorrelationResult Observe(
            BodyExecutionRequest request,
            BodyExecutionRuntimeFact fact)
        {
            if (request == null || fact == null ||
                string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.PlanId) ||
                string.IsNullOrWhiteSpace(request.StepId) ||
                string.IsNullOrWhiteSpace(request.ExecutionAttemptId) ||
                string.IsNullOrWhiteSpace(request.ActionId) ||
                string.IsNullOrWhiteSpace(fact.RuntimeExecutionToken) ||
                !Enum.IsDefined(typeof(BodyRuntimeLifecycle),
                    fact.Lifecycle))
                return Result(BodyRuntimeCorrelationStatus.Failed,
                    "BODY_CORRELATION_INPUT_INVALID");

            Entry entry;
            if (fact.Lifecycle == BodyRuntimeLifecycle.RequestAccepted ||
                fact.Lifecycle == BodyRuntimeLifecycle.RequestRejected)
            {
                if (entries.TryGetValue(
                        fact.RuntimeExecutionToken, out entry))
                {
                    if (!Matches(entry, request, fact))
                        return Result(
                            BodyRuntimeCorrelationStatus.Mismatch,
                            "RUNTIME_BODY_TOKEN_ALREADY_BOUND");
                    return Result(
                        BodyRuntimeCorrelationStatus.Duplicate,
                        "DUPLICATE_BODY_REQUEST_LIFECYCLE");
                }

                if (!EnsureCapacity())
                    return Result(BodyRuntimeCorrelationStatus.Failed,
                        "BODY_CORRELATION_CAPACITY_REACHED");

                bool terminal = fact.Lifecycle ==
                    BodyRuntimeLifecycle.RequestRejected;
                entry = new Entry(request, fact.RuntimeGeneration,
                    Mask(fact.Lifecycle), terminal);
                entries.Add(fact.RuntimeExecutionToken, entry);
                order.Enqueue(fact.RuntimeExecutionToken);
                return Result(BodyRuntimeCorrelationStatus.Accepted, "");
            }

            if (!entries.TryGetValue(
                    fact.RuntimeExecutionToken, out entry))
                return Result(BodyRuntimeCorrelationStatus.Stale,
                    "RUNTIME_BODY_TOKEN_NOT_BOUND");
            if (!Matches(entry, request, fact))
                return Result(
                    entry.RuntimeGeneration != fact.RuntimeGeneration
                        ? BodyRuntimeCorrelationStatus.Stale
                        : BodyRuntimeCorrelationStatus.Mismatch,
                    "BODY_CORRELATION_MISMATCH");
            if (entry.Terminal)
                return Result(BodyRuntimeCorrelationStatus.Stale,
                    "BODY_LIFECYCLE_AFTER_TERMINAL");

            int mask = Mask(fact.Lifecycle);
            if ((entry.ObservedMask & mask) != 0)
                return Result(BodyRuntimeCorrelationStatus.Duplicate,
                    "DUPLICATE_BODY_LIFECYCLE");
            if ((entry.ObservedMask &
                    Mask(BodyRuntimeLifecycle.RequestAccepted)) == 0)
                return Result(BodyRuntimeCorrelationStatus.Stale,
                    "BODY_REQUEST_ACCEPTED_NOT_OBSERVED");

            if (fact.Lifecycle ==
                    BodyRuntimeLifecycle.PhysicalTransportWritten &&
                (entry.ObservedMask & Mask(
                    BodyRuntimeLifecycle.PhysicalDispatchRequested)) == 0)
                return Result(BodyRuntimeCorrelationStatus.Stale,
                    "PHYSICAL_DISPATCH_NOT_OBSERVED");

            if ((fact.Lifecycle ==
                    BodyRuntimeLifecycle.PhysicalCompletionUnverified ||
                 fact.Lifecycle ==
                    BodyRuntimeLifecycle.PhysicalCompletionVerified) &&
                (entry.ObservedMask & Mask(
                    BodyRuntimeLifecycle.PhysicalTransportWritten)) == 0)
                return Result(BodyRuntimeCorrelationStatus.Stale,
                    "PHYSICAL_TRANSPORT_WRITE_NOT_OBSERVED");

            bool isTerminal =
                fact.Lifecycle ==
                    BodyRuntimeLifecycle.PhysicalCompletionUnverified ||
                fact.Lifecycle ==
                    BodyRuntimeLifecycle.PhysicalCompletionVerified ||
                fact.Lifecycle == BodyRuntimeLifecycle.Failed ||
                fact.Lifecycle == BodyRuntimeLifecycle.Interrupted ||
                fact.Lifecycle == BodyRuntimeLifecycle.SafetyPreempted;

            entries[fact.RuntimeExecutionToken] = new Entry(
                entry.Request, entry.RuntimeGeneration,
                entry.ObservedMask | mask, isTerminal);
            return Result(BodyRuntimeCorrelationStatus.Accepted, "");
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
            while (entries.Count >= MaxRuntimeInstances && remaining-- > 0)
            {
                string token = order.Dequeue();
                Entry candidate;
                if (!entries.TryGetValue(token, out candidate))
                    continue;
                if (candidate.Terminal)
                {
                    entries.Remove(token);
                    continue;
                }
                order.Enqueue(token);
            }
            return entries.Count < MaxRuntimeInstances;
        }

        private static bool Matches(
            Entry entry, BodyExecutionRequest request,
            BodyExecutionRuntimeFact fact)
        {
            return entry.RuntimeGeneration == fact.RuntimeGeneration &&
                Same(entry.Request.RequestId, request.RequestId) &&
                Same(entry.Request.PlanId, request.PlanId) &&
                Same(entry.Request.StepId, request.StepId) &&
                Same(entry.Request.ExecutionAttemptId,
                    request.ExecutionAttemptId) &&
                Same(entry.Request.ActionId, request.ActionId) &&
                Same(request.ActionId, fact.ActionId);
        }

        private static int Mask(BodyRuntimeLifecycle lifecycle)
        {
            return 1 << (int)lifecycle;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static BodyRuntimeCorrelationResult Result(
            BodyRuntimeCorrelationStatus status, string reason)
        {
            return new BodyRuntimeCorrelationResult(status, reason);
        }

        private sealed class Entry
        {
            public BodyExecutionRequest Request { get; }
            public int RuntimeGeneration { get; }
            public int ObservedMask { get; }
            public bool Terminal { get; }
            public Entry(BodyExecutionRequest request,
                int generation, int observedMask, bool terminal)
            {
                Request = request;
                RuntimeGeneration = generation;
                ObservedMask = observedMask;
                Terminal = terminal;
            }
        }
    }
}
