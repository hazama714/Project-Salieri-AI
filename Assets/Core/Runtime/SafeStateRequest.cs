// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Input;
using SalieriAI.Core.State;

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// Phase 7-A:
    /// Interruptを「直接停止命令」ではなく、SafeStateへ向かう要求として表す最小データ。
    ///
    /// 現段階では observe-only のログ用。
    /// ExecutionController / BodyActionExecutor / VoiceController へはまだ接続しない。
    /// </summary>
    [Serializable]
    public sealed class SafeStateRequest
    {
        public string RequestId;

        public InterruptType InterruptType;
        public SafeStateReason Reason;

        public ExternalInputType SourceEventType;
        public string SourcePayload;
        public int SourcePriority;

        public InteractionState CurrentState;
        public bool HasStateController;
        public bool HasLimboPermission;
        public bool CanInterrupt;
        public bool IsEmergencyMode;

        public string Route;
        public string Message;

        public static SafeStateRequest Create(
            InterruptType interruptType,
            SafeStateReason reason,
            ExternalInputEvent sourceEvent,
            InteractionState currentState,
            bool hasStateController,
            bool hasLimboPermission,
            bool canInterrupt,
            bool isEmergencyMode,
            string route,
            string message)
        {
            return new SafeStateRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                InterruptType = interruptType,
                Reason = reason,
                SourceEventType = sourceEvent != null ? sourceEvent.Type : ExternalInputType.None,
                SourcePayload = sourceEvent != null ? (sourceEvent.Payload ?? string.Empty) : string.Empty,
                SourcePriority = sourceEvent != null ? (int)sourceEvent.Priority : 0,
                CurrentState = currentState,
                HasStateController = hasStateController,
                HasLimboPermission = hasLimboPermission,
                CanInterrupt = canInterrupt,
                IsEmergencyMode = isEmergencyMode,
                Route = route ?? string.Empty,
                Message = message ?? string.Empty
            };
        }

        public override string ToString()
        {
            return
                "requestId=" + RequestId +
                " interrupt=" + InterruptType +
                " reason=" + Reason +
                " source=" + SourceEventType +
                " payload=" + SourcePayload +
                " priority=" + SourcePriority +
                " state=" + (HasStateController ? CurrentState.ToString() : "Unknown") +
                " canInterrupt=" + (HasLimboPermission ? CanInterrupt.ToString() : "Unknown") +
                " emergency=" + (HasLimboPermission ? IsEmergencyMode.ToString() : "Unknown") +
                " route=" + Route +
                " message=" + Message;
        }
    }
}
