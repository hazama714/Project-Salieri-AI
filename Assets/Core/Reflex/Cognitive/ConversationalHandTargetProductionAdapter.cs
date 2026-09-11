// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.FreePose;
using SalieriAI.Body.SpatialTarget;
using SalieriAI.Core.LLM.Common;
using SalieriAI.Core.Skills.Body.Pointing;

namespace SalieriAI.Core.Reflex.Cognitive
{
    public readonly struct ConversationalHandTargetHandoffResult
    {
        public bool HasUserTargetDemand { get; }
        public bool RequestAttempted { get; }
        public ProductionHandTargetRequestResult ProductionResult { get; }
        public bool DynamicPointExecuted { get; }
        public string Reason { get; }

        internal ConversationalHandTargetHandoffResult(
            bool hasUserTargetDemand,
            bool requestAttempted,
            ProductionHandTargetRequestResult productionResult,
            string reason,
            bool dynamicPointExecuted = false)
        {
            HasUserTargetDemand = hasUserTargetDemand;
            RequestAttempted = requestAttempted;
            ProductionResult = productionResult;
            DynamicPointExecuted = dynamicPointExecuted;
            Reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Minimal Production bridge for Cloud-selected, allow-listed semantic
    /// hand targets. It selects no pose and writes no Transform itself.
    /// </summary>
    public static class ConversationalHandTargetProductionAdapter
    {
        public const string RequestSource =
            "ConversationService.ConversationalHandTarget";

        public static ConversationalHandTargetHandoffResult Handle(
            ConversationLLMResponse response,
            string turnId,
            Action<string> logSink)
        {
            ConversationLLMResponse safe =
                response ?? ConversationLLMResponse.Empty();
            safe.Normalize();

            bool rejectedInput =
                safe.ConversationalHandTargetInputRejected;
            bool hasDemand = rejectedInput ||
                !ConversationalHandTargetSelectionV0.IsKeep(
                    safe.rightHandTarget) ||
                !ConversationalHandTargetSelectionV0.IsKeep(
                    safe.leftHandTarget);

            if (rejectedInput)
            {
                const string rejected =
                    "conversation_hand_target_output_rejected";
                Log(logSink, safe, turnId, null,
                    "REJECTED", rejected);
                return new ConversationalHandTargetHandoffResult(
                    true, false, null, rejected);
            }

            if (!hasDemand)
            {
                const string keep = "conversation_hand_targets_keep";
                Log(logSink, safe, turnId, null,
                    "NO_CHANGE", keep);
                return new ConversationalHandTargetHandoffResult(
                    false, false, null, keep);
            }

            if (string.IsNullOrWhiteSpace(turnId))
            {
                const string missing =
                    "conversation_hand_target_turn_id_missing";
                Log(logSink, safe, turnId, null,
                    "REJECTED", missing);
                return new ConversationalHandTargetHandoffResult(
                    true, false, null, missing);
            }

            bool rightDynamic =
                safe.rightHandTarget ==
                    SemanticBodyDynamicTargetIds.CurrentAttentionTarget;
            bool leftDynamic =
                safe.leftHandTarget ==
                    SemanticBodyDynamicTargetIds.CurrentAttentionTarget;
            if (rightDynamic || leftDynamic)
            {
                return HandleDynamicPoint(
                    safe,
                    turnId,
                    rightDynamic,
                    leftDynamic,
                    logSink);
            }

            if (!ConversationalAttentionPointRuntime
                    .TryReleaseCapturedPoint(out string pointReleaseReason))
            {
                Log(logSink, safe, turnId, null,
                    "REJECTED", pointReleaseReason);
                return new ConversationalHandTargetHandoffResult(
                    true, false, null, pointReleaseReason);
            }

            var request = new ProductionHandTargetRequest(
                turnId,
                RequestSource,
                safe.rightHandTarget,
                safe.leftHandTarget);
            bool attempted = ProductionPoseRequestRuntime.TryRequestHandTargets(
                request,
                out ProductionHandTargetRequestResult result,
                out string reason);
            Log(
                logSink,
                safe,
                turnId,
                result,
                attempted ? "REQUESTED" : "NOT_REQUESTED",
                reason);
            return new ConversationalHandTargetHandoffResult(
                true, attempted, result, reason);
        }

        private static ConversationalHandTargetHandoffResult
            HandleDynamicPoint(
                ConversationLLMResponse response,
                string turnId,
                bool rightDynamic,
                bool leftDynamic,
                Action<string> logSink)
        {
            bool otherTargetSelected = rightDynamic
                ? !ConversationalHandTargetSelectionV0.IsKeep(
                    response.leftHandTarget)
                : !ConversationalHandTargetSelectionV0.IsKeep(
                    response.rightHandTarget);
            if (rightDynamic == leftDynamic || otherTargetSelected)
            {
                const string invalid =
                    "conversation_point_requires_one_arm_and_other_keep";
                Log(logSink, response, turnId, null,
                    "REJECTED", invalid);
                return new ConversationalHandTargetHandoffResult(
                    true, false, null, invalid);
            }

            if (!ConversationalAttentionPointRuntime.IsAvailable)
            {
                const string unavailable =
                    "conversation_point_runtime_unavailable";
                Log(logSink, response, turnId, null,
                    "NOT_REQUESTED", unavailable);
                return new ConversationalHandTargetHandoffResult(
                    true, false, null, unavailable);
            }

            if (ProductionPoseRequestRuntime.IsAvailable &&
                !ProductionPoseRequestRuntime.TryReleaseActivePose(
                    out string releaseReason))
            {
                Log(logSink, response, turnId, null,
                    "REJECTED", releaseReason);
                return new ConversationalHandTargetHandoffResult(
                    true, false, null, releaseReason);
            }

            HandTargetArm arm = rightDynamic
                ? HandTargetArm.Right
                : HandTargetArm.Left;
            bool executed = ConversationalAttentionPointRuntime.TryExecute(
                arm,
                turnId,
                out PointAttentionTargetResult pointResult,
                out string reason);
            Log(
                logSink,
                response,
                turnId,
                null,
                executed ? "POINT_EXECUTED" : "POINT_REJECTED",
                reason,
                pointResult);
            return new ConversationalHandTargetHandoffResult(
                true,
                true,
                null,
                reason,
                executed);
        }

        private static void Log(
            Action<string> logSink,
            ConversationLLMResponse response,
            string turnId,
            ProductionHandTargetRequestResult result,
            string execution,
            string reason,
            PointAttentionTargetResult pointResult = null)
        {
            logSink?.Invoke(
                "[ConversationalHandTarget]" +
                " TurnId=" + Text(turnId) +
                " Right=" + Text(response.rightHandTarget) +
                " Left=" + Text(response.leftHandTarget) +
                " Status=" +
                (result != null
                    ? result.Status.ToString()
                    : pointResult != null
                        ? "POINT_EXECUTED"
                        : "NONE") +
                " CapturedTarget=" +
                Text(pointResult != null
                    ? pointResult.CapturedTargetKey
                    : string.Empty) +
                " Reason=" + Text(reason) +
                " Execution=" + execution);
        }

        private static string Text(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "<empty>"
                : value;
        }
    }
}
