// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.FreePose;

namespace SalieriAI.Core.LLM.Common
{
    /// <summary>
    /// ユーザー会話に対するLLM返答の最小構造。
    /// speechは実際に発話する文章。
    /// emotionは表情Runtimeへ渡す意味タグ。
    /// </summary>
    [Serializable]
    public sealed class ConversationLLMResponse
    {
        public string speech;
        public string emotion;
        public string overallPoseIntent;
        public string rightArmIntent;
        public string leftArmIntent;
        public string headIntent;
        public string targetIntent;
        public string rightHandTarget;
        public string leftHandTarget;

        [NonSerialized]
        private bool conversationalHandTargetInputRejected;

        public const string DefaultOverallPoseIntent = "Unknown";
        public const string DefaultArmIntent = "Keep";
        public const string DefaultHeadIntent = "Keep";
        public const string DefaultTargetIntent = "None";
        public const string DefaultHandTarget =
            ConversationalHandTargetSelectionV0.Keep;

        public bool ConversationalHandTargetInputRejected =>
            conversationalHandTargetInputRejected;

        private static readonly string[] OverallPoseIntentValues =
        {
            "Unknown",
            "Keep",
            "Greeting",
            "Explain",
            "Present",
            "Emphasize",
            "Listen",
            "Think",
            "React"
        };

        private static readonly string[] ArmIntentValues =
        {
            "Keep",
            "Neutral",
            "Present",
            "Open",
            "Raise",
            "Emphasize",
            "PointLike"
        };

        private static readonly string[] HeadIntentValues =
        {
            "Keep",
            "LookAtPartner",
            "LookAtTarget",
            "Neutral"
        };

        private static readonly string[] TargetIntentValues =
        {
            "None",
            "ConversationPartner",
            "CurrentObject",
            "Self"
        };

        public void Normalize()
        {
            speech = string.IsNullOrWhiteSpace(speech)
                ? string.Empty
                : speech.Trim();

            emotion = NormalizeEmotion(emotion);
            overallPoseIntent = NormalizeAllowedValue(
                overallPoseIntent,
                OverallPoseIntentValues,
                DefaultOverallPoseIntent);
            rightArmIntent = NormalizeAllowedValue(
                rightArmIntent,
                ArmIntentValues,
                DefaultArmIntent);
            leftArmIntent = NormalizeAllowedValue(
                leftArmIntent,
                ArmIntentValues,
                DefaultArmIntent);
            headIntent = NormalizeAllowedValue(
                headIntent,
                HeadIntentValues,
                DefaultHeadIntent);
            targetIntent = NormalizeAllowedValue(
                targetIntent,
                TargetIntentValues,
                DefaultTargetIntent);

            bool valid =
                ConversationalHandTargetSelectionV0.TryNormalize(
                    rightHandTarget,
                    leftHandTarget,
                    out ConversationalHandTargetSelection selection,
                    out _);
            conversationalHandTargetInputRejected |= !valid;
            rightHandTarget = valid
                ? selection.RightHandTarget
                : DefaultHandTarget;
            leftHandTarget = valid
                ? selection.LeftHandTarget
                : DefaultHandTarget;
        }

        public bool HasSpeech()
        {
            return !string.IsNullOrWhiteSpace(speech);
        }

        public static ConversationLLMResponse FromSpeech(
            string speech,
            string emotion
        )
        {
            ConversationLLMResponse response =
                new ConversationLLMResponse
                {
                    speech = speech,
                    emotion = emotion
                };

            response.Normalize();
            return response;
        }

        public static ConversationLLMResponse Empty()
        {
            return FromSpeech(string.Empty, "Neutral");
        }

        public void SuppressConversationalHandTargets()
        {
            rightHandTarget = DefaultHandTarget;
            leftHandTarget = DefaultHandTarget;
            conversationalHandTargetInputRejected = false;
        }

        public static string NormalizeEmotion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Neutral";

            string normalized = value.Trim();

            if (string.Equals(
                normalized,
                "Joy",
                StringComparison.OrdinalIgnoreCase
            ))
            {
                return "Joy";
            }

            if (string.Equals(
                normalized,
                "Fun",
                StringComparison.OrdinalIgnoreCase
            ))
            {
                return "Fun";
            }

            if (string.Equals(
                normalized,
                "Sorrow",
                StringComparison.OrdinalIgnoreCase
            ))
            {
                return "Sorrow";
            }

            if (string.Equals(
                normalized,
                "Angry",
                StringComparison.OrdinalIgnoreCase
            ))
            {
                return "Angry";
            }

            return "Neutral";
        }

        private static string NormalizeAllowedValue(
            string value,
            string[] allowedValues,
            string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            string normalized = value.Trim();
            for (int i = 0; i < allowedValues.Length; i++)
            {
                if (string.Equals(
                    normalized,
                    allowedValues[i],
                    StringComparison.OrdinalIgnoreCase))
                {
                    return allowedValues[i];
                }
            }

            return fallback;
        }
    }
}
