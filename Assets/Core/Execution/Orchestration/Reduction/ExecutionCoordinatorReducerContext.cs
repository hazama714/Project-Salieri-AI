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

namespace SalieriAI.Core.Execution.Orchestration.Reduction
{
    /// <summary>
    /// All non-deterministic values needed by a reduction are supplied by
    /// the caller. It deliberately contains no delegate or runtime service.
    /// </summary>
    public sealed class ExecutionCoordinatorReducerContext
    {
        public DateTime NowUtc { get; }
        public string NextExecutionAttemptId { get; }
        public string NextRequestId { get; }
        public IReadOnlyList<string> NextIntentIds { get; }
        public int Generation { get; }

        public ExecutionCoordinatorReducerContext(
            DateTime nowUtc,
            string nextExecutionAttemptId,
            string nextRequestId,
            IEnumerable<string> nextIntentIds,
            int generation)
        {
            NowUtc = NormalizeUtc(nowUtc);
            NextExecutionAttemptId =
                nextExecutionAttemptId ?? string.Empty;
            NextRequestId = nextRequestId ?? string.Empty;
            NextIntentIds = new ReadOnlyCollection<string>(
                nextIntentIds != null
                    ? new List<string>(nextIntentIds)
                    : new List<string>()
            );
            Generation = generation;
        }

        private static DateTime NormalizeUtc(DateTime value)
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
    }
}
