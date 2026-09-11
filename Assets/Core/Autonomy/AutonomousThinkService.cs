// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Threading.Tasks;
using UnityEngine;

namespace SalieriAI.Autonomy
{
    public sealed class AutonomousThinkService : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private BodyActionExecutor bodyActionExecutor;
        [SerializeField] private AutonomousSpeechService speechService;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        public async Task ExecuteAsync(
            SelfStateSummary summary,
            LLMActionDecision decision)
        {
            if (decision == null)
            {
                Debug.LogWarning("[AutonomousThinkService] decision is null");
                return;
            }

            decision.Normalize();

            if (verboseLog)
            {
                Debug.Log(
                    $"[AutonomousThinkService] action={decision.action} " +
                    $"reason={decision.reason}"
                );
            }

            if (bodyActionExecutor == null)
            {
                Debug.LogWarning("[AutonomousThinkService] bodyActionExecutor is null");
                return;
            }

            bodyActionExecutor.Execute(decision);

            if (speechService != null)
                await speechService.SpeakForActionAsync(summary, decision);
        }
    }
}