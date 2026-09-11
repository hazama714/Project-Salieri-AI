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
using SalieriAI.Core.Execution.Orchestration.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Reduction
{
    /// <summary>
    /// Immutable description of an external effect requested by a future
    /// reducer. Creating an intent does not mean that the effect started,
    /// succeeded, or reached its completion policy.
    /// </summary>
    public sealed class ExecutionCoordinatorEffectIntent
    {
        public string IntentId { get; }
        public ExecutionCoordinatorEffectIntentType IntentType { get; }
        public string PlanId { get; }
        public string StepId { get; }
        public string ExecutionAttemptId { get; }
        public string RequestId { get; }
        public string ControlRequestId { get; }
        public DateTime CreatedAtUtc { get; }
        public IReadOnlyList<ExecutionResourceRequirement>
            ResourceRequirements { get; }
        public ExecutionTimeoutPolicy TimeoutPolicy { get; }
        public string DiagnosticMessage { get; }
        public ExecutionResourceLeaseIntentPayload ResourceLease { get; }

        public ExecutionCoordinatorEffectIntent(
            string intentId,
            ExecutionCoordinatorEffectIntentType intentType,
            string planId,
            string stepId,
            string executionAttemptId,
            string requestId,
            string controlRequestId,
            DateTime createdAtUtc,
            IEnumerable<ExecutionResourceRequirement>
                resourceRequirements,
            ExecutionTimeoutPolicy timeoutPolicy,
            string diagnosticMessage)
            : this(
                intentId,
                intentType,
                planId,
                stepId,
                executionAttemptId,
                requestId,
                controlRequestId,
                createdAtUtc,
                resourceRequirements,
                timeoutPolicy,
                diagnosticMessage,
                null)
        {
        }

        public ExecutionCoordinatorEffectIntent(
            string intentId,
            ExecutionCoordinatorEffectIntentType intentType,
            string planId,
            string stepId,
            string executionAttemptId,
            string requestId,
            string controlRequestId,
            DateTime createdAtUtc,
            IEnumerable<ExecutionResourceRequirement>
                resourceRequirements,
            ExecutionTimeoutPolicy timeoutPolicy,
            string diagnosticMessage,
            ExecutionResourceLeaseIntentPayload resourceLease)
        {
            IntentId = Text(intentId);
            IntentType = intentType;
            PlanId = Text(planId);
            StepId = Text(stepId);
            ExecutionAttemptId = Text(executionAttemptId);
            RequestId = Text(requestId);
            ControlRequestId = Text(controlRequestId);
            CreatedAtUtc = Utc(createdAtUtc);
            ResourceRequirements = Copy(resourceRequirements);
            TimeoutPolicy = timeoutPolicy;
            DiagnosticMessage = Text(diagnosticMessage);
            ResourceLease = resourceLease;
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

        private static IReadOnlyList<T> Copy<T>(
            IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(
                source != null
                    ? new List<T>(source)
                    : new List<T>()
            );
        }
    }
}
