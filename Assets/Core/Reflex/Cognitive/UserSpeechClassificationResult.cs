// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Reflex.Cognitive
{
    public sealed class UserSpeechClassificationResult
    {
        public UserSpeechClass SpeechClass { get; }
        public string Text { get; }
        public string Reason { get; }

        public UserSpeechClassificationResult(
            UserSpeechClass speechClass,
            string text,
            string reason
        )
        {
            SpeechClass = speechClass;
            Text = text ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
