// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.CloudLLM
{
    [CreateAssetMenu(
        fileName = "CloudLLMSettings",
        menuName = "SalieriAI/CloudLLMSettings"
    )]
    public sealed class CloudLLMSettings : ScriptableObject
    {
        [Header("OpenAI")]
        public string apiKey;

        [Header("Model")]
        public string model = "gpt-5.4-mini";

        [Header("Request")]
        public int timeoutSeconds = 20;

        public int maxOutputTokens = 2;
    }
}