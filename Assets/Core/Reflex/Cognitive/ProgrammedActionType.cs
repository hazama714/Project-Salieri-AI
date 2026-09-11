// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Reflex.Cognitive
{
    /// <summary>
    /// Phase 9-R-1 observe-only.
    ///
    /// STT / RemainingText から推定された ProgrammedAction 候補。
    /// この enum は「実行命令」ではなく、候補ログ用の分類である。
    /// ExecutionController / BodyActionExecutor へはまだ渡さない。
    /// </summary>
    public enum ProgrammedActionType
    {
        Unknown = 0,

        None,

        LookAtUser,
        SearchUser,
        LightSearch,
        LookAround,
        LookAtUserOrSearch,

        ReturnCenter,
        IdleNod,
        Hold,
        Stop,

        NeedsContext,
        NeedsTarget,
        NeedsDirection,
        NeedsClarification
    }
}
