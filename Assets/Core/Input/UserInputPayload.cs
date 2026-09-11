// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Input
{
    /// <summary>
    /// UserSpeech Runtime 本流へ渡す入力ペイロード。
    ///
    /// ただし Phase 10-D では既存 ConversationReactionService が string を受けるため、
    /// Runtime 上の Payload は引き続き string を使う。
    /// この型は source / raw / normalized を保持したい場合の拡張用。
    /// </summary>
    [Serializable]
    public sealed class UserInputPayload
    {
        public string RawText;
        public string NormalizedText;
        public UserInputSource Source;
        public float Time;

        public UserInputPayload(
            string rawText,
            string normalizedText,
            UserInputSource source,
            float time
        )
        {
            RawText = rawText;
            NormalizedText = normalizedText;
            Source = source;
            Time = time;
        }
    }
}