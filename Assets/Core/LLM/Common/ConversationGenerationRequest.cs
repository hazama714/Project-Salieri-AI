// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Text;

using SalieriAI.Body.Semantics;
using SalieriAI.Core.Reflex.Cognitive;
using SalieriAI.Core.Reflex.Cognitive.Grounding;

namespace SalieriAI.Core.LLM.Common
{
    /// <summary>
    /// Immutable prompt-facing conversation request. It carries a frozen
    /// semantic snapshot and never a live referent provider.
    /// </summary>
    public sealed class ConversationGenerationRequest
    {
        public string UserText { get; }
        public string InputId { get; }
        public string InteractionId { get; }
        public ConversationObjectGrounding ObjectGrounding { get; }
        public IReadOnlyList<ConversationRecentDialogueTurn> RecentDialogue
        { get; }
        public SemanticBodySnapshot SemanticBodySnapshot { get; }

        public ConversationGenerationRequest(
            string userText,
            string inputId,
            string interactionId,
            ConversationObjectGrounding objectGrounding)
            : this(
                userText,
                inputId,
                interactionId,
                objectGrounding,
                null,
                null)
        {
        }

        public ConversationGenerationRequest(
            string userText,
            string inputId,
            string interactionId,
            ConversationObjectGrounding objectGrounding,
            IEnumerable<ConversationRecentDialogueTurn> recentDialogue)
            : this(
                userText,
                inputId,
                interactionId,
                objectGrounding,
                recentDialogue,
                null)
        {
        }

        public ConversationGenerationRequest(
            string userText,
            string inputId,
            string interactionId,
            ConversationObjectGrounding objectGrounding,
            IEnumerable<ConversationRecentDialogueTurn> recentDialogue,
            SemanticBodySnapshot semanticBodySnapshot)
        {
            UserText = userText == null ? string.Empty : userText.Trim();
            InputId = inputId == null ? string.Empty : inputId.Trim();
            InteractionId = interactionId == null
                ? string.Empty
                : interactionId.Trim();
            ObjectGrounding = objectGrounding;
            var dialogueCopy = recentDialogue != null
                ? new List<ConversationRecentDialogueTurn>(recentDialogue)
                : new List<ConversationRecentDialogueTurn>();
            RecentDialogue = dialogueCopy.AsReadOnly();
            SemanticBodySnapshot = semanticBodySnapshot;
        }

        public static ConversationGenerationRequest FromTurn(
            ConversationTurnContext turn)
        {
            return FromTurn(turn, null);
        }

        public static ConversationGenerationRequest FromTurn(
            ConversationTurnContext turn,
            IEnumerable<ConversationRecentDialogueTurn> recentDialogue)
        {
            return FromTurn(turn, recentDialogue, null);
        }

        public static ConversationGenerationRequest FromTurn(
            ConversationTurnContext turn,
            IEnumerable<ConversationRecentDialogueTurn> recentDialogue,
            SemanticBodySnapshot semanticBodySnapshot)
        {
            if (turn == null)
                return new ConversationGenerationRequest(
                    string.Empty, string.Empty, string.Empty, null,
                    recentDialogue, semanticBodySnapshot);

            return new ConversationGenerationRequest(
                turn.UserText,
                turn.Input != null ? turn.Input.InputId : string.Empty,
                turn.Input != null
                    ? turn.Input.InteractionId
                    : string.Empty,
                turn.ObjectGrounding,
                recentDialogue,
                semanticBodySnapshot);
        }

        public static ConversationGenerationRequest FromText(string userText)
        {
            return new ConversationGenerationRequest(
                userText, string.Empty, string.Empty, null);
        }
    }

    /// <summary>
    /// Shared prompt-only rendering for Cloud and Local providers. Runtime
    /// correlation IDs and terminal metadata are intentionally not emitted.
    /// </summary>
    public static class ConversationRecentDialoguePromptFormatter
    {
        public static string Build(
            IReadOnlyList<ConversationRecentDialogueTurn> recentDialogue)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Recent conversation:");

            if (recentDialogue == null || recentDialogue.Count == 0)
            {
                sb.Append("(none)");
                return sb.ToString();
            }

            for (int i = 0; i < recentDialogue.Count; i++)
            {
                ConversationRecentDialogueTurn turn = recentDialogue[i];
                if (turn == null)
                    continue;

                sb.Append("User: ");
                sb.AppendLine(SafeLine(turn.UserText));
                sb.Append("Assistant: ");
                sb.Append(SafeLine(turn.AssistantText));
                if (i + 1 < recentDialogue.Count)
                    sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string SafeLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result = value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            return result.Length <=
                ConversationRecentDialogueTurn.MaxTextLength
                ? result
                : result.Substring(
                    0, ConversationRecentDialogueTurn.MaxTextLength);
        }
    }

    /// <summary>
    /// Shared deterministic representation used by both Local and Cloud
    /// conversation prompt builders. No inference or fallback identity is
    /// performed here.
    /// </summary>
    public static class ConversationGroundingPromptFormatter
    {
        public static string Build(ConversationObjectGrounding grounding)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[TURN_OBJECT_GROUNDING_V0]");

            if (grounding == null || !grounding.HasReferent)
            {
                sb.AppendLine("available=false");
                sb.AppendLine("referent_source=" +
                    (grounding != null
                        ? grounding.ReferentSource.ToString()
                        : "None"));
                sb.AppendLine("memory_status=None");
                sb.AppendLine("known_name=");
                sb.AppendLine("experience_record_id=");
                sb.AppendLine("resolution_status=" +
                    (grounding != null
                        ? grounding.Status.ToString()
                        : "NoGrounding"));
            }
            else
            {
                FrozenObjectReferentSnapshot referent = grounding.Referent;
                sb.AppendLine("available=true");
                sb.AppendLine("referent_source=" + grounding.ReferentSource);
                sb.AppendLine("memory_status=" + referent.MemoryStatus);
                sb.AppendLine("known_name=" +
                    (referent.HasUsableKnownIdentity
                        ? SafeLine(referent.KnownName)
                        : string.Empty));
                sb.AppendLine("experience_record_id=" +
                    (referent.HasUsableKnownIdentity
                        ? SafeLine(referent.ExperienceRecordId)
                        : string.Empty));
                sb.AppendLine("resolution_status=" + grounding.Status);
            }

            sb.Append("[/TURN_OBJECT_GROUNDING_V0]");
            return sb.ToString();
        }

        private static string SafeLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string result = value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            return result.Length <= 256
                ? result
                : result.Substring(0, 256);
        }
    }
}
