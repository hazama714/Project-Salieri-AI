// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.LLM.Common
{
    [Serializable]
    public sealed class LLMSpeechDecision
    {
        public string speech = "";
        public string reason = "";
        public float confidence = 0f;

        public bool HasSpeech()
        {
            return !string.IsNullOrWhiteSpace(speech);
        }

        public void Normalize()
        {
            if (speech == null)
                speech = "";

            if (reason == null)
                reason = "";

            if (confidence < 0f)
                confidence = 0f;

            if (confidence > 1f)
                confidence = 1f;
        }

        public static LLMSpeechDecision Empty(string reason = "empty")
        {
            return new LLMSpeechDecision
            {
                speech = "",
                reason = reason,
                confidence = 0f
            };
        }
    }
}