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
    /// Bounded shadow-only correlation. Runtime token and coordinator control
    /// ID remain distinct; control kind or spoken STOP text is never identity.
    /// </summary>
    public sealed class SafetyRuntimeCorrelationTracker
    {
        public const int MaxRuntimeControls = 256;
        private readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Queue<string> order = new Queue<string>();
        public int TrackedRuntimeCount => entries.Count;

        public SafetyRuntimeCorrelationResult Observe(
            SafetyControlRequest request, SafetyControlRuntimeFact fact)
        {
            if (request == null || fact == null ||
                string.IsNullOrWhiteSpace(request.ControlRequestId) ||
                string.IsNullOrWhiteSpace(request.PlanId) ||
                string.IsNullOrWhiteSpace(fact.RuntimeControlToken) ||
                fact.RuntimeGeneration <= 0)
                return Result(SafetyRuntimeCorrelationStatus.Failed,
                    "SAFETY_CORRELATION_INPUT_INVALID");

            Entry entry;
            bool opening = fact.Lifecycle == SafetyRuntimeLifecycle.RequestObserved;
            if (opening)
            {
                if (entries.TryGetValue(fact.RuntimeControlToken, out entry))
                {
                    if (!Matches(entry, request, fact))
                        return Result(SafetyRuntimeCorrelationStatus.Mismatch,
                            "RUNTIME_SAFETY_TOKEN_ALREADY_BOUND");
                    return Result(SafetyRuntimeCorrelationStatus.Duplicate,
                        "DUPLICATE_SAFETY_REQUEST_OBSERVED");
                }
                if (!EnsureCapacity())
                    return Result(SafetyRuntimeCorrelationStatus.Failed,
                        "SAFETY_CORRELATION_CAPACITY_REACHED");
                entries.Add(fact.RuntimeControlToken,
                    new Entry(request, fact.RuntimeGeneration,
                        Mask(fact.Lifecycle), false));
                order.Enqueue(fact.RuntimeControlToken);
                return Result(SafetyRuntimeCorrelationStatus.Accepted, "");
            }

            if (!entries.TryGetValue(fact.RuntimeControlToken, out entry))
                return Result(SafetyRuntimeCorrelationStatus.Stale,
                    "RUNTIME_SAFETY_TOKEN_NOT_BOUND");
            if (!Matches(entry, request, fact))
                return Result(
                    entry.RuntimeGeneration != fact.RuntimeGeneration
                        ? SafetyRuntimeCorrelationStatus.Stale
                        : SafetyRuntimeCorrelationStatus.Mismatch,
                    "SAFETY_CORRELATION_MISMATCH");
            int mask = Mask(fact.Lifecycle);
            if ((entry.ObservedMask & mask) != 0)
                return Result(SafetyRuntimeCorrelationStatus.Duplicate,
                    "DUPLICATE_SAFETY_LIFECYCLE");
            if (entry.Terminal)
                return Result(SafetyRuntimeCorrelationStatus.Stale,
                    "SAFETY_LIFECYCLE_AFTER_TERMINAL");

            bool acceptedObserved = (entry.ObservedMask &
                Mask(SafetyRuntimeLifecycle.RequestAccepted)) != 0;
            bool dispatchObserved = (entry.ObservedMask &
                Mask(SafetyRuntimeLifecycle.ControlDispatched)) != 0;
            if (fact.Lifecycle == SafetyRuntimeLifecycle.ControlDispatched &&
                !acceptedObserved)
                return Result(SafetyRuntimeCorrelationStatus.Stale,
                    "SAFETY_REQUEST_ACCEPTED_NOT_OBSERVED");
            if (fact.Lifecycle == SafetyRuntimeLifecycle.EffectConfirmed &&
                !dispatchObserved)
                return Result(SafetyRuntimeCorrelationStatus.Stale,
                    "SAFETY_CONTROL_DISPATCH_NOT_OBSERVED");

            bool terminal = fact.Lifecycle == SafetyRuntimeLifecycle.EffectConfirmed ||
                fact.Lifecycle == SafetyRuntimeLifecycle.Rejected ||
                fact.Lifecycle == SafetyRuntimeLifecycle.Failed ||
                fact.Lifecycle == SafetyRuntimeLifecycle.Interrupted;
            entries[fact.RuntimeControlToken] = new Entry(
                entry.Request, entry.RuntimeGeneration,
                entry.ObservedMask | mask, terminal);
            return Result(SafetyRuntimeCorrelationStatus.Accepted, "");
        }

        public void Clear()
        {
            entries.Clear();
            order.Clear();
        }

        private bool EnsureCapacity()
        {
            if (entries.Count < MaxRuntimeControls) return true;
            int remaining = order.Count;
            while (entries.Count >= MaxRuntimeControls && remaining-- > 0)
            {
                string token = order.Dequeue();
                Entry candidate;
                if (!entries.TryGetValue(token, out candidate)) continue;
                if (candidate.Terminal) entries.Remove(token);
                else order.Enqueue(token);
            }
            return entries.Count < MaxRuntimeControls;
        }

        private static bool Matches(
            Entry entry, SafetyControlRequest request,
            SafetyControlRuntimeFact fact)
        {
            return entry.RuntimeGeneration == fact.RuntimeGeneration &&
                Same(entry.Request.ControlRequestId, request.ControlRequestId) &&
                Same(entry.Request.PlanId, request.PlanId) &&
                Same(entry.Request.TargetStepId, request.TargetStepId) &&
                Same(entry.Request.TargetAttemptId, request.TargetAttemptId) &&
                entry.Request.Scope == request.Scope &&
                entry.Request.ControlKind == request.ControlKind &&
                request.ControlKind == fact.ControlKind;
        }

        private static int Mask(SafetyRuntimeLifecycle lifecycle)
        {
            return 1 << (int)lifecycle;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static SafetyRuntimeCorrelationResult Result(
            SafetyRuntimeCorrelationStatus status, string reason)
        {
            return new SafetyRuntimeCorrelationResult(status, reason);
        }

        private sealed class Entry
        {
            public SafetyControlRequest Request { get; }
            public int RuntimeGeneration { get; }
            public int ObservedMask { get; }
            public bool Terminal { get; }
            public Entry(SafetyControlRequest request, int generation,
                int observedMask, bool terminal)
            {
                Request = request;
                RuntimeGeneration = generation;
                ObservedMask = observedMask;
                Terminal = terminal;
            }
        }
    }
}
