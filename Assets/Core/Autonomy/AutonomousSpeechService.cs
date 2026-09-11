// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Threading.Tasks;
using SalieriAI.Core.LLM.Common;
using SalieriAI.Core.Limbo;
using SalieriAI.Expression.Voice;
using UnityEngine;

namespace SalieriAI.Autonomy
{
    public sealed class AutonomousSpeechService : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private LLMRouteController llmRouteController;
        [SerializeField] private VoiceController voiceController;
        [SerializeField] private LimboPermission limboPermission;

        [Header("Voice")]
        [SerializeField] private string defaultSpeakerName = "冥鳴ひまり";

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        public async Task SpeakForActionAsync(
            SelfStateSummary summary,
            LLMActionDecision actionDecision)
        {
            if (summary == null)
            {
                Debug.LogWarning("[AutonomousSpeechService] summary is null");
                return;
            }

            if (actionDecision == null)
            {
                Debug.LogWarning("[AutonomousSpeechService] actionDecision is null");
                return;
            }

            actionDecision.Normalize();

            if (!ShouldSpeak(actionDecision))
                return;

            if (!CanSpeakNow())
                return;

            if (llmRouteController == null)
            {
                Debug.LogWarning("[AutonomousSpeechService] llmRouteController is null");
                return;
            }

            if (voiceController == null)
            {
                Debug.LogWarning("[AutonomousSpeechService] voiceController is null");
                return;
            }

            LLMSpeechDecision speechDecision =
                await llmRouteController.DecideSpeechAsync(summary, actionDecision);

            if (speechDecision == null)
                return;

            speechDecision.Normalize();

            if (!speechDecision.HasSpeech())
                return;

            if (verboseLog)
            {
                Debug.Log(
                    $"[AutonomousSpeechService] speak={speechDecision.speech} " +
                    $"reason={speechDecision.reason}"
                );
            }

            voiceController.Speak(speechDecision.speech, defaultSpeakerName);
        }

        private static bool ShouldSpeak(LLMActionDecision actionDecision)
        {
            if (actionDecision == null)
                return false;

            if (actionDecision.action == "speakShort")
                return true;

            if (!string.IsNullOrWhiteSpace(actionDecision.speech))
                return true;

            return false;
        }

        private bool CanSpeakNow()
        {
            if (limboPermission == null)
                return true;

            if (limboPermission.IsEmergencyMode)
                return false;

            if (!limboPermission.CanSpeak)
                return false;

            return true;
        }
    }
}