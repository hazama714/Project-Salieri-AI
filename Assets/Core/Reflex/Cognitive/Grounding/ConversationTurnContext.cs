// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Language.Contracts;

namespace SalieriAI.Core.Reflex.Cognitive.Grounding
{
    /// <summary>
    /// Immutable conversation-turn envelope. The grounding is captured before
    /// asynchronous generation begins and is never refreshed from live state.
    /// </summary>
    public sealed class ConversationTurnContext
    {
        public string TurnId { get; }
        public int RequestSerial { get; }
        public string UserText { get; }
        public CommunicationInput Input { get; }
        public ReactionDecision Decision { get; }
        public ConversationObjectGrounding ObjectGrounding { get; }
        public DateTime StartedAtUtc { get; }

        public ConversationTurnContext(
            int requestSerial,
            string userText,
            CommunicationInput input,
            ReactionDecision decision,
            ConversationObjectGrounding objectGrounding,
            DateTime startedAtUtc)
        {
            RequestSerial = requestSerial;
            UserText = userText == null ? string.Empty : userText.Trim();
            Input = input;
            Decision = decision;
            ObjectGrounding = objectGrounding;
            TurnId = input != null ? input.InputId : string.Empty;
            StartedAtUtc = Utc(startedAtUtc);
        }

        private static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime) || value.Kind == DateTimeKind.Utc)
                return value;
            return value.ToUniversalTime();
        }
    }
}
