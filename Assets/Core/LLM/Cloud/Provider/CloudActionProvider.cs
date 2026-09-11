// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Threading.Tasks;
using SalieriAI.Core.LLM.Common;
using UnityEngine;

namespace SalieriAI.CloudLLM
{
    public sealed class CloudActionProvider : MonoBehaviour, ILLMActionProvider
    {
        [Header("Cloud")]
        [SerializeField] private CloudLLMClient cloudClient;

        public async Task<LLMActionDecision> DecideActionAsync(SelfStateSummary summary)
        {
            if (cloudClient == null)
            {
                Debug.LogWarning("[CloudActionProvider] cloudClient is null");
                return LLMActionDecision.SafeDefault("cloud_client_missing");
            }

            if (summary == null)
            {
                Debug.LogWarning("[CloudActionProvider] summary is null");
                return LLMActionDecision.SafeDefault("summary_missing");
            }

            LLMActionDecision decision = await cloudClient.GenerateActionDecisionAsync(summary);

            if (decision == null)
                return LLMActionDecision.SafeDefault("cloud_action_null");

            decision.Normalize();
            return decision;
        }
    }
}