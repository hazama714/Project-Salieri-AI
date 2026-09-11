// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Reflex.Cognitive
{
    /// <summary>
    /// Phase 9-R-1 observe-only resolver.
    ///
    /// 目的:
    /// RemainingText を ProgrammedAction 候補へ読む。
    ///
    /// 注意:
    /// - ここでは実行しない。
    /// - ExecutionController / BodyActionExecutor / NeckController / VoiceController は呼ばない。
    /// - 「こそあど」系の未解決対象は Needs* として止める。
    /// - 将来 TrainingCommandResolver / DeicticSpatialResolver に分割してよい。
    /// </summary>
    public static class ProgrammedActionResolver
    {
        public static ProgrammedActionCandidate Resolve(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return ProgrammedActionCandidate.Unknown(
                    text,
                    string.Empty,
                    "empty"
                );
            }

            string source = text.Trim();
            string normalized = Normalize(source);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return ProgrammedActionCandidate.Unknown(
                    source,
                    normalized,
                    "normalized empty"
                );
            }

            // こっち系は「座標指定」ではなく、ユーザー注目 / 探索要求として扱う。
            if (ContainsAny(normalized, "こっち見て", "こっちを見て", "こっち向いて", "こっちを向いて"))
            {
                return Candidate(
                    ProgrammedActionType.LookAtUserOrSearch,
                    ProgrammedActionType.SearchUser,
                    source,
                    normalized,
                    "deictic user attention request",
                    false
                );
            }

            if (ContainsAny(normalized, "こっちこっち", "こっち"))
            {
                return Candidate(
                    ProgrammedActionType.SearchUser,
                    ProgrammedActionType.LightSearch,
                    source,
                    normalized,
                    "deictic user search request",
                    false
                );
            }

            // 対象・場所・方向が未解決のこそあど言葉。
            // 「見て」を含んでいても、対象未解決なら実行候補ではなく Needs* に寄せる。
            if (ContainsAny(normalized, "あれ"))
            {
                return Candidate(
                    ProgrammedActionType.NeedsTarget,
                    ProgrammedActionType.Unknown,
                    source,
                    normalized,
                    "deictic target is unresolved",
                    true
                );
            }

            if (ContainsAny(normalized, "そこ"))
            {
                return Candidate(
                    ProgrammedActionType.NeedsContext,
                    ProgrammedActionType.Unknown,
                    source,
                    normalized,
                    "deictic place/context is unresolved",
                    true
                );
            }

            if (ContainsAny(normalized, "あっち"))
            {
                return Candidate(
                    ProgrammedActionType.NeedsDirection,
                    ProgrammedActionType.Unknown,
                    source,
                    normalized,
                    "deictic direction is unresolved",
                    true
                );
            }

            if (ContainsAny(normalized, "どこ", "どっち"))
            {
                return Candidate(
                    ProgrammedActionType.NeedsClarification,
                    ProgrammedActionType.Unknown,
                    source,
                    normalized,
                    "question/clarification request",
                    true
                );
            }

            // しつけ言葉 / 短い行動合図。
            if (ContainsAny(normalized, "戻って", "戻れ", "正面", "正面向いて", "真ん中", "センター"))
            {
                return Candidate(
                    ProgrammedActionType.ReturnCenter,
                    ProgrammedActionType.Unknown,
                    source,
                    normalized,
                    "return center command",
                    false
                );
            }

            if (ContainsAny(normalized, "うなずいて", "頷いて", "うなずけ", "頷け"))
            {
                return Candidate(
                    ProgrammedActionType.IdleNod,
                    ProgrammedActionType.Unknown,
                    source,
                    normalized,
                    "nod command",
                    false
                );
            }

            if (ContainsAny(normalized, "見回して", "周り見て", "まわり見て"))
            {
                return Candidate(
                    ProgrammedActionType.LookAround,
                    ProgrammedActionType.LightSearch,
                    source,
                    normalized,
                    "look around command",
                    false
                );
            }

            if (ContainsAny(normalized, "探して", "探せ"))
            {
                return Candidate(
                    ProgrammedActionType.LightSearch,
                    ProgrammedActionType.LookAround,
                    source,
                    normalized,
                    "search command",
                    false
                );
            }

            // Phase 9-Rでは「待って」は停止破壊ではなく Hold 候補として読む。
            if (ContainsAny(normalized, "待って", "ちょっと待って", "そのまま"))
            {
                return Candidate(
                    ProgrammedActionType.Hold,
                    ProgrammedActionType.Unknown,
                    source,
                    normalized,
                    "hold command",
                    false
                );
            }

            // StopCommand は通常 UserSpeechClassifier 側で先に拾われる。
            // ただし将来の直接Resolveやデバッグに備えて分類を残す。
            if (ContainsAny(normalized, "止まって", "止まれ", "止めて", "停止", "ストップ", "やめて", "動かないで"))
            {
                return Candidate(
                    ProgrammedActionType.Stop,
                    ProgrammedActionType.Unknown,
                    source,
                    normalized,
                    "stop command candidate",
                    false
                );
            }

            // 方向語だけでは身体座標・対象・意図が不足するため、まだ直接実行候補にしない。
            if (ContainsAny(normalized, "上", "下", "左", "右", "前", "後ろ"))
            {
                return Candidate(
                    ProgrammedActionType.NeedsDirection,
                    ProgrammedActionType.Unknown,
                    source,
                    normalized,
                    "direction word needs spatial context",
                    true
                );
            }

            // 「見て」「向いて」だけでは対象が未解決。
            if (ContainsAny(normalized, "見て", "向いて"))
            {
                return Candidate(
                    ProgrammedActionType.NeedsTarget,
                    ProgrammedActionType.Unknown,
                    source,
                    normalized,
                    "look/turn command needs target",
                    true
                );
            }

            return ProgrammedActionCandidate.Unknown(
                source,
                normalized,
                "no programmed action rule matched"
            );
        }

        private static ProgrammedActionCandidate Candidate(
            ProgrammedActionType primary,
            ProgrammedActionType secondary,
            string source,
            string normalized,
            string reason,
            bool requiresAdditionalContext
        )
        {
            return new ProgrammedActionCandidate(
                primary,
                secondary,
                source,
                normalized,
                reason,
                requiresAdditionalContext
            );
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords == null)
            {
                return false;
            }

            for (int i = 0; i < keywords.Length; i++)
            {
                string keyword = Normalize(keywords[i]);
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                if (text.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

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
    }
}
