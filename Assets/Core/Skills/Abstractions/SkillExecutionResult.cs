// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Skills
{
    /// <summary>
    /// Non-generic result envelope used by the registry and plan runner.
    /// Typed handlers return SkillExecutionResult&lt;TPayload&gt;.
    /// </summary>
    public class SkillExecutionResult
    {
        public SkillExecutionStatus Status { get; private set; }

        public ISkillPayload Payload { get; private set; }

        public string FailureReason { get; private set; }

        public string Message { get; private set; }

        public DateTime StartedAt { get; private set; }

        public DateTime FinishedAt { get; private set; }

        public TimeSpan Duration { get; private set; }

        public bool Retryable { get; private set; }

        public bool IsSuccess => Status == SkillExecutionStatus.Succeeded;

        protected SkillExecutionResult(
            SkillExecutionStatus status,
            ISkillPayload payload,
            string failureReason,
            string message,
            DateTime startedAt,
            DateTime finishedAt,
            bool retryable)
        {
            Status = status;
            Payload = payload;
            FailureReason = failureReason ?? string.Empty;
            Message = message ?? string.Empty;
            StartedAt = NormalizeUtc(startedAt, finishedAt);
            FinishedAt = NormalizeUtc(finishedAt, DateTime.UtcNow);

            if (FinishedAt < StartedAt)
                FinishedAt = StartedAt;

            Duration = FinishedAt - StartedAt;
            Retryable = retryable;
        }

        public static SkillExecutionResult Create(
            SkillExecutionStatus status,
            ISkillPayload payload = null,
            string failureReason = "",
            string message = "",
            DateTime startedAt = default(DateTime),
            bool retryable = false)
        {
            DateTime finishedAt = DateTime.UtcNow;

            return new SkillExecutionResult(
                status,
                payload,
                failureReason,
                message,
                startedAt,
                finishedAt,
                retryable
            );
        }

        public static SkillExecutionResult InvalidInput(
            string message,
            DateTime startedAt = default(DateTime))
        {
            return Create(
                SkillExecutionStatus.InvalidInput,
                failureReason: "invalid_input",
                message: message,
                startedAt: startedAt
            );
        }

        public static SkillExecutionResult Failed(
            string failureReason,
            string message,
            DateTime startedAt = default(DateTime),
            bool retryable = false)
        {
            return Create(
                SkillExecutionStatus.Failed,
                failureReason: failureReason,
                message: message,
                startedAt: startedAt,
                retryable: retryable
            );
        }

        public static SkillExecutionResult Cancelled(
            string message,
            DateTime startedAt = default(DateTime))
        {
            return Create(
                SkillExecutionStatus.Cancelled,
                failureReason: "cancelled",
                message: message,
                startedAt: startedAt
            );
        }

        public static SkillExecutionResult TimedOut(
            string message,
            DateTime startedAt = default(DateTime))
        {
            return Create(
                SkillExecutionStatus.TimedOut,
                failureReason: "timed_out",
                message: message,
                startedAt: startedAt,
                retryable: true
            );
        }

        internal void EnsureTiming(DateTime fallbackStartedAt)
        {
            DateTime finishedAt = FinishedAt == default(DateTime)
                ? DateTime.UtcNow
                : FinishedAt;

            DateTime startedAt = StartedAt == default(DateTime)
                ? NormalizeUtc(fallbackStartedAt, finishedAt)
                : StartedAt;

            if (finishedAt < startedAt)
                finishedAt = startedAt;

            StartedAt = startedAt;
            FinishedAt = finishedAt;
            Duration = finishedAt - startedAt;
        }

        private static DateTime NormalizeUtc(DateTime value, DateTime fallback)
        {
            DateTime result = value == default(DateTime) ? fallback : value;

            if (result.Kind == DateTimeKind.Utc)
                return result;

            return result.ToUniversalTime();
        }
    }

    public sealed class SkillExecutionResult<TPayload> : SkillExecutionResult
        where TPayload : ISkillPayload
    {
        public new TPayload Payload => (TPayload)base.Payload;

        private SkillExecutionResult(
            SkillExecutionStatus status,
            TPayload payload,
            string failureReason,
            string message,
            DateTime startedAt,
            DateTime finishedAt,
            bool retryable)
            : base(
                status,
                payload,
                failureReason,
                message,
                startedAt,
                finishedAt,
                retryable)
        {
        }

        public static SkillExecutionResult<TPayload> Create(
            SkillExecutionStatus status,
            TPayload payload = default(TPayload),
            string failureReason = "",
            string message = "",
            DateTime startedAt = default(DateTime),
            bool retryable = false)
        {
            return new SkillExecutionResult<TPayload>(
                status,
                payload,
                failureReason,
                message,
                startedAt,
                DateTime.UtcNow,
                retryable
            );
        }

        public static SkillExecutionResult<TPayload> Succeeded(
            TPayload payload,
            string message = "",
            DateTime startedAt = default(DateTime))
        {
            return Create(
                SkillExecutionStatus.Succeeded,
                payload,
                message: message,
                startedAt: startedAt
            );
        }

        public new static SkillExecutionResult<TPayload> InvalidInput(
            string message,
            DateTime startedAt = default(DateTime))
        {
            return Create(
                SkillExecutionStatus.InvalidInput,
                failureReason: "invalid_input",
                message: message,
                startedAt: startedAt
            );
        }

        public new static SkillExecutionResult<TPayload> Failed(
            string failureReason,
            string message,
            DateTime startedAt = default(DateTime),
            bool retryable = false)
        {
            return Create(
                SkillExecutionStatus.Failed,
                failureReason: failureReason,
                message: message,
                startedAt: startedAt,
                retryable: retryable
            );
        }

        public new static SkillExecutionResult<TPayload> Cancelled(
            string message,
            DateTime startedAt = default(DateTime))
        {
            return Create(
                SkillExecutionStatus.Cancelled,
                failureReason: "cancelled",
                message: message,
                startedAt: startedAt
            );
        }
    }
}
