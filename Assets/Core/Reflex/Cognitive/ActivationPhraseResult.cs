// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Reflex.Cognitive
{
    public sealed class ActivationPhraseResult
    {
        public bool IsActivated { get; }
        public string MatchedAlias { get; }
        public string RemainingText { get; }
        public string OriginalText { get; }

        public ActivationPhraseResult(
            bool isActivated,
            string matchedAlias,
            string remainingText,
            string originalText
        )
        {
            IsActivated = isActivated;
            MatchedAlias = matchedAlias ?? string.Empty;
            RemainingText = remainingText ?? string.Empty;
            OriginalText = originalText ?? string.Empty;
        }

        public static ActivationPhraseResult NotActivated(string originalText)
        {
            return new ActivationPhraseResult(
                false,
                string.Empty,
                originalText ?? string.Empty,
                originalText ?? string.Empty
            );
        }
    }
}
