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
    /// <summary>
    /// Side-effect-free result contract for CGL analysis.
    /// Phase 2A adds deterministic shadow fields without changing production routing.
    /// </summary>
    [Serializable]
    public sealed class CglInterpretation
    {
        public string AnalysisId { get; }

        public string InputId { get; }

        public string InteractionId { get; }

        public string NormalizedText { get; }

        public string SemanticForm { get; }

        public bool ActivationMatched { get; }

        public string RequestType { get; }

        public string ActionCandidate { get; }

        public string DirectionCandidate { get; }

        public string InterpretationStatus { get; }

        public string ParserVersion { get; }

        public bool Unknown { get; }

        public bool Ambiguous { get; }

        public bool Unresolved { get; }

        public string CglVersion { get; }

        public DateTime CompletedAtUtc { get; }

        public CglInterpretation(
            string analysisId,
            string inputId,
            string interactionId,
            string normalizedText,
            string semanticForm,
            bool unknown,
            bool ambiguous,
            bool unresolved,
            string cglVersion,
            DateTime completedAtUtc)
            : this(
                analysisId,
                inputId,
                interactionId,
                normalizedText,
                semanticForm,
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                unknown ? "UNRESOLVED" : "RESOLVED",
                cglVersion,
                unknown,
                ambiguous,
                unresolved,
                cglVersion,
                completedAtUtc)
        {
        }

        public CglInterpretation(
            string analysisId,
            string inputId,
            string interactionId,
            string normalizedText,
            string semanticForm,
            bool activationMatched,
            string requestType,
            string actionCandidate,
            string directionCandidate,
            string interpretationStatus,
            string parserVersion,
            bool unknown,
            bool ambiguous,
            bool unresolved,
            string cglVersion,
            DateTime completedAtUtc)
        {
            AnalysisId = NormalizeId(analysisId, "analysis");
            InputId = inputId ?? string.Empty;
            InteractionId = interactionId ?? string.Empty;
            NormalizedText = normalizedText ?? string.Empty;
            SemanticForm = semanticForm ?? string.Empty;
            ActivationMatched = activationMatched;
            RequestType = requestType ?? string.Empty;
            ActionCandidate = actionCandidate ?? string.Empty;
            DirectionCandidate = directionCandidate ?? string.Empty;
            InterpretationStatus = interpretationStatus ?? string.Empty;
            ParserVersion = parserVersion ?? string.Empty;
            Unknown = unknown;
            Ambiguous = ambiguous;
            Unresolved = unresolved;
            CglVersion = cglVersion ?? string.Empty;
            CompletedAtUtc = NormalizeUtc(completedAtUtc);
        }

        private static string NormalizeId(string value, string prefix)
        {
            return string.IsNullOrWhiteSpace(value)
                ? CommunicationIdGenerator.Create(prefix)
                : value.Trim();
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            DateTime safe = value == default(DateTime)
                ? DateTime.UtcNow
                : value;

            return safe.Kind == DateTimeKind.Utc
                ? safe
                : safe.ToUniversalTime();
        }
    }
}
