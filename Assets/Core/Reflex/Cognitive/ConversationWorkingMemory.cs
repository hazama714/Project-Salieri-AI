// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using System.Collections.Generic;

using SalieriAI.Core.Language.Contracts;
using SalieriAI.Core.Reflex.Cognitive.Grounding;

using UnityEngine;

namespace SalieriAI.Core.Reflex.Cognitive
{
    /// <summary>
    /// Immutable, bounded short-term Conversation context. It contains the
    /// current normal Conversation turn, last terminal diagnostics, and a
    /// small completed-dialogue window. It is not an Experience/Recall store.
    /// </summary>
    public sealed class ConversationWorkingMemorySnapshot
    {
        public const string ContractVersion = "conversation-working-memory-v1";
        public const int RecentDialogueTurnCapacity = 3;

        public ConversationTurnLifecycleSnapshot CurrentTurn { get; }
        public ConversationTurnLifecycleSnapshot LastTerminalTurn { get; }
        public string LastUserText { get; }
        public string LastAssistantText { get; }
        public ConversationObjectGrounding LastGrounding { get; }
        public FrozenObjectReferentSnapshot LastObjectReferent { get; }
        public ReactionOutcome LastReactionOutcome { get; }
        public ConversationTurnLifecycleState? LastTerminalState { get; }
        public IReadOnlyList<ConversationRecentDialogueTurn> RecentDialogue
        { get; }
        public DateTime UpdatedAtUtc { get; }
        public string Version => ContractVersion;

        public bool HasCurrentTurn => CurrentTurn != null && !CurrentTurn.IsTerminal;
        public bool HasLastTerminalTurn => LastTerminalTurn != null;

        internal ConversationWorkingMemorySnapshot(
            ConversationTurnLifecycleSnapshot currentTurn,
            ConversationTurnLifecycleSnapshot lastTerminalTurn,
            string lastUserText,
            string lastAssistantText,
            ConversationObjectGrounding lastGrounding,
            FrozenObjectReferentSnapshot lastObjectReferent,
            ReactionOutcome lastReactionOutcome,
            ConversationTurnLifecycleState? lastTerminalState,
            IEnumerable<ConversationRecentDialogueTurn> recentDialogue,
            DateTime updatedAtUtc)
        {
            CurrentTurn = currentTurn != null && !currentTurn.IsTerminal
                ? currentTurn
                : null;
            LastTerminalTurn = lastTerminalTurn;
            LastUserText = Text(lastUserText);
            LastAssistantText = Text(lastAssistantText);
            LastGrounding = lastGrounding;
            LastObjectReferent = lastObjectReferent;
            LastReactionOutcome = lastReactionOutcome;
            LastTerminalState = lastTerminalState;
            var dialogueCopy = recentDialogue != null
                ? new List<ConversationRecentDialogueTurn>(recentDialogue)
                : new List<ConversationRecentDialogueTurn>();
            RecentDialogue = dialogueCopy.AsReadOnly();
            UpdatedAtUtc = Utc(updatedAtUtc);
        }

        internal static ConversationWorkingMemorySnapshot Empty(DateTime nowUtc)
        {
            return new ConversationWorkingMemorySnapshot(
                null, null, string.Empty, string.Empty, null, null, null, null,
                Array.Empty<ConversationRecentDialogueTurn>(), nowUtc);
        }

        private static string Text(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static DateTime Utc(DateTime value)
        {
            DateTime safe = value == default(DateTime) ? DateTime.UtcNow : value;
            return safe.Kind == DateTimeKind.Utc
                ? safe
                : safe.ToUniversalTime();
        }
    }

    /// <summary>
    /// Single owner of bounded normal-Conversation working memory.  The
    /// ConversationTurnLifecycle snapshot is its only transition input.
    /// </summary>
    public sealed class ConversationWorkingMemoryService
    {
        private readonly Func<DateTime> clock;
        private ConversationWorkingMemorySnapshot snapshot;

        public ConversationWorkingMemoryService(Func<DateTime> clock = null)
        {
            this.clock = clock ?? (() => DateTime.UtcNow);
            snapshot = ConversationWorkingMemorySnapshot.Empty(this.clock());
        }

        public ConversationWorkingMemorySnapshot GetSnapshot()
        {
            return snapshot;
        }

        public bool ObserveLifecycleTransition(
            ConversationTurnLifecycleSnapshot transition,
            ConversationTurnLifecycleSnapshot currentActiveTurn)
        {
            if (transition == null)
                return false;

            ConversationTurnLifecycleSnapshot lastTerminal =
                snapshot.LastTerminalTurn;
            string lastUserText = snapshot.LastUserText;
            string lastAssistantText = snapshot.LastAssistantText;
            ConversationObjectGrounding lastGrounding = snapshot.LastGrounding;
            FrozenObjectReferentSnapshot lastReferent = snapshot.LastObjectReferent;
            ReactionOutcome lastOutcome = snapshot.LastReactionOutcome;
            ConversationTurnLifecycleState? lastState = snapshot.LastTerminalState;
            var recentDialogue = new List<ConversationRecentDialogueTurn>(
                snapshot.RecentDialogue);

            if (transition.IsTerminal)
            {
                lastTerminal = transition;
                ConversationTurnContext turn = transition.TurnContext;
                lastUserText = turn != null ? turn.UserText : string.Empty;
                lastGrounding = turn != null ? turn.ObjectGrounding : null;
                lastReferent = lastGrounding != null
                    ? lastGrounding.Referent
                    : null;
                lastOutcome = transition.ReactionOutcome;
                lastAssistantText = lastOutcome != null
                    ? lastOutcome.Text
                    : string.Empty;
                lastState = transition.State;

                if (ConversationRecentDialogueTurn.TryCreateCompleted(
                        transition, out ConversationRecentDialogueTurn completed))
                {
                    recentDialogue.Add(completed);
                    while (recentDialogue.Count >
                           ConversationWorkingMemorySnapshot
                               .RecentDialogueTurnCapacity)
                    {
                        recentDialogue.RemoveAt(0);
                    }
                }
            }

            snapshot = new ConversationWorkingMemorySnapshot(
                currentActiveTurn,
                lastTerminal,
                lastUserText,
                lastAssistantText,
                lastGrounding,
                lastReferent,
                lastOutcome,
                lastState,
                recentDialogue,
                transition.UpdatedAtUtc == default(DateTime)
                    ? clock()
                    : transition.UpdatedAtUtc);
            return true;
        }
    }

    /// <summary>
    /// Scene-independent Production bridge from the canonical turn lifecycle
    /// into the single bounded working-memory owner.
    /// </summary>
    public static class ConversationWorkingMemoryRuntime
    {
        private static ConversationWorkingMemoryService service =
            new ConversationWorkingMemoryService();

        public static ConversationWorkingMemorySnapshot GetSnapshot()
        {
            return service.GetSnapshot();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAtRuntimeStart()
        {
            ResetAndConnect();
        }

        internal static void ResetForTests()
        {
            ResetAndConnect();
        }

        private static void ResetAndConnect()
        {
            ConversationTurnLifecycleRuntime.Transitioned -= OnLifecycleTransition;
            service = new ConversationWorkingMemoryService();
            ConversationTurnLifecycleRuntime.Transitioned += OnLifecycleTransition;
        }

        private static void OnLifecycleTransition(
            ConversationTurnLifecycleSnapshot transition)
        {
            if (!service.ObserveLifecycleTransition(
                    transition,
                    ConversationTurnLifecycleRuntime.CurrentActiveTurn))
            {
                return;
            }

            ConversationWorkingMemorySnapshot current = service.GetSnapshot();
            Debug.Log(
                "[CONVERSATION_WORKING_MEMORY] CurrentTurnId=" +
                TurnId(current.CurrentTurn) +
                " LastTurnId=" + TurnId(current.LastTerminalTurn) +
                " LastTerminalState=" +
                (current.LastTerminalState.HasValue
                    ? current.LastTerminalState.Value.ToString()
                    : "None") +
                " LastUserText=" + Safe(current.LastUserText) +
                " LastAssistantText=" + Safe(current.LastAssistantText) +
                " RecentDialogueCount=" + current.RecentDialogue.Count +
                " LastReferent=" +
                (current.LastObjectReferent != null
                    ? Safe(current.LastObjectReferent.ReferentId)
                    : string.Empty) +
                " Reason=" + Safe(transition.Reason));
        }

        private static string TurnId(ConversationTurnLifecycleSnapshot turn)
        {
            return turn != null ? turn.TurnId : string.Empty;
        }

        private static string Safe(string value)
        {
            string text = value == null
                ? string.Empty
                : value.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= 80 ? text : text.Substring(0, 80) + "...";
        }
    }
}
