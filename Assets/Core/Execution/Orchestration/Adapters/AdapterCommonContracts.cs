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
using System.Collections.ObjectModel;

namespace SalieriAI.Core.Execution.Orchestration.Adapters
{
    public sealed class AdapterTranslationResult<T> where T : class
    {
        public bool IsValid { get; }
        public T Value { get; }
        public IReadOnlyList<string> FailureCodes { get; }
        public string DiagnosticMessage { get; }

        private AdapterTranslationResult(
            T value,
            IEnumerable<string> failures,
            string diagnostic)
        {
            Value = value;
            FailureCodes = new ReadOnlyCollection<string>(
                failures != null
                    ? new List<string>(failures)
                    : new List<string>());
            IsValid = value != null && FailureCodes.Count == 0;
            DiagnosticMessage = diagnostic ?? string.Empty;
        }

        public static AdapterTranslationResult<T> Success(T value)
        {
            return new AdapterTranslationResult<T>(
                value, new string[0], string.Empty);
        }

        public static AdapterTranslationResult<T> Failure(
            string code, string diagnostic)
        {
            return new AdapterTranslationResult<T>(
                null, new[] { code ?? string.Empty }, diagnostic);
        }
    }

    public static class ExecutionAdapterFailureCodes
    {
        public const string NullSource = "ADAPTER_NULL_SOURCE";
        public const string WrongDomain = "ADAPTER_WRONG_DOMAIN";
        public const string MissingId = "ADAPTER_MISSING_ID";
        public const string CorrelationMismatch =
            "ADAPTER_CORRELATION_MISMATCH";
        public const string InvalidStatus = "ADAPTER_INVALID_STATUS";
        public const string InvalidScope = "ADAPTER_INVALID_SCOPE";
        public const string InvalidPayload = "ADAPTER_INVALID_PAYLOAD";
        public const string PendingHasNoEvent =
            "ADAPTER_PENDING_HAS_NO_EVENT";
        public const string RuntimeFailureHasNoCoordinatorFact =
            "ADAPTER_RUNTIME_FAILURE_HAS_NO_COORDINATOR_FACT";
        public const string SnapshotInvalid = "ADAPTER_SNAPSHOT_INVALID";
    }

    internal static class AdapterContractUtility
    {
        public static string Text(string value)
        {
            return value ?? string.Empty;
        }

        public static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime) ||
                value.Kind == DateTimeKind.Utc)
                return value;
            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        public static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>(
                values != null
                    ? new List<T>(values)
                    : new List<T>());
        }
    }
}
