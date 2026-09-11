// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Language.Contracts;

namespace SalieriAI.Core.Language.Cgl
{
    /// <summary>
    /// Phase 2A deterministic shadow parser.
    /// It only describes an input and never selects or executes a production route.
    /// </summary>
    public static class DeterministicCglParser
    {
        public const string ParserVersion = "cgl-shadow-deterministic-2a.1";
        public const string CglVersion = "cgl-shadow-2a";

        public static CglInterpretation Parse(CommunicationInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            string normalizedText = input.NormalizedText ?? string.Empty;
            bool activationMatched;
            string requestText = RemoveActivationPrefix(
                normalizedText,
                out activationMatched
            );

            ParseResult result = Classify(requestText, activationMatched);
            string semanticForm = BuildSemanticForm(
                result.RequestType,
                result.ActionCandidate,
                result.DirectionCandidate
            );

            return new CglInterpretation(
                CommunicationIdGenerator.Create("analysis"),
                input.InputId,
                input.InteractionId,
                normalizedText,
                semanticForm,
                activationMatched,
                result.RequestType,
                result.ActionCandidate,
                result.DirectionCandidate,
                result.InterpretationStatus,
                ParserVersion,
                result.RequestType == CglRequestTypes.Unknown,
                false,
                result.RequestType == CglRequestTypes.Unknown,
                CglVersion,
                DateTime.UtcNow
            );
        }

        private static ParseResult Classify(
            string requestText,
            bool activationMatched)
        {
            string comparable = NormalizeForComparison(requestText);

            if (string.IsNullOrEmpty(comparable))
            {
                return activationMatched
                    ? ParseResult.Resolved(CglRequestTypes.Activation)
                    : ParseResult.Unresolved();
            }

            if (ContainsAny(
                comparable,
                "緊急停止",
                "非常停止",
                "絶対停止",
                "エマージェンシーストップ"))
            {
                return ParseResult.Resolved(CglRequestTypes.Emergency);
            }

            if (ContainsAny(
                comparable,
                "止まって",
                "止まれ",
                "止めて",
                "停止",
                "ストップ",
                "やめて"))
            {
                return ParseResult.Resolved(CglRequestTypes.Stop);
            }

            if (MatchesAny(comparable, "右を向いて", "右向いて"))
            {
                return ParseResult.Action("LOOK_DIRECTION", "RIGHT");
            }

            if (MatchesAny(comparable, "左を向いて", "左向いて"))
            {
                return ParseResult.Action("LOOK_DIRECTION", "LEFT");
            }

            if (MatchesAny(comparable, "前を向いて", "正面を向いて"))
            {
                return ParseResult.Action("LOOK_DIRECTION", "FORWARD");
            }

            if (MatchesAny(
                comparable,
                "前に進んで",
                "前へ進んで",
                "前進して"))
            {
                return ParseResult.Action("MOVE", "FORWARD");
            }

            if (MatchesAny(
                comparable,
                "後ろに進んで",
                "後ろへ進んで",
                "後退して"))
            {
                return ParseResult.Action("MOVE", "BACKWARD");
            }

            if (ContainsAny(
                comparable,
                "こんにちは",
                "おはよう",
                "こんばんは",
                "ありがとう"))
            {
                return ParseResult.Resolved(CglRequestTypes.Conversation);
            }

            return ParseResult.Unresolved();
        }

        private static string RemoveActivationPrefix(
            string text,
            out bool activationMatched)
        {
            activationMatched = false;

            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string trimmed = text.Trim();
            const string activation = "アシス";

            if (!trimmed.StartsWith(activation, StringComparison.Ordinal))
                return trimmed;

            activationMatched = true;
            return TrimLeadingSeparators(trimmed.Substring(activation.Length));
        }

        private static string NormalizeForComparison(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("　", string.Empty)
                .Replace("、", string.Empty)
                .Replace("。", string.Empty)
                .Replace(",", string.Empty)
                .Replace(".", string.Empty)
                .Replace("！", string.Empty)
                .Replace("!", string.Empty)
                .Replace("？", string.Empty)
                .Replace("?", string.Empty);
        }

        private static string TrimLeadingSeparators(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text.Trim().TrimStart(
                '、',
                '。',
                ',',
                '.',
                '，',
                '．',
                ' ',
                '\t',
                '\r',
                '\n',
                '！',
                '!',
                '？',
                '?',
                '：',
                ':'
            ).Trim();
        }

        private static bool MatchesAny(string text, params string[] candidates)
        {
            if (string.IsNullOrEmpty(text) || candidates == null)
                return false;

            for (int i = 0; i < candidates.Length; i++)
            {
                if (text == NormalizeForComparison(candidates[i]))
                    return true;
            }

            return false;
        }

        private static bool ContainsAny(string text, params string[] candidates)
        {
            if (string.IsNullOrEmpty(text) || candidates == null)
                return false;

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = NormalizeForComparison(candidates[i]);
                if (!string.IsNullOrEmpty(candidate) &&
                    text.Contains(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildSemanticForm(
            string requestType,
            string actionCandidate,
            string directionCandidate)
        {
            return
                "requestType=" + (requestType ?? string.Empty) +
                ";actionCandidate=" + (actionCandidate ?? string.Empty) +
                ";directionCandidate=" + (directionCandidate ?? string.Empty);
        }

        private struct ParseResult
        {
            public string RequestType;
            public string ActionCandidate;
            public string DirectionCandidate;
            public string InterpretationStatus;

            public static ParseResult Resolved(string requestType)
            {
                return new ParseResult
                {
                    RequestType = requestType,
                    ActionCandidate = string.Empty,
                    DirectionCandidate = string.Empty,
                    InterpretationStatus = CglInterpretationStatuses.Resolved
                };
            }

            public static ParseResult Action(
                string actionCandidate,
                string directionCandidate)
            {
                return new ParseResult
                {
                    RequestType = CglRequestTypes.Action,
                    ActionCandidate = actionCandidate,
                    DirectionCandidate = directionCandidate,
                    InterpretationStatus = CglInterpretationStatuses.Resolved
                };
            }

            public static ParseResult Unresolved()
            {
                return new ParseResult
                {
                    RequestType = CglRequestTypes.Unknown,
                    ActionCandidate = string.Empty,
                    DirectionCandidate = string.Empty,
                    InterpretationStatus = CglInterpretationStatuses.Unresolved
                };
            }
        }
    }

    public static class CglRequestTypes
    {
        public const string Activation = "ACTIVATION";
        public const string Conversation = "CONVERSATION";
        public const string Action = "ACTION";
        public const string Clarification = "CLARIFICATION";
        public const string Stop = "STOP";
        public const string Emergency = "EMERGENCY";
        public const string Unknown = "UNKNOWN";
    }

    public static class CglInterpretationStatuses
    {
        public const string Resolved = "RESOLVED";
        public const string Unresolved = "UNRESOLVED";
    }
}
