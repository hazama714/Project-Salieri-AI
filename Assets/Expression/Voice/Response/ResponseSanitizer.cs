// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// ResponseSanitizer v2025-09-08-SS03
// Timestamp (JST): 2025-09-08 00:15
// Comment (最小改修・統合):
// - SS02 に (J) 手順を追加："<|eot_id|>" 以降を強制的に切り捨て（UI/TTS漏洩防止の最終関門）。
// - 既存の構造・命名・処理順は維持。追加のみ（削除・置換なし）。
// - 全199行構成（差分あり：数行追加のみ。既存正規表現・ロジックは不変更）。

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AIChatSystem
{
    public static class ResponseSanitizer
    {
        // === 正規表現（Compiled, CultureInvariant） ===
        // 1) ChatML ブロック・トークン除去
        private static readonly Regex RxChatMlBlock =
            new Regex(@"<\|im_start\|>.*?<\|im_end\|>", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RxChatMlHeaderBlock =
            new Regex(@"<\|start_header_id\|>.*?<\|end_header_id\|>", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RxChatMlTokens =
            new Regex(@"<\|(eot_id|endoftext|assistant|user|system|im_start|im_end)\|>", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 2) 役割（role）行・接頭語
        private static readonly Regex RxRoleOnlyLine =
            new Regex(@"(?im)^\s*(system|assistant|user|システム|アシスタント|ユーザー)\s*:?\s*$",
                      RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RxRoleInlineHead =
            new Regex(@"(?im)^\s*(system|assistant|user)\s*:\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 3) 崩れた "system" スパム対策
        private static readonly Regex RxLeadingSystemSpam =
            new Regex(@"(?is)^\s*(?:system){1,}(?:[a-z]{0,8})?\s*[:\-]?\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RxLineLeadingSystemSpam =
            new Regex(@"(?im)^\s*(?:system){1,}(?:[a-z]{0,8})?\s*[:\-]?\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 4) 制御文字
        private static readonly Regex RxCtrlChars =
            new Regex(@"[\u0000-\u0008\u000B-\u001F\u007F]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 5) 重複行圧縮
        private static readonly Regex RxCollapseRepeatLines =
            new Regex(@"(?m)^(.*)(?:\r?\n\1\b)+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 6) 連続空行縮約
        private static readonly Regex RxBlankLines =
            new Regex(@"\n{3,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 7) Echoヘッダ抑止
        private static readonly Regex RxEchoHeadUntilAssistant =
            new Regex(@".*?<\|start_header_id\|>\s*assistant\s*<\|end_header_id\|>\s*", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 8) 文頭短フレーズの重複
        private static readonly Regex RxHeadShortRepeat =
            new Regex(@"^((..|...))\1+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string Filter(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string s = input.Replace("\r\n", "\n").Replace("\r", "\n");

            // (A) Echoヘッダ抑止
            s = RxEchoHeadUntilAssistant.Replace(s, string.Empty);

            // (B) ChatML除去
            s = RxChatMlBlock.Replace(s, string.Empty);
            s = RxChatMlHeaderBlock.Replace(s, string.Empty);
            s = RxChatMlTokens.Replace(s, string.Empty);

            // (C) 役割行削除
            s = RxRoleOnlyLine.Replace(s, string.Empty);

            // (D) 行頭ロール接頭辞剥離
            s = RxRoleInlineHead.Replace(s, string.Empty);

            // (E) systemスパム削除
            s = RxLeadingSystemSpam.Replace(s, string.Empty);
            s = RxLineLeadingSystemSpam.Replace(s, string.Empty);

            // (F) 制御文字除去
            s = RxCtrlChars.Replace(s, string.Empty);

            // (G) 重複行圧縮
            s = RxCollapseRepeatLines.Replace(s, "$1");

            // (H) 連続空行縮約
            s = RxBlankLines.Replace(s, "\n\n");

            // (I) 文頭短フレーズ二重化抑制
            s = RxHeadShortRepeat.Replace(s, "$1");

            // (J) <|eot_id|> 以降を削除
            int eotIndex = s.IndexOf("<|eot_id|>", StringComparison.OrdinalIgnoreCase);
            if (eotIndex >= 0)
            {
                s = s.Substring(0, eotIndex);
            }

            s = s.Trim();
            return s;
        }
    }
}
