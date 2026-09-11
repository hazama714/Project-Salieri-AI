// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Language.Compiler;

namespace SalieriAI.Core.Reflex.Cognitive.Grounding
{
    /// <summary>
    /// Pure, deliberately narrow v0 resolver for an explicit Japanese
    /// proximal-object reference at the beginning of an admitted utterance.
    /// It does not inspect perception, memory, or detector labels.
    /// </summary>
    public sealed class ConversationTargetReferenceResolverV0
    {
        public SemanticTargetRef Resolve(string inputText)
        {
            string text = Normalize(inputText);
            if (text.Length == 0)
                return SemanticTargetRef.Unspecified;

            if (IsExplicitKore(text) || IsExplicitKono(text))
                return SemanticTargetRef.This;

            return SemanticTargetRef.Unspecified;
        }

        private static bool IsExplicitKore(string text)
        {
            const string token = "これ";
            if (!text.StartsWith(token, StringComparison.Ordinal))
                return false;
            if (text.Length == token.Length)
                return true;

            char next = text[token.Length];
            return next == 'は' ||
                   next == 'を' ||
                   next == 'が' ||
                   next == 'に' ||
                   next == 'の' ||
                   next == 'で' ||
                   next == 'と' ||
                   next == 'っ' ||
                   next == '、' ||
                   next == '。' ||
                   next == '？' ||
                   next == '?' ||
                   next == '！' ||
                   next == '!' ||
                   char.IsWhiteSpace(next);
        }

        private static bool IsExplicitKono(string text)
        {
            const string token = "この";
            if (!text.StartsWith(token, StringComparison.Ordinal))
                return false;

            // The standalone determiner remains an explicit proximal form.
            // A following non-whitespace token is its bounded v0 noun phrase.
            return text.Length == token.Length ||
                   !char.IsWhiteSpace(text[token.Length]);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
