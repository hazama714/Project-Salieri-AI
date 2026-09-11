// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Expression.Voice
{
    public sealed class VoiceController : MonoBehaviour
    {
        [Header("Output")]
        [SerializeField] private MonoBehaviour voiceOutputBehaviour;

        private IVoiceOutput VoiceOutput =>
            voiceOutputBehaviour as IVoiceOutput;

        public void Speak(string text, string speakerName)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (VoiceOutput == null)
            {
                UnityEngine.Debug.LogWarning("[VoiceController] VoiceOutput is null");
                return;
            }

            VoiceOutput.Speak(text, speakerName);
        }
    }
}