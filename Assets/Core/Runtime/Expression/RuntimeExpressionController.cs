// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

using SalieriAI.Core.Limbo;
using SalieriAI.Core.Perception.Buffer;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// Expression Runtime入口。
    ///
    /// FaceEvent / Conversation / ActionResult / Persona tone などから来た
    /// 感情・状況情報を、Runtime状態・Limbo・顔状態と統合し、
    /// 最終的な ExpressionIntent として ExpressionReactionHub へ渡す。
    ///
    /// Phase 10-M-1:
    /// 会話LLMから受け取った Emotion を ExpressionIntent へ変換する。
    ///
    /// Emotion が空または未知の場合は、
    /// 既存のキーワード判定へフォールバックする。
    /// </summary>
    public sealed class RuntimeExpressionController : MonoBehaviour
    {
        [Header("Runtime Refs")]
        [SerializeField]
        private InteractionStateController stateController;

        [SerializeField]
        private LimboPermission limboPermission;

        [SerializeField]
        private FacePerceptionBuffer facePerceptionBuffer;

        [Header("Expression Output")]
        [SerializeField]
        private global::ExpressionReactionHub expressionReactionHub;

        [Header("Face Expression Policy")]
        [SerializeField]
        private float minNoFaceDurationForConcernedSeconds = 3.5f;

        [Header("Conversation Expression Policy")]
        [SerializeField]
        private bool enableConversationExpression = true;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private void Awake()
        {
            if (stateController == null)
            {
                stateController =
                    FindObjectOfType<InteractionStateController>();
            }

            if (limboPermission == null)
            {
                limboPermission =
                    FindObjectOfType<LimboPermission>();
            }

            if (facePerceptionBuffer == null)
            {
                facePerceptionBuffer =
                    FindObjectOfType<FacePerceptionBuffer>();
            }

            if (expressionReactionHub == null)
            {
                expressionReactionHub =
                    FindObjectOfType<global::ExpressionReactionHub>();
            }
        }

        public void RequestFromFaceEvent(
            string faceState,
            string reason
        )
        {
            ExpressionIntent intent =
                DecideFaceExpression(
                    faceState
                );

            RequestExpression(
                intent,
                ExpressionSource.FaceEvent,
                "faceState=" +
                Safe(faceState) +
                " reason=" +
                Safe(reason)
            );
        }

        public void RequestFromIdleFullyLost(
            string reason
        )
        {
            RequestExpression(
                ExpressionIntent.Concerned,
                ExpressionSource.IdleTick,
                reason
            );
        }

        /// <summary>
        /// 旧会話表情入口。
        /// Emotionが取得できない場合のフォールバックとして残す。
        /// </summary>
        public void RequestFromConversation(
            string userText,
            string responseText,
            string reason
        )
        {
            if (!enableConversationExpression)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeExpressionController] " +
                        "Conversation expression skipped: disabled."
                    );
                }

                return;
            }

            ExpressionIntent intent =
                DecideConversationExpression(
                    userText,
                    responseText
                );

            RequestExpression(
                intent,
                ExpressionSource.Conversation,
                "reason=" +
                Safe(reason) +
                " user=" +
                Safe(userText) +
                " response=" +
                Safe(responseText)
            );
        }

        /// <summary>
        /// Phase 10-M-1:
        /// LLM会話返答に付属するEmotionを受け取る新しい入口。
        ///
        /// Emotionが未知または空の場合は、
        /// 旧キーワード判定へ戻す。
        /// </summary>
        public void RequestFromConversationEmotion(
            string emotion,
            string userText,
            string responseText,
            string reason
        )
        {
            if (!enableConversationExpression)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeExpressionController] " +
                        "Conversation emotion skipped: disabled."
                    );
                }

                return;
            }

            ExpressionIntent intent =
                DecideConversationEmotionExpression(
                    emotion
                );

            if (intent == ExpressionIntent.None)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeExpressionController] " +
                        "Conversation emotion fallback to keyword. " +
                        "emotion=" +
                        Safe(emotion)
                    );
                }

                RequestFromConversation(
                    userText,
                    responseText,
                    Safe(reason) +
                    " fallback=keyword"
                );

                return;
            }

            RequestExpression(
                intent,
                ExpressionSource.Conversation,
                "emotion=" +
                Safe(emotion) +
                " reason=" +
                Safe(reason) +
                " user=" +
                Safe(userText) +
                " response=" +
                Safe(responseText)
            );
        }

        public void RequestExpression(
            ExpressionIntent intent,
            ExpressionSource source,
            string reason
        )
        {
            if (intent == ExpressionIntent.None ||
                intent == ExpressionIntent.Keep)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeExpressionController] " +
                        "Expression skipped: " +
                        "intent=" +
                        intent +
                        " source=" +
                        source +
                        " reason=" +
                        Safe(reason)
                    );
                }

                return;
            }

            if (!CanOutputExpression(
                intent,
                source
            ))
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeExpressionController] " +
                        "Expression blocked: " +
                        "intent=" +
                        intent +
                        " source=" +
                        source +
                        " state=" +
                        GetStateForLog() +
                        " reason=" +
                        Safe(reason)
                    );
                }

                return;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeExpressionController] " +
                    "Expression request: " +
                    "intent=" +
                    intent +
                    " source=" +
                    source +
                    " state=" +
                    GetStateForLog() +
                    " reason=" +
                    Safe(reason)
                );
            }

            if (expressionReactionHub == null)
            {
                Debug.LogWarning(
                    "[RuntimeExpressionController] " +
                    "ExpressionReactionHub is not assigned."
                );

                return;
            }

            expressionReactionHub.PlayIntent(
                intent,
                source.ToString(),
                reason
            );
        }

        private ExpressionIntent DecideFaceExpression(
            string faceState
        )
        {
            if (string.Equals(
                faceState,
                "StableFound",
                System.StringComparison.Ordinal
            ))
            {
                if (facePerceptionBuffer != null &&
                    facePerceptionBuffer.PreviousState ==
                    FacePerceptionState.FullyLost)
                {
                    return ExpressionIntent.Relieved;
                }

                return ExpressionIntent.Attentive;
            }

            if (string.Equals(
                faceState,
                "TemporaryLost",
                System.StringComparison.Ordinal
            ))
            {
                return ExpressionIntent.Keep;
            }

            if (string.Equals(
                faceState,
                "FullyLost",
                System.StringComparison.Ordinal
            ))
            {
                // FaceEvent.FullyLost自体は知覚通知のみ。
                // 即座に表情変更しない。
                //
                // 見失った状態が継続した場合は、
                // IdleTick.FullyLostからConcernedを要求する。
                return ExpressionIntent.Keep;
            }

            return ExpressionIntent.None;
        }

        /// <summary>
        /// LLMが返したEmotionタグを、
        /// Runtime側のExpressionIntentへ変換する。
        ///
        /// VRM / FBX固有のBlendShape名は扱わない。
        /// </summary>
        private static ExpressionIntent
            DecideConversationEmotionExpression(
                string emotion
            )
        {
            if (string.IsNullOrWhiteSpace(emotion))
                return ExpressionIntent.None;

            string normalized =
                emotion.Trim();

            if (string.Equals(
                normalized,
                "Joy",
                System.StringComparison.OrdinalIgnoreCase
            ))
            {
                return ExpressionIntent.JoySoft;
            }

            if (string.Equals(
                normalized,
                "Fun",
                System.StringComparison.OrdinalIgnoreCase
            ))
            {
                return ExpressionIntent.JoySoft;
            }

            if (string.Equals(
                normalized,
                "Sorrow",
                System.StringComparison.OrdinalIgnoreCase
            ))
            {
                return ExpressionIntent.Concerned;
            }

            if (string.Equals(
                normalized,
                "Angry",
                System.StringComparison.OrdinalIgnoreCase
            ))
            {
                // 現在はAngry専用Intent未実装。
                // 暫定的にConcernedへ寄せる。
                return ExpressionIntent.Concerned;
            }

            if (string.Equals(
                normalized,
                "Neutral",
                System.StringComparison.OrdinalIgnoreCase
            ))
            {
                return ExpressionIntent.Attentive;
            }

            return ExpressionIntent.None;
        }

        /// <summary>
        /// Emotionタグが取得できない場合に使う旧キーワード判定。
        /// Local fallbackや異常応答時の退避路として残す。
        /// </summary>
        private ExpressionIntent DecideConversationExpression(
            string userText,
            string responseText
        )
        {
            string joined =
                (
                    (userText ?? string.Empty) +
                    " " +
                    (responseText ?? string.Empty)
                ).Trim();

            if (ContainsAny(
                joined,
                "ありがとう",
                "ありがと",
                "すごい",
                "えらい",
                "助かった",
                "嬉しい",
                "うれしい"
            ))
            {
                return ExpressionIntent.JoySoft;
            }

            if (ContainsAny(
                joined,
                "ごめん",
                "大丈夫",
                "心配",
                "こわい",
                "怖い",
                "痛い",
                "つらい"
            ))
            {
                return ExpressionIntent.Concerned;
            }

            if (ContainsAny(
                joined,
                "はい",
                "うん",
                "聞いてる",
                "わかった",
                "了解"
            ))
            {
                return ExpressionIntent.Attentive;
            }

            return ExpressionIntent.Attentive;
        }

        private bool CanOutputExpression(
            ExpressionIntent intent,
            ExpressionSource source
        )
        {
            if (limboPermission != null &&
                limboPermission.IsEmergencyMode)
            {
                return false;
            }

            InteractionState state =
                stateController != null
                    ? stateController.CurrentState
                    : InteractionState.Idle;

            if (state == InteractionState.Booting)
                return false;

            return true;
        }

        private string GetStateForLog()
        {
            return stateController != null
                ? stateController.CurrentState.ToString()
                : "Unknown";
        }

        private static bool ContainsAny(
            string text,
            params string[] words
        )
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]) &&
                    text.Contains(words[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Safe(
            string value
        )
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }
    }
}