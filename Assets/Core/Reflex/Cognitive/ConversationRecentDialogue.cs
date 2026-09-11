// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;

namespace SalieriAI.Core.Reflex.Cognitive
{
    /// <summary>
    /// One completed normal-Conversation pair retained only in runtime-local
    /// working memory. IDs and terminal state are correlation metadata; prompt
    /// formatting exposes only UserText and AssistantText.
    /// </summary>
    public sealed class ConversationRecentDialogueTurn
    {
        public const int MaxTextLength = 160;

        public string TurnId { get; }
        public string UserText { get; }
        public string AssistantText { get; }
        public ConversationTurnLifecycleState TerminalState { get; }
        public DateTime CompletedAtUtc { get; }

        private ConversationRecentDialogueTurn(
            string turnId,
            string userText,
            string assistantText,
            ConversationTurnLifecycleState terminalState,
            DateTime completedAtUtc)
        {
            TurnId = Text(turnId);
            UserText = Text(userText);
            AssistantText = Text(assistantText);
            TerminalState = terminalState;
            CompletedAtUtc = Utc(completedAtUtc);
        }

        internal static bool TryCreateCompleted(
            ConversationTurnLifecycleSnapshot transition,
            out ConversationRecentDialogueTurn dialogueTurn)
        {
            dialogueTurn = null;
            if (transition == null ||
                transition.State != ConversationTurnLifecycleState.Completed ||
                transition.TurnContext == null ||
                transition.ReactionOutcome == null)
            {
                return false;
            }

            string userText = Text(transition.TurnContext.UserText);
            string assistantText = Text(transition.ReactionOutcome.Text);
            if (string.IsNullOrWhiteSpace(userText) ||
                string.IsNullOrWhiteSpace(assistantText))
            {
                return false;
            }

            dialogueTurn = new ConversationRecentDialogueTurn(
                transition.TurnId,
                userText,
                assistantText,
                transition.State,
                transition.UpdatedAtUtc);
            return true;
        }

        private static string Text(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result = value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            return result.Length <= MaxTextLength
                ? result
                : result.Substring(0, MaxTextLength);
        }

        private static DateTime Utc(DateTime value)
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
