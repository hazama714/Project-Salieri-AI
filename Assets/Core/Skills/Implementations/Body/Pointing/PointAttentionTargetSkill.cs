// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Threading;
using System.Threading.Tasks;

using SalieriAI.Body.SpatialTarget;

namespace SalieriAI.Core.Skills.Body.Pointing
{
    [Serializable]
    public sealed class PointAttentionTargetRequest : ISkillPayload
    {
        public bool CaptureCurrentAttentionTarget;
        public string RequestedArm = string.Empty;
    }

    [Serializable]
    public sealed class PointAttentionTargetResult : ISkillPayload
    {
        public string ActiveArmId = string.Empty;
        public string CapturedTargetKey = string.Empty;
    }

    /// <summary>
    /// 既存AttentionTargetとHandTargetのPOINT経路をSkill境界から開始する。
    /// </summary>
    public sealed class PointAttentionTargetSkill :
        SkillHandler<PointAttentionTargetRequest, PointAttentionTargetResult>
    {
        public const string Id = "point_attention_target";
        public const int CurrentVersion = 1;

        private readonly IAttentionTargetPointingController controller;

        public PointAttentionTargetSkill(
            IAttentionTargetPointingController controller)
            : base(
                Id,
                CurrentVersion,
                "point_attention_target_request",
                "point_attention_target_result")
        {
            this.controller = controller;
        }

        protected override Task<SkillExecutionResult<PointAttentionTargetResult>>
            ExecuteTypedAsync(
                PointAttentionTargetRequest input,
                SkillExecutionContext context)
        {
            DateTime startedAt = DateTime.UtcNow;
            if (controller == null)
            {
                return Task.FromResult(
                    SkillExecutionResult<PointAttentionTargetResult>.Failed(
                        "pointing_controller_unavailable",
                        "AttentionTarget pointing controller is unavailable.",
                        startedAt));
            }

            string capturedTargetKey = string.Empty;
            bool started;
            if (input != null && input.CaptureCurrentAttentionTarget)
            {
                if (!(controller is
                    ICapturedAttentionTargetPointingController captured))
                {
                    return Task.FromResult(
                        SkillExecutionResult<PointAttentionTargetResult>.Failed(
                            "captured_attention_pointing_unavailable",
                            "Captured AttentionTarget pointing is unavailable.",
                            startedAt));
                }

                if (!TryParseArm(input.RequestedArm, out HandTargetArm arm))
                {
                    return Task.FromResult(
                        SkillExecutionResult<PointAttentionTargetResult>.Failed(
                            "pointing_arm_invalid",
                            "POINT requires exactly Right or Left.",
                            startedAt));
                }

                started = captured.TryBeginCapturedAttentionPointing(
                    arm,
                    out capturedTargetKey);
            }
            else
            {
                started = controller.BeginPointing();
            }

            if (!started)
            {
                return Task.FromResult(
                    SkillExecutionResult<PointAttentionTargetResult>.Failed(
                        "pointing_start_failed",
                        controller.LastFailureReason,
                        startedAt));
            }

            return Task.FromResult(
                SkillExecutionResult<PointAttentionTargetResult>.Succeeded(
                    new PointAttentionTargetResult
                    {
                        ActiveArmId = controller.ActiveArmId,
                        CapturedTargetKey = capturedTargetKey
                    },
                    "AttentionTarget pointing started.",
                    startedAt));
        }

        private static bool TryParseArm(
            string value,
            out HandTargetArm arm)
        {
            if (string.Equals(value, "Right", StringComparison.Ordinal))
            {
                arm = HandTargetArm.Right;
                return true;
            }
            if (string.Equals(value, "Left", StringComparison.Ordinal))
            {
                arm = HandTargetArm.Left;
                return true;
            }

            arm = default;
            return false;
        }
    }

    /// <summary>
    /// Production availability boundary for Conversation-selected POINT.
    /// It invokes the existing POINT Skill and owns no target or body state.
    /// </summary>
    public static class ConversationalAttentionPointRuntime
    {
        private static ICapturedAttentionTargetPointingController current;

        public static bool IsAvailable => current != null;

        public static bool TryRegister(
            ICapturedAttentionTargetPointingController controller)
        {
            if (controller == null)
                return false;
            if (current != null && !ReferenceEquals(current, controller))
                return false;
            current = controller;
            return true;
        }

        public static void Unregister(
            ICapturedAttentionTargetPointingController controller)
        {
            if (ReferenceEquals(current, controller))
                current = null;
        }

        public static bool TryExecute(
            HandTargetArm arm,
            string turnId,
            out PointAttentionTargetResult result,
            out string reason)
        {
            result = null;
            if (current == null)
            {
                reason = "conversation_point_runtime_unavailable";
                return false;
            }

            if (current.IsCapturedAttentionPointing)
                current.StopPointing(false);

            var request = new PointAttentionTargetRequest
            {
                CaptureCurrentAttentionTarget = true,
                RequestedArm = arm.ToString()
            };
            var context = new SkillExecutionContext(
                string.IsNullOrWhiteSpace(turnId)
                    ? "conversation-point"
                    : turnId.Trim(),
                "conversation-body-action",
                "point-current-attention",
                DateTime.UtcNow,
                "runtime",
                CancellationToken.None);
            SkillExecutionResult execution =
                new PointAttentionTargetSkill(current)
                    .ExecuteUntypedAsync(request, context)
                    .GetAwaiter()
                    .GetResult();
            if (execution == null || !execution.IsSuccess)
            {
                reason = execution != null &&
                    !string.IsNullOrWhiteSpace(execution.FailureReason)
                        ? execution.FailureReason
                        : "conversation_point_execution_failed";
                return false;
            }

            result = execution.Payload as PointAttentionTargetResult;
            reason = string.Empty;
            return result != null;
        }

        public static bool TryReleaseCapturedPoint(out string reason)
        {
            if (current == null || !current.IsCapturedAttentionPointing)
            {
                reason = string.Empty;
                return true;
            }

            current.StopPointing(false);
            bool released = !current.IsCapturedAttentionPointing;
            reason = released
                ? string.Empty
                : "conversation_point_release_failed";
            return released;
        }

        internal static void ResetForTests()
        {
            current = null;
        }
    }
}
