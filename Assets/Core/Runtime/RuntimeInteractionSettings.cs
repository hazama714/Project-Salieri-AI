// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// Phase 10-F:
    /// Mic / Text / Voice / Conversation continuation の最小設定受け皿。
    ///
    /// SettingsPanel 本体は後工程。
    /// ここでは Inspector から運用値を切り替えられる Script 側の器だけを用意する。
    /// </summary>
    public sealed class RuntimeInteractionSettings : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private bool enableMicrophoneInput = true;
        [SerializeField] private bool enableTextInput = true;

        [Header("Output")]
        [SerializeField] private bool enableVoiceOutput = true;
        [SerializeField] private bool showTextOutput = true;

        [Header("Timing")]
        [SerializeField] private float reListenDelaySeconds = 0.75f;
        [SerializeField] private float conversationContinueSeconds = 8.0f;

        public bool EnableMicrophoneInput => enableMicrophoneInput;
        public bool EnableTextInput => enableTextInput;
        public bool EnableVoiceOutput => enableVoiceOutput;
        public bool ShowTextOutput => showTextOutput;

        public float ReListenDelaySeconds => Mathf.Max(0.0f, reListenDelaySeconds);
        public float ConversationContinueSeconds => Mathf.Max(1.0f, conversationContinueSeconds);

        public void SetMicrophoneInputEnabled(bool enabled)
        {
            enableMicrophoneInput = enabled;
        }

        public void SetTextInputEnabled(bool enabled)
        {
            enableTextInput = enabled;
        }

        public void SetVoiceOutputEnabled(bool enabled)
        {
            enableVoiceOutput = enabled;
        }

        public void SetTextOutputEnabled(bool enabled)
        {
            showTextOutput = enabled;
        }

        public void SetReListenDelaySeconds(float seconds)
        {
            reListenDelaySeconds = Mathf.Max(0.0f, seconds);
        }

        public void SetConversationContinueSeconds(float seconds)
        {
            conversationContinueSeconds = Mathf.Max(1.0f, seconds);
        }
    }
}
