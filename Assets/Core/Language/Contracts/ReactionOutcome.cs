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
    /// Phase 1 result envelope immediately before the existing output boundary.
    /// It records an output request, not physical audio playback success.
    /// </summary>
    [Serializable]
    public sealed class ReactionOutcome
    {
        public string OutputId { get; }

        public string InteractionId { get; }

        public string InputId { get; }

        public string ReactionId { get; }

        public string Source { get; }

        public string ResponseType { get; }

        public string Text { get; }

        public string Emotion { get; }

        public string SelectedSkillId { get; }

        public string SkillStatus { get; }

        public string Caller { get; }

        public bool SpeechRequested { get; }

        public DateTime TimestampUtc { get; }

        public ReactionOutcome(
            string outputId,
            string interactionId,
            string inputId,
            string reactionId,
            string source,
            string responseType,
            string text,
            string emotion,
            string selectedSkillId,
            string skillStatus,
            string caller,
            bool speechRequested,
            DateTime timestampUtc)
        {
            OutputId = string.IsNullOrWhiteSpace(outputId)
                ? CommunicationIdGenerator.Create("output")
                : outputId.Trim();
            InteractionId = interactionId ?? string.Empty;
            InputId = inputId ?? string.Empty;
            ReactionId = reactionId ?? string.Empty;
            Source = source ?? string.Empty;
            ResponseType = responseType ?? string.Empty;
            Text = text ?? string.Empty;
            Emotion = emotion ?? string.Empty;
            SelectedSkillId = selectedSkillId ?? string.Empty;
            SkillStatus = skillStatus ?? string.Empty;
            Caller = caller ?? string.Empty;
            SpeechRequested = speechRequested;

            DateTime safe = timestampUtc == default(DateTime)
                ? DateTime.UtcNow
                : timestampUtc;

            TimestampUtc = safe.Kind == DateTimeKind.Utc
                ? safe
                : safe.ToUniversalTime();
        }

        public static ReactionOutcome CreateOutputRequest(
            CommunicationInput input,
            ReactionDecision decision,
            string source,
            string responseType,
            string text,
            string emotion,
            string skillStatus,
            string caller)
        {
            return new ReactionOutcome(
                CommunicationIdGenerator.Create("output"),
                input != null ? input.InteractionId : string.Empty,
                input != null ? input.InputId : string.Empty,
                decision != null ? decision.ReactionId : string.Empty,
                source,
                responseType,
                text,
                emotion,
                decision != null ? decision.SelectedSkillId : string.Empty,
                skillStatus,
                caller,
                true,
                DateTime.UtcNow
            );
        }
    }
}
