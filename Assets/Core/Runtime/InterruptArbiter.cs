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
    /// ExternalInputEvent / InteractionState / LimboPermission の現在値から、
    /// 将来どの割り込みとして扱うかを分類する。
    ///
    /// この段階では分類のみ。
    /// Stop / Cancel / State変更 / Reprocess は行わない。
    /// </summary>
    public static class InterruptArbiter
    {
        public static SafeStateRequest Classify(
            ExternalInputEvent inputEvent,
            InteractionState currentState,
            bool hasStateController,
            bool hasLimboPermission,
            bool canInterrupt,
            bool isEmergencyMode)
        {
            if (inputEvent == null)
            {
                return null;
            }

            InterruptType interruptType;
            SafeStateReason reason;
            string message;

            if (!TryClassify(
                inputEvent,
                currentState,
                hasStateController,
                hasLimboPermission,
                canInterrupt,
                isEmergencyMode,
                out interruptType,
                out reason,
                out message))
            {
                return null;
            }

            return SafeStateRequest.Create(
                interruptType: interruptType,
                reason: reason,
                sourceEvent: inputEvent,
                currentState: currentState,
                hasStateController: hasStateController,
                hasLimboPermission: hasLimboPermission,
                canInterrupt: canInterrupt,
                isEmergencyMode: isEmergencyMode,
                route: "observe-only/no-state-change/no-cancel",
                message: message
            );
        }

        private static bool TryClassify(
            ExternalInputEvent inputEvent,
            InteractionState currentState,
            bool hasStateController,
            bool hasLimboPermission,
            bool canInterrupt,
            bool isEmergencyMode,
            out InterruptType interruptType,
            out SafeStateReason reason,
            out string message)
        {
            interruptType = InterruptType.None;
            reason = SafeStateReason.None;
            message = string.Empty;

            if (inputEvent == null)
            {
                return false;
            }

            string payload = inputEvent.Payload ?? string.Empty;

            // Emergency is allowed to be classified even when Limbo is already locked.
            if (inputEvent.Type == ExternalInputType.Emergency ||
                currentState == InteractionState.Emergency ||
                isEmergencyMode)
            {
                interruptType = InterruptType.Emergency;
                reason = SafeStateReason.Emergency;
                message = "Emergency classified. Phase7-B observes only; no cancel / stop is executed.";
                return true;
            }

            // For non-emergency interrupts, do not create misleading SafeStateRequest candidates
            // before the state and Limbo references are available.
            if (!hasStateController)
            {
                return false;
            }

            if (!hasLimboPermission)
            {
                return false;
            }

            if (!canInterrupt)
            {
                return false;
            }

            switch (inputEvent.Type)
            {
                case ExternalInputType.UserSpeech:
                    return TryClassifyUserSpeech(
                        payload,
                        currentState,
                        out interruptType,
                        out reason,
                        out message
                    );

                case ExternalInputType.FaceEvent:
                    return TryClassifyFaceEvent(
                        payload,
                        currentState,
                        out interruptType,
                        out reason,
                        out message
                    );

                case ExternalInputType.DeviceStateChange:
                    return TryClassifyDeviceStateChange(
                        payload,
                        out interruptType,
                        out reason,
                        out message
                    );

                case ExternalInputType.Touch:
                    return TryClassifyTouch(
                        currentState,
                        out interruptType,
                        out reason,
                        out message
                    );

                default:
                    return false;
            }
        }

        private static bool TryClassifyUserSpeech(
            string payload,
            InteractionState currentState,
            out InterruptType interruptType,
            out SafeStateReason reason,
            out string message)
        {
            interruptType = InterruptType.None;
            reason = SafeStateReason.None;
            message = string.Empty;

            if (IsStopCommand(payload))
            {
                interruptType = InterruptType.StopCommand;
                reason = SafeStateReason.StopCommand;
                message = "UserSpeech payload looks like stop command. Future route: HardInterrupt / SafeStateRequest. Phase7-B observes only.";
                return true;
            }

            if (currentState == InteractionState.Speaking)
            {
                interruptType = InterruptType.SpeechInterrupt;
                reason = SafeStateReason.UserSpeech;
                message = "UserSpeech arrived while Speaking. Future route: speech safe-state / reprocess. Phase7-B observes only.";
                return true;
            }

            if (currentState == InteractionState.Acting ||
                currentState == InteractionState.Searching ||
                currentState == InteractionState.Thinking ||
                currentState == InteractionState.Recovering)
            {
                interruptType = InterruptType.Soft;
                reason = SafeStateReason.UserSpeech;
                message = "UserSpeech arrived during active state. Future route: SoftInterrupt / SafeStateRequest. Phase7-B observes only.";
                return true;
            }

            return false;
        }

        private static bool TryClassifyFaceEvent(
            string payload,
            InteractionState currentState,
            out InterruptType interruptType,
            out SafeStateReason reason,
            out string message)
        {
            interruptType = InterruptType.None;
            reason = SafeStateReason.None;
            message = string.Empty;

            if (IsStableFound(payload) &&
                (currentState == InteractionState.Searching ||
                 currentState == InteractionState.TemporaryLost ||
                 currentState == InteractionState.FullyLost))
            {
                interruptType = InterruptType.FaceTrackOverride;
                reason = SafeStateReason.FaceDetected;
                message = "Face stable found while searching/lost. Future route: stop search or look_at_user. Phase7-B observes only.";
                return true;
            }

            if (IsFullyLost(payload) && currentState == InteractionState.Acting)
            {
                interruptType = InterruptType.Soft;
                reason = SafeStateReason.FaceLost;
                message = "Face fully lost while Acting. Future route: safe-state then re-evaluate target. Phase7-B observes only.";
                return true;
            }

            return false;
        }

        private static bool TryClassifyDeviceStateChange(
            string payload,
            out InterruptType interruptType,
            out SafeStateReason reason,
            out string message)
        {
            interruptType = InterruptType.None;
            reason = SafeStateReason.None;
            message = string.Empty;

            if (!IsResourceLimited(payload))
            {
                return false;
            }

            interruptType = InterruptType.ResourceLimited;
            reason = SafeStateReason.ResourceLimited;
            message = "DeviceStateChange indicates resource limitation. Future route: resource-limited SafeState. Phase7-B observes only.";
            return true;
        }

        private static bool TryClassifyTouch(
            InteractionState currentState,
            out InterruptType interruptType,
            out SafeStateReason reason,
            out string message)
        {
            interruptType = InterruptType.None;
            reason = SafeStateReason.None;
            message = string.Empty;

            if (!IsBusyState(currentState))
            {
                return false;
            }

            interruptType = InterruptType.Soft;
            reason = SafeStateReason.Touch;
            message = "Touch arrived while runtime is busy. Future route: safe-state request. Phase7-B observes only.";
            return true;
        }

        private static bool IsBusyState(InteractionState state)
        {
            return
                state == InteractionState.Thinking ||
                state == InteractionState.Speaking ||
                state == InteractionState.Acting ||
                state == InteractionState.Searching ||
                state == InteractionState.Recovering;
        }

        private static bool IsStopCommand(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            return
                Contains(payload, "止まれ") ||
                Contains(payload, "とまれ") ||
                Contains(payload, "止めて") ||
                Contains(payload, "止めろ") ||
                Contains(payload, "やめて") ||
                Contains(payload, "やめろ") ||
                Contains(payload, "停止") ||
                Contains(payload, "中止") ||
                Contains(payload, "中断") ||
                Contains(payload, "待って") ||
                Contains(payload, "まって") ||
                Contains(payload, "ストップ") ||
                Contains(payload, "キャンセル") ||
                Contains(payload, "取り消し") ||
                Contains(payload, "stop") ||
                Contains(payload, "halt") ||
                Contains(payload, "cancel") ||
                Contains(payload, "abort");
        }

        private static bool IsStableFound(string payload)
        {
            return Contains(payload, "StableFound") || Contains(payload, "FaceFound");
        }

        private static bool IsFullyLost(string payload)
        {
            return Contains(payload, "FullyLost") || Contains(payload, "FaceLost");
        }

        private static bool IsResourceLimited(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            return
                Contains(payload, "ResourceLimited") ||
                Contains(payload, "Thermal") ||
                Contains(payload, "BatteryLow") ||
                Contains(payload, "LowBattery") ||
                Contains(payload, "BluetoothDisconnected") ||
                Contains(payload, "Disconnected") ||
                Contains(payload, "Disconnect") ||
                Contains(payload, "Overheat") ||
                Contains(payload, "LowPower") ||
                Contains(payload, "Resource") ||
                Contains(payload, "バッテリー") ||
                Contains(payload, "低電力") ||
                Contains(payload, "温度") ||
                Contains(payload, "過熱") ||
                Contains(payload, "切断");
        }

        private static bool Contains(string source, string value)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            {
                return false;
            }

            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
