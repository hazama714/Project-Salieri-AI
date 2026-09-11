// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Body.FreePose
{
    /// <summary>
    /// Validated, semantic hand-target selection produced by Conversation.
    /// Values are exact existing SpatialTarget IDs or Keep. It carries no
    /// coordinate, joint, servo, or physical-reached information.
    /// </summary>
    public readonly struct ConversationalHandTargetSelection
    {
        public string RightHandTarget { get; }
        public string LeftHandTarget { get; }

        public bool ChangesRightHand =>
            !ConversationalHandTargetSelectionV0.IsKeep(RightHandTarget);
        public bool ChangesLeftHand =>
            !ConversationalHandTargetSelectionV0.IsKeep(LeftHandTarget);
        public bool HasTargetChange => ChangesRightHand || ChangesLeftHand;
        public bool PointsRightAtCurrentAttention =>
            ConversationalHandTargetSelectionV0.IsCurrentAttentionTarget(
                RightHandTarget);
        public bool PointsLeftAtCurrentAttention =>
            ConversationalHandTargetSelectionV0.IsCurrentAttentionTarget(
                LeftHandTarget);
        public bool HasDynamicPerceptionTarget =>
            PointsRightAtCurrentAttention || PointsLeftAtCurrentAttention;

        internal ConversationalHandTargetSelection(
            string rightHandTarget,
            string leftHandTarget)
        {
            RightHandTarget = rightHandTarget;
            LeftHandTarget = leftHandTarget;
        }
    }

    /// <summary>
    /// Runtime allow-list boundary for untrusted LLM-selected hand targets.
    /// The LLM can select only the twelve existing Free Pose targets or Keep.
    /// </summary>
    public static class ConversationalHandTargetSelectionV0
    {
        public const string Keep = "Keep";

        public static bool TryNormalize(
            string rightHandTarget,
            string leftHandTarget,
            out ConversationalHandTargetSelection selection,
            out string reason)
        {
            if (!TryNormalizeArm(
                    rightHandTarget,
                    true,
                    out string right,
                    out reason))
            {
                selection = default;
                return false;
            }

            if (!TryNormalizeArm(
                    leftHandTarget,
                    false,
                    out string left,
                    out reason))
            {
                selection = default;
                return false;
            }

            selection = new ConversationalHandTargetSelection(right, left);
            reason = string.Empty;
            return true;
        }

        public static bool TryCreateDefinition(
            string requestId,
            ConversationalHandTargetSelection selection,
            out FreePoseDefinition definition,
            out string reason)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(requestId))
            {
                reason = "conversation_hand_target_request_id_missing";
                return false;
            }

            if (!TryNormalize(
                    selection.RightHandTarget,
                    selection.LeftHandTarget,
                    out ConversationalHandTargetSelection normalized,
                    out reason))
            {
                return false;
            }

            if (normalized.HasDynamicPerceptionTarget)
            {
                reason = "dynamic_perception_target_requires_point_executor";
                return false;
            }

            definition = new FreePoseDefinition(
                "conversation_hand_targets_v0:" + requestId.Trim(),
                normalized.ChangesRightHand
                    ? FreePoseArmDirective.ForSpatialTarget(
                        normalized.RightHandTarget)
                    : FreePoseArmDirective.Keep(),
                normalized.ChangesLeftHand
                    ? FreePoseArmDirective.ForSpatialTarget(
                        normalized.LeftHandTarget)
                    : FreePoseArmDirective.Keep(),
                FreePoseHeadIntent.Keep);
            return definition.TryValidate(out reason);
        }

        public static bool IsKeep(string value)
        {
            return string.Equals(value, Keep, StringComparison.Ordinal);
        }

        public static bool IsCurrentAttentionTarget(string value)
        {
            return string.Equals(
                value,
                SemanticBodyDynamicTargetIds.CurrentAttentionTarget,
                StringComparison.Ordinal);
        }

        private static bool TryNormalizeArm(
            string value,
            bool rightArm,
            out string normalized,
            out string reason)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(
                    value.Trim(),
                    Keep,
                    StringComparison.OrdinalIgnoreCase))
            {
                normalized = Keep;
                reason = string.Empty;
                return true;
            }

            string candidate = value.Trim();
            SemanticBodyTargetKind requiredKind = rightArm
                ? SemanticBodyTargetKind.RightHand
                : SemanticBodyTargetKind.LeftHand;
            if (SemanticBodyTargetCatalogV0.TryGetExact(
                    candidate,
                    requiredKind,
                    out SemanticBodyTargetDefinition definition))
            {
                normalized = definition.Id;
                reason = string.Empty;
                return true;
            }

            normalized = Keep;
            reason = rightArm
                ? "conversation_right_hand_target_rejected"
                : "conversation_left_hand_target_rejected";
            return false;
        }

    }
}
