// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Reflex.Cognitive
{
    [Serializable]
    public sealed class PersonaActivationProfile
    {
        public string displayName = "アシス";
        public string[] activationAliases = Array.Empty<string>();
        public string activationAck = "はい";

        public string GetActivationAckOrDefault()
        {
            return string.IsNullOrWhiteSpace(activationAck)
                ? "はい"
                : activationAck.Trim();
        }
    }
}
