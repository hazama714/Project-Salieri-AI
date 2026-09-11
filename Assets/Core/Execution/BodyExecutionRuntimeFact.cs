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
    public enum BodyRuntimeLifecycle
    {
        RequestAccepted = 0,
        RequestRejected = 1,
        VirtualTargetApplied = 2,
        VirtualBodyResolved = 3,
        PhysicalDispatchRequested = 4,
        PhysicalTransportWritten = 5,
        PhysicalCompletionUnverified = 6,
        PhysicalCompletionVerified = 7,
        Failed = 8,
        Interrupted = 9,
        SafetyPreempted = 10
    }

    public enum BodyPhysicalVerificationLevel
    {
        None = 0,
        Unverified = 1,
        Verified = 2
    }

    /// <summary>
    /// Immutable fact emitted by an existing body runtime boundary. It does
    /// not contain coordinator IDs and does not imply more physical evidence
    /// than the emitting boundary actually owns.
    /// </summary>
    public sealed class BodyExecutionRuntimeFact
    {
        public string RuntimeExecutionToken { get; }
        public int RuntimeGeneration { get; }
        public string ActionId { get; }
        public BodyRuntimeLifecycle Lifecycle { get; }
        public ExecutionDomainStage DomainStage { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }
        public BodyPhysicalVerificationLevel VerificationLevel { get; }
        public DateTime OccurredAtUtc { get; }
        public string FailureReason { get; }
        public string Diagnostic { get; }

        public BodyExecutionRuntimeFact(
            string runtimeExecutionToken,
            int runtimeGeneration,
            string actionId,
            BodyRuntimeLifecycle lifecycle,
            ExecutionDomainStage domainStage,
            ExecutionTerminalOutcome terminalOutcome,
            BodyPhysicalVerificationLevel verificationLevel,
            DateTime occurredAtUtc,
            string failureReason,
            string diagnostic)
        {
            RuntimeExecutionToken = runtimeExecutionToken ?? string.Empty;
            RuntimeGeneration = runtimeGeneration;
            ActionId = actionId ?? string.Empty;
            Lifecycle = lifecycle;
            DomainStage = domainStage;
            TerminalOutcome = terminalOutcome;
            VerificationLevel = verificationLevel;
            OccurredAtUtc = NormalizeUtc(occurredAtUtc);
            FailureReason = failureReason ?? string.Empty;
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
