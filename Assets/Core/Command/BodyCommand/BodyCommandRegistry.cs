// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Command.BodyCommand
{
    /// <summary>
    /// 発話・テキスト・UI・将来のLLM/Habit層から参照するBodyCommand共通入口。
    ///
    /// 現段階では固定BodyAction用のC#辞書として動作する。
    /// 実行そのものは担当せず、入力テキストを BodyActionId に解決するだけにする。
    /// </summary>
    public static class BodyCommandRegistry
    {
        private static readonly BodyCommandRule[] Rules =
        {
            // Safety first:
            // 「止まって」「止まれ」は広義の安全停止として扱う。
            // 現状のBodyActionExecutor側では emergency_stop も crawler_stop fallback で
            // CRAWLER:STOPへ届く想定。
            new BodyCommandRule(
                "emergency_stop",
                "fixed-rule:safety-stop-command",
                100,
                true,
                true,
                new[]
                {
                    "緊急停止",
                    "非常停止",
                    "きんきゅうていし",
                    "止まって",
                    "止まれ",
                    "止めて",
                    "とまって",
                    "とまれ",
                    "とめて"
                }
            ),

            new BodyCommandRule(
                "crawler_stop",
                "fixed-rule:crawler-stop-command",
                95,
                true,
                true,
                new[]
                {
                    "クローラーストップ",
                    "くろーらーすとっぷ",
                    "クローラー停止",
                    "くろーらーていし"
                }
            ),

            new BodyCommandRule(
                "crawler_stop",
                "fixed-rule:stop-command",
                90,
                true,
                true,
                new[]
                {
                    "ストップ",
                    "すとっぷ"
                }
            ),

            // 「こっち」系は Phase 10-J-2 では安全側で首Actionのみ。
            // まだ approach_user_step / キャタピラ前進には接続しない。
            // 「こっち来て」は将来の approach_user_step 用に温存し、ここでは拾わない。
            new BodyCommandRule(
                "lookAround",
                "fixed-rule:look-here-neck-only-command",
                45,
                false,
                false,
                new[]
                {
                    "こっち",
                    "こっち見て",
                    "こっちを見て",
                    "こっちみて",
                    "こっちをみて",
                    "こっち向いて",
                    "こっちを向いて",
                    "こっちむいて",
                    "こっちをむいて",
                    "こっちだよ",
                    "こっちこっち"
                }
            ),

            // 移動系は誤爆を避けるため、短い命令・明確な命令だけ拾う。
            // 例: 「前に言った件」などの通常会話を crawler_forward_short にしない。
            new BodyCommandRule(
                "crawler_forward_short",
                "fixed-rule:crawler-forward-short-command",
                50,
                false,
                false,
                new[]
                {
                    "前",
                    "まえ",
                    "前へ",
                    "まえへ",
                    "前に",
                    "まえに",
                    "前進",
                    "ぜんしん",
                    "進んで",
                    "すすんで",
                    "少し前",
                    "すこし前",
                    "少しまえ",
                    "少し前へ",
                    "すこし前へ",
                    "少しまえへ",
                    "ちょっと前",
                    "ちょっとまえ",
                    "ちょっと前へ",
                    "ちょっとまえへ",
                    "ちょっと前に",
                    "ちょっとまえに",
                    "前へ進んで",
                    "前に進んで",
                    "まえへすすんで",
                    "まえにすすんで",
                    "少し進んで",
                    "すこしすすんで",
                    "ちょっと進んで",
                    "ちょっとすすんで"
                }
            ),

            new BodyCommandRule(
                "crawler_back_short",
                "fixed-rule:crawler-back-short-command",
                50,
                false,
                false,
                new[]
                {
                    "後ろ",
                    "うしろ",
                    "後ろへ",
                    "うしろへ",
                    "後ろに",
                    "うしろに",
                    "後退",
                    "こうたい",
                    "下がって",
                    "さがって",
                    "バック",
                    "ばっく",
                    "少し後ろ",
                    "すこし後ろ",
                    "少しうしろ",
                    "少し後ろへ",
                    "すこし後ろへ",
                    "少しうしろへ",
                    "ちょっと後ろ",
                    "ちょっとうしろ",
                    "ちょっと後ろへ",
                    "ちょっとうしろへ",
                    "ちょっと下がって",
                    "ちょっとさがって",
                    "少し下がって",
                    "すこしさがって"
                }
            ),

            new BodyCommandRule(
                "crawler_turn_left_short",
                "fixed-rule:crawler-turn-left-short-command",
                50,
                false,
                false,
                new[]
                {
                    "左",
                    "ひだり",
                    "左へ",
                    "ひだりへ",
                    "左に",
                    "ひだりに",
                    "左向いて",
                    "ひだり向いて",
                    "左を向いて",
                    "ひだりを向いて",
                    "左回って",
                    "ひだり回って",
                    "左旋回",
                    "ひだり旋回",
                    "左に回って",
                    "ひだりに回って",
                    "ちょっと左",
                    "ちょっとひだり",
                    "ちょっと左へ",
                    "ちょっとひだりへ",
                    "少し左",
                    "すこし左",
                    "少しひだり",
                    "少し左へ",
                    "すこし左へ",
                    "少しひだりへ"
                }
            ),

            new BodyCommandRule(
                "crawler_turn_right_short",
                "fixed-rule:crawler-turn-right-short-command",
                50,
                false,
                false,
                new[]
                {
                    "右",
                    "みぎ",
                    "右へ",
                    "みぎへ",
                    "右に",
                    "みぎに",
                    "右向いて",
                    "みぎ向いて",
                    "右を向いて",
                    "みぎを向いて",
                    "右回って",
                    "みぎ回って",
                    "右旋回",
                    "みぎ旋回",
                    "右に回って",
                    "みぎに回って",
                    "ちょっと右",
                    "ちょっとみぎ",
                    "ちょっと右へ",
                    "ちょっとみぎへ",
                    "少し右",
                    "すこし右",
                    "少しみぎ",
                    "少し右へ",
                    "すこし右へ",
                    "少しみぎへ"
                }
            )
        };

        public static bool TryMatch(string sourceText, out BodyCommandMatch match)
        {
            match = default(BodyCommandMatch);

            string normalized = NormalizeCommandText(sourceText);

            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            for (int i = 0; i < Rules.Length; i++)
            {
                BodyCommandRule rule = Rules[i];

                if (TryMatchRule(normalized, rule, out match))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryMatchRule(
            string normalized,
            BodyCommandRule rule,
            out BodyCommandMatch match
        )
        {
            match = default(BodyCommandMatch);

            string[] phrases = rule.Phrases;

            if (string.IsNullOrEmpty(normalized) || phrases == null)
            {
                return false;
            }

            for (int i = 0; i < phrases.Length; i++)
            {
                string phrase = NormalizeCommandText(phrases[i]);

                if (string.IsNullOrEmpty(phrase))
                {
                    continue;
                }

                bool matched = rule.UseContainsMatch
                    ? normalized.Contains(phrase)
                    : normalized == phrase;

                if (!matched)
                {
                    continue;
                }

                match = new BodyCommandMatch(
                    rule.ActionId,
                    rule.Reason,
                    rule.Priority,
                    rule.SafetyStop,
                    phrase
                );
                return true;
            }

            return false;
        }

        private static string NormalizeCommandText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim();
            normalized = normalized.Replace(" ", string.Empty);
            normalized = normalized.Replace("　", string.Empty);
            normalized = normalized.Replace("、", string.Empty);
            normalized = normalized.Replace("。", string.Empty);
            normalized = normalized.Replace(",", string.Empty);
            normalized = normalized.Replace(".", string.Empty);
            normalized = normalized.Replace("！", string.Empty);
            normalized = normalized.Replace("!", string.Empty);
            normalized = normalized.Replace("？", string.Empty);
            normalized = normalized.Replace("?", string.Empty);
            return normalized;
        }
    }
}
