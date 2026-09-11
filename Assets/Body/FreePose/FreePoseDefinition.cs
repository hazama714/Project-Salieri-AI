// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Body.FreePose
{
    public enum FreePoseArmDirectiveKind
    {
        Keep = 0,
        SpatialTarget = 1
    }

    /// <summary>
    /// A pure arm directive. Keep means that a future executor must not
    /// acquire, submit, or release an authority lease for this arm.
    /// SpatialTargetId is an exact registry key, not a scene reference.
    /// </summary>
    public readonly struct FreePoseArmDirective
    {
        public FreePoseArmDirectiveKind Kind { get; }
        public string SpatialTargetId { get; }

        public bool IsKeep => Kind == FreePoseArmDirectiveKind.Keep;

        private FreePoseArmDirective(
            FreePoseArmDirectiveKind kind,
            string spatialTargetId)
        {
            Kind = kind;
            SpatialTargetId = spatialTargetId;
        }

        public static FreePoseArmDirective Keep()
        {
            return new FreePoseArmDirective(
                FreePoseArmDirectiveKind.Keep,
                null);
        }

        public static FreePoseArmDirective ForSpatialTarget(
            string spatialTargetId)
        {
            return new FreePoseArmDirective(
                FreePoseArmDirectiveKind.SpatialTarget,
                spatialTargetId);
        }
    }

    public enum FreePoseHeadIntent
    {
        /// <summary>
        /// Do not submit or release an orientation request.
        /// Existing LOOK / Orientation ownership remains untouched.
        /// </summary>
        Keep = 0,

        Front = 1,
        ConversationPartner = 2,
        CurrentObject = 3
    }

    /// <summary>
    /// Immutable Free Pose v0 data contract.
    /// It contains semantic IDs only and performs no scene or registry lookup.
    /// </summary>
    public sealed class FreePoseDefinition
    {
        public string PoseId { get; }
        public FreePoseArmDirective RightArm { get; }
        public FreePoseArmDirective LeftArm { get; }
        public FreePoseHeadIntent HeadIntent { get; }

        public FreePoseDefinition(
            string poseId,
            FreePoseArmDirective rightArm,
            FreePoseArmDirective leftArm,
            FreePoseHeadIntent headIntent)
        {
            PoseId = poseId;
            RightArm = rightArm;
            LeftArm = leftArm;
            HeadIntent = headIntent;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(PoseId))
            {
                error = "pose_id_missing";
                return false;
            }

            if (!TryValidateArm(RightArm, "right_arm", out error))
                return false;

            if (!TryValidateArm(LeftArm, "left_arm", out error))
                return false;

            switch (HeadIntent)
            {
                case FreePoseHeadIntent.Keep:
                case FreePoseHeadIntent.Front:
                case FreePoseHeadIntent.ConversationPartner:
                case FreePoseHeadIntent.CurrentObject:
                    error = string.Empty;
                    return true;

                default:
                    error = "head_intent_unsupported";
                    return false;
            }
        }

        private static bool TryValidateArm(
            FreePoseArmDirective directive,
            string armName,
            out string error)
        {
            switch (directive.Kind)
            {
                case FreePoseArmDirectiveKind.Keep:
                    error = string.Empty;
                    return true;

                case FreePoseArmDirectiveKind.SpatialTarget:
                    if (!string.IsNullOrWhiteSpace(
                            directive.SpatialTargetId))
                    {
                        error = string.Empty;
                        return true;
                    }

                    error = armName + "_spatial_target_id_missing";
                    return false;

                default:
                    error = armName + "_directive_unsupported";
                    return false;
            }
        }
    }
}
