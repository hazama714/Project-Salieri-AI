// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Language.Contracts
{
    [Serializable]
    public sealed class CglLegacyComparisonRecord
    {
        public string InputId { get; }
        public string InteractionId { get; }
        public string NormalizedText { get; }
        public string CglRequestType { get; }
        public string CglActionCandidate { get; }
        public string CglDirectionCandidate { get; }
        public string LegacyRoute { get; }
        public string LegacyActionId { get; }
        public string LegacyDirection { get; }
        public string ComparisonResult { get; }
        public string MismatchReason { get; }
        public DateTime ComparedAtUtc { get; }
        public string ParserVersion { get; }

        public CglLegacyComparisonRecord(
            string inputId,
            string interactionId,
            string normalizedText,
            string cglRequestType,
            string cglActionCandidate,
            string cglDirectionCandidate,
            string legacyRoute,
            string legacyActionId,
            string legacyDirection,
            string comparisonResult,
            string mismatchReason,
            DateTime comparedAtUtc,
            string parserVersion)
        {
            InputId = inputId ?? string.Empty;
            InteractionId = interactionId ?? string.Empty;
            NormalizedText = normalizedText ?? string.Empty;
            CglRequestType = cglRequestType ?? string.Empty;
            CglActionCandidate = cglActionCandidate ?? string.Empty;
            CglDirectionCandidate = cglDirectionCandidate ?? string.Empty;
            LegacyRoute = legacyRoute ?? string.Empty;
            LegacyActionId = legacyActionId ?? string.Empty;
            LegacyDirection = legacyDirection ?? string.Empty;
            ComparisonResult = comparisonResult ?? string.Empty;
            MismatchReason = mismatchReason ?? string.Empty;
            ParserVersion = parserVersion ?? string.Empty;

            DateTime safe = comparedAtUtc == default(DateTime)
                ? DateTime.UtcNow
                : comparedAtUtc;

            ComparedAtUtc = safe.Kind == DateTimeKind.Utc
                ? safe
                : safe.ToUniversalTime();
        }
    }

    public static class CglLegacyComparisonResults
    {
        public const string Match = "MATCH";
        public const string RouteMismatch = "ROUTE_MISMATCH";
        public const string ActionMismatch = "ACTION_MISMATCH";
        public const string DirectionMismatch = "DIRECTION_MISMATCH";
        public const string CglUnresolved = "CGL_UNRESOLVED";
        public const string LegacyUnresolved = "LEGACY_UNRESOLVED";
        public const string ComparisonUnavailable = "COMPARISON_UNAVAILABLE";
        public const string ComparisonFailed = "COMPARISON_FAILED";
    }
}

