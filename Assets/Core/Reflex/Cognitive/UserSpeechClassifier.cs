// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Reflex.Cognitive
{
    public static class UserSpeechClassifier
    {
        private static readonly string[] AdminEmergencyStopKeywords =
        {
            "絶対停止",
            "緊急停止",
            "非常停止",
            "エマージェンシーストップ"
        };

        private static readonly string[] StopCommandKeywords =
        {
            "止まれ",
            "止まって",
            "止めて",
            "停止",
            "ストップ",
            "やめて",
            "動かないで",
            "待て"
        };

        /*
         * Phase 10-A:
         * 曖昧指示は ActionIntent より先に分類する。
         *
         * 「あれ見て」= 対象が不明
         * 「そこ見て」= 場所・文脈が不明
         *
         * ここでは ExecutionRequest にせず、ConversationReactionService 側で
         * clarification route へ流す。
         */
        private static readonly string[] NeedsTargetKeywords =
        {
            "あれ",
            "それ",
            "これ",
            "あの",
            "その",
            "この"
        };

        private static readonly string[] NeedsContextKeywords =
        {
            "そこ",
            "どこ",
            "どっち",
            "どのへん",
            "そのへん",
            "あそこ"
        };

        private static readonly string[] LookIntentKeywords =
        {
            "見て",
            "向いて",
            "見に行って"
        };

        private static readonly string[] ActionIntentKeywords =
        {
            "探して",
            "見回して",
            "戻って",
            "正面",
            "うなずいて",
            "頷いて",
            "待って",
            "こっち",
            "あっち"
        };

        private static readonly string[] ReferentialConversationKeywords =
        {
            "さっき見てたやつ",
            "さっき見ていたやつ",
            "今見てるもの",
            "今見ているもの"
        };

        public static UserSpeechClassificationResult Classify(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new UserSpeechClassificationResult(
                    UserSpeechClass.Unknown,
                    text,
                    "empty"
                );
            }

            string trimmed = text.Trim();
            string normalized = Normalize(trimmed);

            if (ContainsAny(normalized, AdminEmergencyStopKeywords))
            {
                return new UserSpeechClassificationResult(
                    UserSpeechClass.AdminEmergencyStop,
                    trimmed,
                    "admin emergency stop keyword"
                );
            }

            if (ContainsAny(normalized, StopCommandKeywords))
            {
                return new UserSpeechClassificationResult(
                    UserSpeechClass.StopCommand,
                    trimmed,
                    "stop command keyword"
                );
            }

            // Reference Resolution v1: these bounded noun phrases describe
            // an object; the embedded 「見て」 is not a body command.
            if (ContainsAny(normalized, ReferentialConversationKeywords))
            {
                return new UserSpeechClassificationResult(
                    UserSpeechClass.Conversation,
                    trimmed,
                    "referential conversation expression"
                );
            }

            /*
             * Phase 10-A:
             * 「あれ見て」「そこ見て」などは、見たい意思はあるが、
             * 実行対象・実行文脈が足りない。
             *
             * ActionIntent として BodyActionExecutor へ流す前に止める。
             */
            if (ContainsAny(normalized, LookIntentKeywords))
            {
                if (ContainsAny(normalized, NeedsTargetKeywords))
                {
                    return new UserSpeechClassificationResult(
                        UserSpeechClass.NeedsTarget,
                        trimmed,
                        "needs target clarification"
                    );
                }

                if (ContainsAny(normalized, NeedsContextKeywords))
                {
                    return new UserSpeechClassificationResult(
                        UserSpeechClass.NeedsContext,
                        trimmed,
                        "needs context clarification"
                    );
                }
            }

            /*
             * SR-0:
             * 方向語や単独の「見て / 向いて」は、身体動作の対象や種類を
             * 確定しない。これらを部分一致だけで ActionIntent にすると、
             * 「右手」「右の方がいい」などの自然な会話まで旧
             * ProgrammedAction / NeedsDirection 経路が consume する。
             *
             * 対象・文脈を伴う既存の LOOK clarification は上で維持し、
             * ここでは明示的なProgrammedAction語彙だけを分類する。
             */
            if (ContainsAny(normalized, ActionIntentKeywords))
            {
                return new UserSpeechClassificationResult(
                    UserSpeechClass.ActionIntent,
                    trimmed,
                    "action intent keyword"
                );
            }

            return new UserSpeechClassificationResult(
                UserSpeechClass.Conversation,
                trimmed,
                "default conversation"
            );
        }

        private static bool ContainsAny(string text, string[] keywords)
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
