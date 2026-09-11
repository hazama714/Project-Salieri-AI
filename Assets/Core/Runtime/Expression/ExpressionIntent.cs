// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// Runtime側で決定された最終的な表情要求。
    /// Emotionそのものではなく、顔に出す出力意図。
    /// </summary>
    public enum ExpressionIntent
    {
        None = 0,
        Keep = 1,
        Neutral = 2,
        JoySoft = 3,
        Joy = 4,
        Attentive = 5,
        Concerned = 6,
        Relieved = 7
    }
}
