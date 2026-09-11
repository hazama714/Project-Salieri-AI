// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Core.Input;
using SalieriAI.Core.Language.Contracts;
using SalieriAI.Core.Language.Runtime;

namespace SalieriAI.Core.Reflex.Cognitive
{
    /// <summary>
    /// ExternalInputBuffer から来た高レベル入力を、
    /// Cognitive / Conversation 系の反応処理へ振り分ける入口。
    ///
    /// ここでは会話判断そのものは行わない。
    /// UserSpeech を ConversationReactionService へ渡すだけにする。
    /// </summary>
    public class CognitiveReflexController : MonoBehaviour
    {
        [Header("Reaction Services")]
        [Tooltip("UserSpeech を会話・起動語判定へ渡すサービス。未設定だと音声入力が会話に進まない。")]
        [SerializeField]
        private ConversationReactionService conversationReactionService;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private void Reset()
        {
            AutoFindReferences();
        }

        private void Awake()
        {
            if (conversationReactionService == null)
            {
                AutoFindReferences();
            }

            if (conversationReactionService == null)
            {
                Debug.LogWarning(
                    "[CognitiveReflexController] ConversationReactionService is not assigned. " +
                    "UserSpeech will not be routed to conversation."
                );
            }
        }

        public void ProcessExternalInput(ExternalInputEvent inputEvent)
        {
            if (inputEvent == null)
            {
                if (verboseLog)
                {
                    Debug.LogWarning(
                        "[CognitiveReflexController] ProcessExternalInput skipped: inputEvent is null."
                    );
                }

                return;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[CognitiveReflexController] ProcessExternalInput: " +
                    inputEvent.Type +
                    " source=" +
                    SafeLog(inputEvent.Source) +
                    " payload=" +
                    SafeLog(inputEvent.Payload)
                );
            }

            switch (inputEvent.Type)
            {
                case ExternalInputType.UserSpeech:
                    RouteUserSpeech(inputEvent);
                    break;

                default:
                    if (verboseLog)
                    {
                        Debug.Log(
                            "[CognitiveReflexController] No handler for: " +
                            inputEvent.Type
                        );
                    }

                    break;
            }
        }

        private void RouteUserSpeech(ExternalInputEvent inputEvent)
        {
            string text = inputEvent != null
                ? inputEvent.Payload
                : string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[CognitiveReflexController] UserSpeech skipped: text is empty."
                    );
                }

                return;
            }

            CommunicationInput communicationInput =
                inputEvent.CommunicationInput;

            if (communicationInput == null)
            {
                UserInputSource source;

                if (!System.Enum.TryParse(inputEvent.Source, true, out source))
                {
                    source = UserInputSource.Unknown;
                }

                communicationInput =
                    CommunicationInput.CreateAccepted(
                        text,
                        text.Trim(),
                        source
                    );
            }

            InteractionTraceLogger.LogRuntimeStage(
                "COGNITIVE_REFLEX",
                communicationInput,
                "ConversationReactionService"
            );

            if (conversationReactionService == null)
            {
                Debug.LogWarning(
                    "[CognitiveReflexController] UserSpeech route failed: " +
                    "ConversationReactionService is not assigned. text=" +
                    SafeLog(text)
                );
                return;
            }

            if (verboseLog)
            {
                Debug.Log(
                    "[CognitiveReflexController] Route UserSpeech -> ConversationReactionService text=" +
                    SafeLog(text)
                );
            }

            conversationReactionService.ReactToUserSpeech(
                communicationInput
            );
        }

        [ContextMenu("Auto Find References")]
        private void AutoFindReferences()
        {
            if (conversationReactionService == null)
            {
#if UNITY_2023_1_OR_NEWER
                conversationReactionService = FindFirstObjectByType<ConversationReactionService>();
#else
                conversationReactionService = FindObjectOfType<ConversationReactionService>();
#endif
            }
        }

        private static string SafeLog(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "<empty>"
                : value;
        }
    }
}
