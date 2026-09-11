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

namespace SalieriAI.Core.Perception.Speech
{
    /// <summary>
    /// UserSpeech 入力を一時保持し、ExternalInputBuffer へ Runtime Event として積む。
    ///
    /// Phase 10-D:
    /// AndroidSTT / TextInput / DebugInput などの入力元 source を保持する。
    /// </summary>
    public class UserSpeechInputBuffer : MonoBehaviour
    {
        [SerializeField]
        private ExternalInputBuffer externalInputBuffer;

        public string LastText { get; private set; }
        public float LastInputTime { get; private set; }
        public bool HasUnreadInput { get; private set; }

        public UserInputSource LastInputSource { get; private set; } =
            UserInputSource.Unknown;

        public CommunicationInput LastCommunicationInput { get; private set; }

        /// <summary>
        /// 既存互換入口。
        /// source不明の UserSpeech として扱う。
        /// </summary>
        public void Push(string text)
        {
            Push(
                text,
                UserInputSource.Unknown
            );
        }

        /// <summary>
        /// Phase 10-D:
        /// 入力元 source 付きで UserSpeech Runtime 本流へ流す。
        /// </summary>
        public void Push(
            string text,
            UserInputSource source
        )
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            Push(
                CommunicationInput.CreateAccepted(
                    text,
                    text.Trim(),
                    source
                )
            );
        }

        public void Push(CommunicationInput communicationInput)
        {
            if (communicationInput == null ||
                string.IsNullOrWhiteSpace(communicationInput.NormalizedText))
            {
                return;
            }

            LastCommunicationInput = communicationInput;
            LastText = communicationInput.NormalizedText;
            LastInputTime = Time.time;
            LastInputSource = communicationInput.Source;
            HasUnreadInput = true;

            Debug.Log(
                "[UserSpeechInputBuffer] Push: " +
                LastText +
                " source=" +
                LastInputSource +
                " inputId=" +
                communicationInput.InputId +
                " interactionId=" +
                communicationInput.InteractionId
            );

            if (externalInputBuffer == null)
            {
                Debug.LogWarning(
                    "[UserSpeechInputBuffer] ExternalInputBuffer is not assigned. " +
                    "text=" +
                    LastText +
                    " source=" +
                    LastInputSource +
                    " inputId=" +
                    communicationInput.InputId +
                    " interactionId=" +
                    communicationInput.InteractionId
                );
                return;
            }

            externalInputBuffer.Push(
                ExternalInputType.UserSpeech,
                ExternalInputPriority.High,
                communicationInput
            );
        }

        public bool TryConsume(out string text)
        {
            if (!HasUnreadInput || string.IsNullOrWhiteSpace(LastText))
            {
                text = null;
                return false;
            }

            text = LastText;
            HasUnreadInput = false;

            Debug.Log(
                "[UserSpeechInputBuffer] Consume: " +
                text +
                " source=" +
                LastInputSource
            );

            return true;
        }

        public void Clear()
        {
            LastText = null;
            LastInputSource = UserInputSource.Unknown;
            LastCommunicationInput = null;
            HasUnreadInput = false;

            Debug.Log("[UserSpeechInputBuffer] Clear");
        }
    }
}
