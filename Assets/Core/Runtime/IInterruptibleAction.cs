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
    /// Phase 7-A:
    /// Minimal interface for future interruptible runtime actions.
    ///
    /// This is not connected yet.
    /// BodyActionExecutor / VoiceController / NeckController should not implement this until the later Phase 7 steps.
    /// </summary>
    public interface IInterruptibleAction
    {
        bool IsRunning { get; }
        bool IsSafeStateReached { get; }
        bool CanEnterSafeState { get; }

        void RequestSafeState(SafeStateReason reason);
        void CancelAction();
    }
}
