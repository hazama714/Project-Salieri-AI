// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SalieriAI.Core.Reflex.Cognitive
{
    public static class ActivationPhraseDetector
    {
        private static readonly string[] LeadingCallPrefixes =
        {
            "ねえ",
            "ねぇ",
            "なあ",
            "なぁ",
            "おい",
            "あの",
            "もしもし"
        };

        public static ActivationPhraseResult Detect(
            string userText,
            PersonaActivationProfile profile
        )
        {
            if (string.IsNullOrWhiteSpace(userText) || profile == null)
            {
                return ActivationPhraseResult.NotActivated(userText);
            }

            string original = userText.Trim();
            List<string> aliases = BuildAliasList(profile);

            foreach (string alias in aliases)
            {
                ActivationPhraseResult result = TryMatchAlias(original, alias);
                if (result.IsActivated)
                {
                    return result;
                }
            }

            return ActivationPhraseResult.NotActivated(original);
        }

        private static List<string> BuildAliasList(PersonaActivationProfile profile)
        {
            List<string> aliases = new List<string>();

            if (profile.activationAliases != null)
            {
                for (int i = 0; i < profile.activationAliases.Length; i++)
                {
                    string alias = profile.activationAliases[i];
                    if (!string.IsNullOrWhiteSpace(alias))
                    {
                        aliases.Add(alias.Trim());
                    }
                }
            }

            return aliases
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(a => a.Length)
                .ToList();
        }

        private static ActivationPhraseResult TryMatchAlias(
            string original,
            string alias
        )
        {
            if (string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(alias))
            {
                return ActivationPhraseResult.NotActivated(original);
            }

            string trimmed = TrimLeadingSeparators(original);

            ActivationPhraseResult direct = TryMatchAliasAtStart(
                original,
                trimmed,
                alias
            );

            if (direct.IsActivated)
            {
                return direct;
            }

            for (int i = 0; i < LeadingCallPrefixes.Length; i++)
            {
                string prefix = LeadingCallPrefixes[i];

                if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string afterPrefix = trimmed.Substring(prefix.Length);
                afterPrefix = TrimLeadingSeparators(afterPrefix);

                ActivationPhraseResult prefixed = TryMatchAliasAtStart(
                    original,
                    afterPrefix,
                    alias
                );

                if (prefixed.IsActivated)
                {
                    return prefixed;
                }
            }

            return ActivationPhraseResult.NotActivated(original);
        }

        private static ActivationPhraseResult TryMatchAliasAtStart(
            string original,
            string target,
            string alias
        )
        {
            if (!target.StartsWith(alias, StringComparison.Ordinal))
            {
                return ActivationPhraseResult.NotActivated(original);
            }

            string remaining = target.Substring(alias.Length);
            remaining = TrimLeadingSeparators(remaining);

            return new ActivationPhraseResult(
                true,
                alias,
                remaining,
                original
            );
        }

        private static string TrimLeadingSeparators(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

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
                ':',
                '「',
                '」',
                '『',
                '』'
            ).Trim();
        }
    }
}
