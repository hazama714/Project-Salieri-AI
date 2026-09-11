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
    /// 表情要求がどこから来たかを表す。
    /// FaceEvent専用にせず、会話・行動・Persona由来も同じ入口へ集約する。
    /// </summary>
    public enum ExpressionSource
    {
        Unknown = 0,
        FaceEvent = 1,
        IdleTick = 2,
        Conversation = 3,
        ActionResult = 4,
        Persona = 5,
        System = 6
    }
}
