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
using System.Threading;

namespace SalieriAI.Core.Skills
{
    /// <summary>
    /// Read-only data supplied to one Skill execution.
    /// Scene objects and safety controllers are intentionally excluded in v0.1.
    /// </summary>
    public sealed class SkillExecutionContext
    {
        private readonly IReadOnlyDictionary<string, string> contextValues;

        public string RunId { get; }

        public string PlanId { get; }

        public string StepId { get; }

        public DateTime StartedAt { get; }

        public string Platform { get; }

        public CancellationToken CancellationToken { get; }

        public IReadOnlyDictionary<string, string> ContextValues => contextValues;

        public SkillExecutionContext(
            string runId,
            string planId,
            string stepId,
            DateTime startedAt,
            string platform,
            CancellationToken cancellationToken,
            IReadOnlyDictionary<string, string> values = null)
        {
            RunId = runId ?? string.Empty;
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            DateTime safeStartedAt = startedAt == default(DateTime)
                ? DateTime.UtcNow
                : startedAt;

            StartedAt = safeStartedAt.Kind == DateTimeKind.Utc
                ? safeStartedAt
                : safeStartedAt.ToUniversalTime();
            Platform = platform ?? string.Empty;
            CancellationToken = cancellationToken;

            Dictionary<string, string> copy =
                new Dictionary<string, string>(StringComparer.Ordinal);

            if (values != null)
            {
                foreach (KeyValuePair<string, string> pair in values)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                        continue;

                    copy[pair.Key] = pair.Value ?? string.Empty;
                }
            }

            contextValues = new ReadOnlyDictionary<string, string>(copy);
        }

        public bool TryGetValue(string key, out string value)
        {
            value = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
                return false;

            return contextValues.TryGetValue(key, out value);
        }
    }
}
