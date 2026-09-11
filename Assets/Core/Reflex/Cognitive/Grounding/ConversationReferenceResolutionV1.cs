// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Core.Language.Compiler;
using SalieriAI.Core.Semantics.Referents;

namespace SalieriAI.Core.Reflex.Cognitive.Grounding
{
    public enum ConversationReferentSource
    {
        None = 0,
        CurrentObjectReferent = 1,
        PreviousTurnObjectReferent = 2
    }

    public enum ConversationReferenceResolutionStatus
    {
        ResolvedCurrent = 0,
        ResolvedPrevious = 1,
        Unspecified = 2,
        MissingCurrent = 3,
        MissingPrevious = 4,
        Unsupported = 5,
        Ambiguous = 6,
        NeedsClarification = 7
    }

    /// <summary>
    /// Immutable result of deterministic linguistic-reference resolution.
    /// The selected referent is frozen here; downstream code never re-reads
    /// the live current-referent provider for this turn.
    /// </summary>
    public sealed class ConversationReferenceResolution
    {
        public SemanticTargetRef SemanticTargetRef { get; }
        public ConversationReferenceResolutionStatus Status { get; }
        public ConversationReferentSource ReferentSource { get; }
        public FrozenObjectReferentSnapshot FrozenReferent { get; }
        public string MatchedExpression { get; }
        public string Diagnostic { get; }

        public bool IsResolved =>
            Status == ConversationReferenceResolutionStatus.ResolvedCurrent ||
            Status == ConversationReferenceResolutionStatus.ResolvedPrevious;

        internal ConversationReferenceResolution(
            SemanticTargetRef semanticTargetRef,
            ConversationReferenceResolutionStatus status,
            ConversationReferentSource referentSource,
            FrozenObjectReferentSnapshot frozenReferent,
            string matchedExpression,
            string diagnostic)
        {
            SemanticTargetRef = semanticTargetRef;
            Status = status;
            ReferentSource = referentSource;
            FrozenReferent = frozenReferent;
            MatchedExpression = Text(matchedExpression);
            Diagnostic = diagnostic ?? string.Empty;
        }

        private static string Text(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }

    /// <summary>
    /// Pure v1 resolver. Current and previous referents are explicit inputs;
    /// this class does not access Scene state, services, Recall, or storage.
    /// </summary>
    public sealed class ConversationReferenceResolverV1
    {
        public ConversationReferenceResolution Resolve(
            string inputText,
            CurrentObjectReferent currentReferent,
            FrozenObjectReferentSnapshot previousTurnReferent)
        {
            string text = Normalize(inputText);
            if (text.Length == 0)
                return Unspecified("Input is empty.");

            if (StartsWithAny(text, "今見てるもの", "今見ているもの"))
            {
                return ResolveCurrent(
                    SemanticTargetRef.CurrentObservationTarget,
                    currentReferent,
                    Matched(text, "今見てるもの", "今見ているもの"));
            }

            if (StartsWithAny(text, "今の"))
            {
                return new ConversationReferenceResolution(
                    SemanticTargetRef.Unresolved,
                    ConversationReferenceResolutionStatus.Ambiguous,
                    ConversationReferentSource.None,
                    null,
                    "今の",
                    "The expression may refer to the current object, the " +
                    "previous conversational referent, or the previous event.");
            }

            if (StartsWithAny(text, "あれ", "あの"))
            {
                return new ConversationReferenceResolution(
                    SemanticTargetRef.Unresolved,
                    ConversationReferenceResolutionStatus.Unsupported,
                    ConversationReferentSource.None,
                    null,
                    Matched(text, "あれ", "あの"),
                    "Distal deixis has no spatial grounding evidence in v1.");
            }

            if (StartsWithAny(
                    text,
                    "さっき見てたやつ",
                    "さっき見ていたやつ",
                    "さっきの",
                    "前の"))
            {
                return ResolvePrevious(
                    previousTurnReferent,
                    Matched(
                        text,
                        "さっき見てたやつ",
                        "さっき見ていたやつ",
                        "さっきの",
                        "前の"));
            }

            if (IsExplicitSore(text) || IsExplicitSono(text))
            {
                return ResolvePrevious(
                    previousTurnReferent,
                    IsExplicitSore(text) ? "それ" : "その");
            }

            if (IsExplicitKore(text) || IsExplicitKono(text))
            {
                return ResolveCurrent(
                    SemanticTargetRef.This,
                    currentReferent,
                    IsExplicitKore(text) ? "これ" : "この");
            }

            return Unspecified("No v1 reference expression matched.");
        }

        private static ConversationReferenceResolution ResolveCurrent(
            SemanticTargetRef targetRef,
            CurrentObjectReferent currentReferent,
            string expression)
        {
            if (currentReferent == null)
            {
                return new ConversationReferenceResolution(
                    targetRef,
                    ConversationReferenceResolutionStatus.MissingCurrent,
                    ConversationReferentSource.CurrentObjectReferent,
                    null,
                    expression,
                    "The expression requires a current object referent.");
            }

            return new ConversationReferenceResolution(
                targetRef,
                ConversationReferenceResolutionStatus.ResolvedCurrent,
                ConversationReferentSource.CurrentObjectReferent,
                new FrozenObjectReferentSnapshot(currentReferent),
                expression,
                "Resolved from the current object referent supplied at turn start.");
        }

        private static ConversationReferenceResolution ResolvePrevious(
            FrozenObjectReferentSnapshot previousTurnReferent,
            string expression)
        {
            if (previousTurnReferent == null)
            {
                return new ConversationReferenceResolution(
                    SemanticTargetRef.That,
                    ConversationReferenceResolutionStatus.MissingPrevious,
                    ConversationReferentSource.PreviousTurnObjectReferent,
                    null,
                    expression,
                    "The expression requires a previous-turn object referent.");
            }

            return new ConversationReferenceResolution(
                SemanticTargetRef.That,
                ConversationReferenceResolutionStatus.ResolvedPrevious,
                ConversationReferentSource.PreviousTurnObjectReferent,
                previousTurnReferent,
                expression,
                "Resolved from ConversationWorkingMemory.LastObjectReferent.");
        }

        private static ConversationReferenceResolution Unspecified(
            string diagnostic)
        {
            return new ConversationReferenceResolution(
                SemanticTargetRef.Unspecified,
                ConversationReferenceResolutionStatus.Unspecified,
                ConversationReferentSource.None,
                null,
                string.Empty,
                diagnostic);
        }

        private static bool IsExplicitKore(string text)
        {
            return HasBoundedNominalPrefix(text, "これ") ||
                   text.StartsWith("これ何", System.StringComparison.Ordinal);
        }

        private static bool IsExplicitKono(string text)
        {
            return HasDeterminerPrefix(text, "この");
        }

        private static bool IsExplicitSore(string text)
        {
            return HasBoundedNominalPrefix(text, "それ") ||
                   text.StartsWith("それ何", System.StringComparison.Ordinal);
        }

        private static bool IsExplicitSono(string text)
        {
            return HasDeterminerPrefix(text, "その");
        }

        private static bool HasBoundedNominalPrefix(string text, string token)
        {
            if (!text.StartsWith(token, System.StringComparison.Ordinal))
                return false;
            if (text.Length == token.Length)
                return true;

            char next = text[token.Length];
            return next == 'は' || next == 'を' || next == 'が' ||
                   next == 'に' || next == 'の' || next == 'で' ||
                   next == 'と' || next == 'っ' || next == '、' ||
                   next == '。' || next == '？' || next == '?' ||
                   next == '！' || next == '!' ||
                   char.IsWhiteSpace(next);
        }

        private static bool HasDeterminerPrefix(string text, string token)
        {
            return text.StartsWith(token, System.StringComparison.Ordinal) &&
                   (text.Length == token.Length ||
                    !char.IsWhiteSpace(text[token.Length]));
        }

        private static bool StartsWithAny(string text, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (text.StartsWith(values[i], System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string Matched(string text, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (text.StartsWith(values[i], System.StringComparison.Ordinal))
                    return values[i];
            }
            return string.Empty;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
