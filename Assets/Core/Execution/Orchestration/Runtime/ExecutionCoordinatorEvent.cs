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

namespace SalieriAI.Core.Execution.Orchestration.Runtime
{
    /// <summary>
    /// Immutable fact offered to a future coordinator reducer. IDs that do
    /// not apply to the selected event type remain empty; another ID must
    /// never be substituted for them.
    /// </summary>
    public sealed class ExecutionCoordinatorEvent
    {
        public string EventId { get; }
        public ExecutionCoordinatorEventType EventType { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string ControlRequestId { get; }
        public DateTime OccurredAtUtc { get; }
        public string Source { get; }
        public int Generation { get; }
        public ExecutionDomainStage DomainStage { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }
        public IReadOnlyList<string> FailureCodes { get; }
        public string DiagnosticMessage { get; }
        public ExecutionResourceLeaseEventPayload ResourceLease { get; }
        public ExecutionResourceAvailabilityEventPayload
            ResourceAvailability { get; }
        public ExecutionResourceWaitTimeoutEventPayload
            ResourceWaitTimeout { get; }

        public ExecutionCoordinatorEvent(
            string eventId,
            ExecutionCoordinatorEventType eventType,
            string planId,
            string stepId,
            string executionAttemptId,
            string controlRequestId,
            DateTime occurredAtUtc,
            string source,
            int generation,
            ExecutionDomainStage domainStage,
            ExecutionTerminalOutcome terminalOutcome,
            IEnumerable<string> failureCodes,
            string diagnosticMessage)
            : this(
                eventId,
                eventType,
                planId,
                stepId,
                executionAttemptId,
                controlRequestId,
                occurredAtUtc,
                source,
                generation,
                domainStage,
                terminalOutcome,
                failureCodes,
                diagnosticMessage,
                null,
                null,
                null)
        {
        }

        public ExecutionCoordinatorEvent(
            string eventId,
            ExecutionCoordinatorEventType eventType,
            string planId,
            string stepId,
            string executionAttemptId,
            string controlRequestId,
            DateTime occurredAtUtc,
            string source,
            int generation,
            ExecutionDomainStage domainStage,
            ExecutionTerminalOutcome terminalOutcome,
            IEnumerable<string> failureCodes,
            string diagnosticMessage,
            ExecutionResourceLeaseEventPayload resourceLease)
            : this(
                eventId,
                eventType,
                planId,
                stepId,
                executionAttemptId,
                controlRequestId,
                occurredAtUtc,
                source,
                generation,
                domainStage,
                terminalOutcome,
                failureCodes,
                diagnosticMessage,
                resourceLease,
                null,
                null)
        {
        }

        public ExecutionCoordinatorEvent(
            string eventId,
            ExecutionCoordinatorEventType eventType,
            string planId,
            string stepId,
            string executionAttemptId,
            string controlRequestId,
            DateTime occurredAtUtc,
            string source,
            int generation,
            ExecutionDomainStage domainStage,
            ExecutionTerminalOutcome terminalOutcome,
            IEnumerable<string> failureCodes,
            string diagnosticMessage,
            ExecutionResourceLeaseEventPayload resourceLease,
            ExecutionResourceAvailabilityEventPayload
                resourceAvailability,
            ExecutionResourceWaitTimeoutEventPayload resourceWaitTimeout)
        {
            EventId = Text(eventId);
            EventType = eventType;
            PlanId = Text(planId);
            StepId = Text(stepId);
            ExecutionAttemptId = Text(executionAttemptId);
            ControlRequestId = Text(controlRequestId);
            OccurredAtUtc = Utc(occurredAtUtc);
            Source = Text(source);
            Generation = generation;
            DomainStage = domainStage;
            TerminalOutcome = terminalOutcome;
            FailureCodes = Copy(failureCodes);
            DiagnosticMessage = Text(diagnosticMessage);
            ResourceLease = resourceLease;
            ResourceAvailability = resourceAvailability;
            ResourceWaitTimeout = resourceWaitTimeout;
        }

        private static string Text(string value)
        {
            return value ?? string.Empty;
        }

        private static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime) ||
                value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
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
}
