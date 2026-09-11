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

namespace SalieriAI.Core.Runtime
{
    public enum SafetyRuntimeLifecycle
    {
        RequestObserved = 0,
        RequestAccepted = 1,
        ControlDispatched = 2,
        EffectConfirmed = 3,
        Rejected = 4,
        Failed = 5,
        Interrupted = 6
    }

    /// <summary>Observed capability of the current runtime, not a request.</summary>
    public enum SafetyRuntimeActualScope
    {
        Unknown = 0,
        CrawlerOnly = 1,
        BodyPartial = 2,
        FullBody = 3,
        ArmOutputOnly = 4
    }

    public enum SafetyRuntimeVerificationLevel
    {
        RequestOnly = 0,
        DispatchConfirmed = 1,
        EffectUnverified = 2,
        EffectVerified = 3
    }

    /// <summary>
    /// Immutable read-only fact from a legacy safety boundary. It deliberately
    /// owns no coordinator IDs and never claims physical verification unless
    /// the source has explicit feedback evidence.
    /// </summary>
    public sealed class SafetyControlRuntimeFact
    {
        public string RuntimeControlToken { get; }
        public int RuntimeGeneration { get; }
        public ExecutionControlType ControlKind { get; }
        public SafetyRuntimeActualScope RuntimeScope { get; }
        public SafetyRuntimeLifecycle Lifecycle { get; }
        public string TargetId { get; }
        public DateTime OccurredAtUtc { get; }
        public string FailureReason { get; }
        public SafetyRuntimeVerificationLevel VerificationLevel { get; }
        public string Diagnostic { get; }

        public SafetyControlRuntimeFact(
            string runtimeControlToken, int runtimeGeneration,
            ExecutionControlType controlKind,
            SafetyRuntimeActualScope runtimeScope,
            SafetyRuntimeLifecycle lifecycle, string targetId,
            DateTime occurredAtUtc, string failureReason,
            SafetyRuntimeVerificationLevel verificationLevel,
            string diagnostic)
        {
            RuntimeControlToken = runtimeControlToken ?? string.Empty;
            RuntimeGeneration = runtimeGeneration;
            ControlKind = controlKind;
            RuntimeScope = runtimeScope;
            Lifecycle = lifecycle;
            TargetId = targetId ?? string.Empty;
            OccurredAtUtc = NormalizeUtc(occurredAtUtc);
            FailureReason = failureReason ?? string.Empty;
            VerificationLevel = verificationLevel;
            Diagnostic = diagnostic ?? string.Empty;
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default(DateTime) || value.Kind == DateTimeKind.Utc)
                return value;
            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
