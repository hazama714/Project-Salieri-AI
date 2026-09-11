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

using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Language.Contracts;
using SalieriAI.Core.Reflex.Cognitive.Grounding;

using UnityEngine;

namespace SalieriAI.Core.Reflex.Cognitive
{
    public enum ConversationTurnLifecycleState
    {
        Created = 0,
        GenerationStarted = 1,
        OutputRequested = 2,
        SpeechAttached = 3,
        SpeechStarted = 4,
        Completed = 5,
        Failed = 6,
        Interrupted = 7
    }

    /// <summary>
    /// Immutable read-only state for one normal Conversation turn.
    /// ConversationTurnContext remains the immutable generation snapshot;
    /// this snapshot only adds runtime output/speech lifecycle correlation.
    /// </summary>
    public sealed class ConversationTurnLifecycleSnapshot
    {
        public ConversationTurnContext TurnContext { get; }
        public string TurnId { get; }
        public string InputId { get; }
        public string InteractionId { get; }
        public int RequestSerial { get; }
        public string OutputId { get; }
        public string RuntimeSpeechId { get; }
        public ReactionOutcome ReactionOutcome { get; }
        public ConversationTurnLifecycleState State { get; }
        public DateTime UpdatedAtUtc { get; }
        public string Reason { get; }

        public bool IsTerminal =>
            State == ConversationTurnLifecycleState.Completed ||
            State == ConversationTurnLifecycleState.Failed ||
            State == ConversationTurnLifecycleState.Interrupted;

        internal ConversationTurnLifecycleSnapshot(
            ConversationTurnContext turnContext,
            string outputId,
            string runtimeSpeechId,
            ReactionOutcome reactionOutcome,
            ConversationTurnLifecycleState state,
            DateTime updatedAtUtc,
            string reason)
        {
            TurnContext = turnContext;
            TurnId = turnContext != null ? Text(turnContext.TurnId) : string.Empty;
            InputId = turnContext != null && turnContext.Input != null
                ? Text(turnContext.Input.InputId)
                : string.Empty;
            InteractionId = turnContext != null && turnContext.Input != null
                ? Text(turnContext.Input.InteractionId)
                : string.Empty;
            RequestSerial = turnContext != null
                ? turnContext.RequestSerial
                : 0;
            OutputId = Text(outputId);
            RuntimeSpeechId = Text(runtimeSpeechId);
            ReactionOutcome = reactionOutcome;
            State = state;
            UpdatedAtUtc = Utc(updatedAtUtc);
            Reason = reason ?? string.Empty;
        }

        private static string Text(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
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

    /// <summary>
    /// Thin lifecycle owner for normal Conversation turns only.
    /// It does not own activation, grounding, generation, TTS, InteractionState,
    /// OneShot Question lifecycle, or working memory.
    /// </summary>
    public sealed class ConversationTurnLifecycleService
    {
        private readonly Func<DateTime> clock;
        private readonly Dictionary<string, ConversationTurnLifecycleSnapshot>
            activeByTurn =
                new Dictionary<string, ConversationTurnLifecycleSnapshot>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, string> turnBySpeech =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public ConversationTurnLifecycleSnapshot CurrentSnapshot { get; private set; }
        public ConversationTurnLifecycleSnapshot CurrentActiveTurn =>
            CurrentSnapshot != null && !CurrentSnapshot.IsTerminal
                ? CurrentSnapshot
                : null;
        public ConversationTurnLifecycleSnapshot LastTerminalTurn { get; private set; }

        public event Action<ConversationTurnLifecycleSnapshot> Transitioned;

        public ConversationTurnLifecycleService(Func<DateTime> clock = null)
        {
            this.clock = clock ?? (() => DateTime.UtcNow);
        }

        public bool TryBeginTurn(
            ConversationTurnContext turn,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (turn == null || turn.Input == null ||
                string.IsNullOrWhiteSpace(turn.TurnId) ||
                string.IsNullOrWhiteSpace(turn.Input.InputId) ||
                string.IsNullOrWhiteSpace(turn.Input.InteractionId))
            {
                error = "Conversation turn identity is incomplete.";
                return false;
            }

            string turnId = turn.TurnId.Trim();
            if (!string.Equals(
                    turnId,
                    turn.Input.InputId,
                    StringComparison.Ordinal))
            {
                error = "TurnId must equal CommunicationInput.InputId.";
                return false;
            }

            if (activeByTurn.ContainsKey(turnId))
            {
                error = "Conversation turn already exists.";
                return false;
            }

            // A newer Production input supersedes only turns which have not
            // produced output yet.  This mirrors ConversationService's
            // requestSerial semantics without cancelling native generation,
            // and prevents a stale pre-output turn from remaining active until
            // its asynchronous result eventually returns.  Output/speech turns
            // remain independently correlated and are not closed here.
            var supersededTurnIds = new List<string>();
            foreach (KeyValuePair<string, ConversationTurnLifecycleSnapshot> pair
                     in activeByTurn)
            {
                if (pair.Value.State == ConversationTurnLifecycleState.Created ||
                    pair.Value.State ==
                        ConversationTurnLifecycleState.GenerationStarted)
                {
                    supersededTurnIds.Add(pair.Key);
                }
            }

            foreach (string supersededTurnId in supersededTurnIds)
            {
                if (activeByTurn.TryGetValue(
                        supersededTurnId, out var supersededSnapshot))
                {
                    TryTerminal(
                        supersededSnapshot,
                        ConversationTurnLifecycleState.Interrupted,
                        "superseded-by-new-conversation-turn",
                        out _,
                        out _,
                        requireStarted: false,
                        allowBeforeSpeech: true);
                }
            }

            snapshot = new ConversationTurnLifecycleSnapshot(
                turn,
                string.Empty,
                string.Empty,
                null,
                ConversationTurnLifecycleState.Created,
                clock(),
                "turn-created");
            activeByTurn.Add(turnId, snapshot);
            CurrentSnapshot = snapshot;
            Notify(snapshot);
            return true;
        }

        public bool TryMarkGenerationStarted(
            string turnId,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            return TryTransition(
                turnId,
                ConversationTurnLifecycleState.Created,
                ConversationTurnLifecycleState.GenerationStarted,
                null,
                null,
                "generation-started",
                out snapshot,
                out error);
        }

        public bool TryMarkOutputRequested(
            string turnId,
            string outputId,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            return TryMarkOutputRequestedCore(
                turnId,
                outputId,
                null,
                out snapshot,
                out error);
        }

        public bool TryMarkOutputRequested(
            string turnId,
            ReactionOutcome outcome,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            if (outcome == null)
            {
                snapshot = null;
                error = "ReactionOutcome is required.";
                return false;
            }
            if (!string.Equals(
                    turnId == null ? string.Empty : turnId.Trim(),
                    outcome.InputId == null ? string.Empty : outcome.InputId.Trim(),
                    StringComparison.Ordinal))
            {
                snapshot = null;
                error = "ReactionOutcome InputId does not match TurnId.";
                return false;
            }

            return TryMarkOutputRequestedCore(
                turnId,
                outcome.OutputId,
                outcome,
                out snapshot,
                out error);
        }

        private bool TryMarkOutputRequestedCore(
            string turnId,
            string outputId,
            ReactionOutcome outcome,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(outputId))
            {
                snapshot = null;
                error = "OutputId is required.";
                return false;
            }

            if (!TryGetActive(
                    turnId,
                    out ConversationTurnLifecycleSnapshot current,
                    out error))
            {
                snapshot = null;
                return false;
            }
            if (current.State != ConversationTurnLifecycleState.GenerationStarted)
            {
                snapshot = null;
                error = "Invalid Conversation turn transition: " +
                    current.State + " -> " +
                    ConversationTurnLifecycleState.OutputRequested + ".";
                return false;
            }
            if (outcome != null &&
                !string.Equals(
                    current.InteractionId,
                    outcome.InteractionId == null
                        ? string.Empty
                        : outcome.InteractionId.Trim(),
                    StringComparison.Ordinal))
            {
                snapshot = null;
                error = "ReactionOutcome InteractionId does not match the turn.";
                return false;
            }

            snapshot = Replace(
                current,
                ConversationTurnLifecycleState.OutputRequested,
                outputId.Trim(),
                null,
                "speech-output-requested",
                outcome);
            activeByTurn[current.TurnId] = snapshot;
            if (CurrentSnapshot != null && CurrentSnapshot.TurnId == current.TurnId)
                CurrentSnapshot = snapshot;
            Notify(snapshot);
            return true;
        }

        public bool TryAttachSpeech(
            string turnId,
            string outputId,
            string interactionId,
            string runtimeSpeechId,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (!TryGetActive(turnId, out ConversationTurnLifecycleSnapshot current, out error))
                return false;
            if (current.State != ConversationTurnLifecycleState.OutputRequested)
            {
                error = "Speech can only attach after OutputRequested.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(outputId) ||
                !string.Equals(current.OutputId, outputId.Trim(), StringComparison.Ordinal))
            {
                error = "OutputId does not match the active turn.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(interactionId) ||
                !string.Equals(
                    current.InteractionId,
                    interactionId.Trim(),
                    StringComparison.Ordinal))
            {
                error = "InteractionId does not match the active turn.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(runtimeSpeechId))
            {
                error = "RuntimeSpeechId is required.";
                return false;
            }

            string speechId = runtimeSpeechId.Trim();
            if (turnBySpeech.ContainsKey(speechId))
            {
                error = "RuntimeSpeechId is already attached.";
                return false;
            }

            snapshot = Replace(
                current,
                ConversationTurnLifecycleState.SpeechAttached,
                current.OutputId,
                speechId,
                "speech-attached");
            activeByTurn[current.TurnId] = snapshot;
            turnBySpeech.Add(speechId, current.TurnId);
            if (CurrentSnapshot != null && CurrentSnapshot.TurnId == current.TurnId)
                CurrentSnapshot = snapshot;
            Notify(snapshot);
            return true;
        }

        public bool TryObserveSpeechLifecycle(
            global::SpeechPlaybackRuntimeFact fact,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (fact == null || string.IsNullOrWhiteSpace(fact.RuntimeSpeechId))
            {
                error = "Speech lifecycle fact is invalid.";
                return false;
            }
            if (!turnBySpeech.TryGetValue(fact.RuntimeSpeechId, out string turnId))
            {
                error = "Speech lifecycle is not correlated to a Conversation turn.";
                return false;
            }
            if (!TryGetActive(turnId, out ConversationTurnLifecycleSnapshot current, out error))
                return false;

            switch (fact.Lifecycle)
            {
                case SpeechAdapterLifecycle.RequestAccepted:
                    snapshot = current;
                    return true;

                case SpeechAdapterLifecycle.PlaybackStarted:
                    return TryTransition(
                        turnId,
                        ConversationTurnLifecycleState.SpeechAttached,
                        ConversationTurnLifecycleState.SpeechStarted,
                        current.OutputId,
                        current.RuntimeSpeechId,
                        "speech-started",
                        out snapshot,
                        out error);

                case SpeechAdapterLifecycle.PlaybackCompleted:
                    return TryTerminal(
                        current,
                        ConversationTurnLifecycleState.Completed,
                        "speech-completed",
                        out snapshot,
                        out error,
                        requireStarted: true);

                case SpeechAdapterLifecycle.PlaybackFailed:
                    return TryTerminal(
                        current,
                        ConversationTurnLifecycleState.Failed,
                        "speech-failed:" + (fact.FailureReason ?? string.Empty),
                        out snapshot,
                        out error,
                        requireStarted: false);

                case SpeechAdapterLifecycle.PlaybackInterrupted:
                    return TryTerminal(
                        current,
                        ConversationTurnLifecycleState.Interrupted,
                        "speech-interrupted:" + (fact.FailureReason ?? string.Empty),
                        out snapshot,
                        out error,
                        requireStarted: false);

                default:
                    error = "Unsupported speech lifecycle.";
                    return false;
            }
        }

        public bool TryFailTurn(
            string turnId,
            string reason,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (!TryGetActive(turnId, out ConversationTurnLifecycleSnapshot current, out error))
                return false;
            return TryTerminal(
                current,
                ConversationTurnLifecycleState.Failed,
                reason,
                out snapshot,
                out error,
                requireStarted: false,
                allowBeforeSpeech: true);
        }

        public bool TryInterruptTurn(
            string turnId,
            string reason,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (!TryGetActive(turnId, out ConversationTurnLifecycleSnapshot current, out error))
                return false;
            return TryTerminal(
                current,
                ConversationTurnLifecycleState.Interrupted,
                reason,
                out snapshot,
                out error,
                requireStarted: false,
                allowBeforeSpeech: true);
        }

        public bool TryGetSnapshot(
            string turnId,
            out ConversationTurnLifecycleSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(turnId))
                return false;
            if (activeByTurn.TryGetValue(turnId.Trim(), out snapshot))
                return true;
            if (CurrentSnapshot != null && CurrentSnapshot.TurnId == turnId.Trim())
            {
                snapshot = CurrentSnapshot;
                return true;
            }
            if (LastTerminalTurn != null && LastTerminalTurn.TurnId == turnId.Trim())
            {
                snapshot = LastTerminalTurn;
                return true;
            }
            return false;
        }

        private bool TryTransition(
            string turnId,
            ConversationTurnLifecycleState required,
            ConversationTurnLifecycleState next,
            string outputId,
            string runtimeSpeechId,
            string reason,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (!TryGetActive(turnId, out ConversationTurnLifecycleSnapshot current, out error))
                return false;
            if (current.State != required)
            {
                error = "Invalid Conversation turn transition: " +
                    current.State + " -> " + next + ".";
                return false;
            }

            snapshot = Replace(current, next, outputId, runtimeSpeechId, reason);
            activeByTurn[current.TurnId] = snapshot;
            if (CurrentSnapshot != null && CurrentSnapshot.TurnId == current.TurnId)
                CurrentSnapshot = snapshot;
            Notify(snapshot);
            return true;
        }

        private bool TryTerminal(
            ConversationTurnLifecycleSnapshot current,
            ConversationTurnLifecycleState terminal,
            string reason,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error,
            bool requireStarted,
            bool allowBeforeSpeech = false)
        {
            snapshot = null;
            error = string.Empty;
            if (current == null || current.IsTerminal)
            {
                error = "Conversation turn is already terminal or missing.";
                return false;
            }
            bool valid = allowBeforeSpeech ||
                current.State == ConversationTurnLifecycleState.SpeechAttached ||
                current.State == ConversationTurnLifecycleState.SpeechStarted;
            if (requireStarted)
                valid = current.State == ConversationTurnLifecycleState.SpeechStarted;
            if (!valid)
            {
                error = "Conversation turn cannot enter " + terminal +
                    " from " + current.State + ".";
                return false;
            }

            snapshot = Replace(
                current,
                terminal,
                current.OutputId,
                current.RuntimeSpeechId,
                reason);
            activeByTurn.Remove(current.TurnId);
            if (!string.IsNullOrEmpty(current.RuntimeSpeechId))
                turnBySpeech.Remove(current.RuntimeSpeechId);
            LastTerminalTurn = snapshot;
            if (CurrentSnapshot != null && CurrentSnapshot.TurnId == current.TurnId)
                CurrentSnapshot = snapshot;
            Notify(snapshot);
            return true;
        }

        private bool TryGetActive(
            string turnId,
            out ConversationTurnLifecycleSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(turnId) ||
                !activeByTurn.TryGetValue(turnId.Trim(), out snapshot))
            {
                error = "Active Conversation turn was not found.";
                return false;
            }
            return true;
        }

        private ConversationTurnLifecycleSnapshot Replace(
            ConversationTurnLifecycleSnapshot current,
            ConversationTurnLifecycleState state,
            string outputId,
            string runtimeSpeechId,
            string reason,
            ReactionOutcome reactionOutcome = null)
        {
            return new ConversationTurnLifecycleSnapshot(
                current.TurnContext,
                outputId ?? current.OutputId,
                runtimeSpeechId ?? current.RuntimeSpeechId,
                reactionOutcome ?? current.ReactionOutcome,
                state,
                clock(),
                reason);
        }

        private void Notify(ConversationTurnLifecycleSnapshot snapshot)
        {
            Transitioned?.Invoke(snapshot);
        }
    }

    /// <summary>
    /// Scene-independent Production owner for the thin turn lifecycle service.
    /// Static state is reset at Unity subsystem registration.
    /// </summary>
    public static class ConversationTurnLifecycleRuntime
    {
        private static ConversationTurnLifecycleService service = CreateService();

        public static ConversationTurnLifecycleSnapshot CurrentSnapshot =>
            service.CurrentSnapshot;
        public static ConversationTurnLifecycleSnapshot CurrentActiveTurn =>
            service.CurrentActiveTurn;
        public static ConversationTurnLifecycleSnapshot LastTerminalTurn =>
            service.LastTerminalTurn;

        public static event Action<ConversationTurnLifecycleSnapshot> Transitioned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAtRuntimeStart()
        {
            service = CreateService();
        }

        public static bool BeginTurn(ConversationTurnContext turn)
        {
            return Report(
                service.TryBeginTurn(turn, out _, out string error),
                error,
                "BeginTurn");
        }

        public static bool MarkGenerationStarted(string turnId)
        {
            return Report(
                service.TryMarkGenerationStarted(turnId, out _, out string error),
                error,
                "GenerationStarted");
        }

        public static bool MarkOutputRequested(string turnId, string outputId)
        {
            return Report(
                service.TryMarkOutputRequested(
                    turnId, outputId, out _, out string error),
                error,
                "OutputRequested");
        }

        public static bool MarkOutputRequested(
            string turnId,
            ReactionOutcome outcome)
        {
            return Report(
                service.TryMarkOutputRequested(
                    turnId, outcome, out _, out string error),
                error,
                "OutputRequested");
        }

        public static bool AttachSpeech(
            string turnId,
            string outputId,
            string interactionId,
            string runtimeSpeechId)
        {
            return Report(
                service.TryAttachSpeech(
                    turnId, outputId, interactionId, runtimeSpeechId,
                    out _, out string error),
                error,
                "SpeechAttached");
        }

        public static bool ObserveSpeechLifecycle(
            global::SpeechPlaybackRuntimeFact fact)
        {
            bool result = service.TryObserveSpeechLifecycle(
                fact, out _, out string error);
            if (!result && error !=
                "Speech lifecycle is not correlated to a Conversation turn.")
            {
                Report(false, error, "SpeechLifecycle");
            }
            return result;
        }

        public static bool FailTurn(string turnId, string reason)
        {
            return Report(
                service.TryFailTurn(turnId, reason, out _, out string error),
                error,
                "Failed");
        }

        public static bool InterruptTurn(string turnId, string reason)
        {
            // ConversationService may observe the stale async result after a
            // newer turn already superseded this pre-output turn.  Treat that
            // repeated stale notification as idempotent and avoid a false
            // lifecycle warning.
            if (service.TryGetSnapshot(turnId, out var existing) &&
                existing.IsTerminal)
            {
                return true;
            }

            return Report(
                service.TryInterruptTurn(
                    turnId, reason, out _, out string error),
                error,
                "Interrupted");
        }

        internal static void ResetForTests()
        {
            service = CreateService();
        }

        private static ConversationTurnLifecycleService CreateService()
        {
            var result = new ConversationTurnLifecycleService();
            result.Transitioned += LogTransition;
            return result;
        }

        private static void LogTransition(
            ConversationTurnLifecycleSnapshot snapshot)
        {
            if (snapshot == null)
                return;
            Debug.Log(
                "[CONVERSATION_TURN] TurnId=" + snapshot.TurnId +
                " InputId=" + snapshot.InputId +
                " InteractionId=" + snapshot.InteractionId +
                " OutputId=" + snapshot.OutputId +
                " RuntimeSpeechId=" + snapshot.RuntimeSpeechId +
                " State=" + snapshot.State +
                " Reason=" + snapshot.Reason);
            Transitioned?.Invoke(snapshot);
        }

        private static bool Report(bool result, string error, string boundary)
        {
            if (!result)
            {
                Debug.LogWarning(
                    "[CONVERSATION_TURN] Boundary=" + boundary +
                    " Rejected=" + (error ?? string.Empty));
            }
            return result;
        }
    }
}
