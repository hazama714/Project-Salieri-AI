// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

using SalieriAI.Core.Input;
using SalieriAI.Core.Language.Cgl;
using SalieriAI.Core.Language.Contracts;
using SalieriAI.Core.Language.Runtime;
using SalieriAI.Core.Perception.Speech;

namespace SalieriAI.Sensors.Audio
{
    /// <summary>
    /// Phase 10-D:
    /// ユーザー入力の共通入口。
    ///
    /// AndroidSTT / TextInput / DebugInput / 将来LocalSTT / ExternalSTT を
    /// 同じ UserSpeech Runtime 本流へ流す。
    /// </summary>
    public class UserSpeechRouter : MonoBehaviour
    {
        [SerializeField]
        private UserSpeechInputBuffer speechInputBuffer;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        /// <summary>
        /// 既存互換入口。
        /// AndroidSTTReceiver など旧コードから呼ばれても AndroidSTT として扱う。
        /// </summary>
        public void OnUserSpeechRecognized(string text)
        {
            OnUserInputReceived(
                text,
                UserInputSource.AndroidSTT
            );
        }

        /// <summary>
        /// Phase 10-D 共通入口。
        /// AndroidSTT / TextInput / DebugInput / LocalSTT / ExternalSTT をここへ集約する。
        /// </summary>
        public void OnUserInputReceived(
            string text,
            UserInputSource source
        )
        {
            string rawText = text ?? string.Empty;
            string normalized = InputNormalizer.NormalizeUserInput(text);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[UserSpeechRouter] Empty input ignored. source=" +
                        source
                    );
                }

                return;
            }

            CommunicationInput communicationInput =
                CommunicationInput.CreateAccepted(
                    rawText,
                    normalized,
                    source
                );

            InteractionTraceLogger.LogAdmission(communicationInput);

            // Phase 2A Shadow Mode:
            // Analyze exactly once without changing or blocking the production route.
            CglInterpretation shadowInterpretation;
            CommunicationGateService.TryAnalyzeShadow(
                communicationInput,
                out shadowInterpretation
            );

            if (verboseLog)
            {
                Debug.Log(
                    "[UserSpeechRouter] User input received. " +
                    "inputId=" +
                    communicationInput.InputId +
                    " interactionId=" +
                    communicationInput.InteractionId +
                    " " +
                    "source=" +
                    source +
                    " text=" +
                    normalized
                );
            }

            if (speechInputBuffer == null)
            {
                Debug.LogWarning(
                    "[UserSpeechRouter] Input dropped: UserSpeechInputBuffer is not assigned. " +
                    "source=" +
                    source +
                    " text=" +
                    normalized
                );
                return;
            }

            speechInputBuffer.Push(communicationInput);
        }
    }
}
