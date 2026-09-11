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
    /// Coordinates the existing arm and head Free Pose executors without
    /// adding pose lookup, body solving, or scene ownership.
    /// </summary>
    public sealed class FreePoseExecutor : IDisposable
    {
        private readonly FreePoseArmExecutor armExecutor;
        private readonly FreePoseHeadAdapter headAdapter;

        private string activePoseId = string.Empty;
        private FreePoseDefinition activeDefinition;

        public bool IsActive =>
            (armExecutor != null && armExecutor.IsActive) ||
            (headAdapter != null && headAdapter.HasOwnRequest);

        public string ActivePoseId => IsActive
            ? activePoseId
            : string.Empty;
        public FreePoseDefinition ActiveDefinition => IsActive
            ? activeDefinition
            : null;

        public bool IsArmTransitioning =>
            armExecutor != null && armExecutor.IsTransitioning;

        public bool IsArmInterpolationCompleted =>
            armExecutor == null || armExecutor.IsInterpolationCompleted;

        public FreePoseExecutor(
            FreePoseArmExecutor armExecutor,
            FreePoseHeadAdapter headAdapter)
        {
            this.armExecutor = armExecutor;
            this.headAdapter = headAdapter;
        }

        public bool TryStartPose(
            FreePoseDefinition definition,
            out string reason)
        {
            if (IsActive)
            {
                reason = "free_pose_executor_already_active";
                return false;
            }

            if (definition == null)
            {
                reason = "free_pose_definition_missing";
                return false;
            }

            if (!definition.TryValidate(out reason))
                return false;

            bool needsArm =
                !definition.RightArm.IsKeep ||
                !definition.LeftArm.IsKeep;
            bool needsHead =
                definition.HeadIntent != FreePoseHeadIntent.Keep;

            if (!needsArm && !needsHead)
            {
                activePoseId = string.Empty;
                activeDefinition = null;
                reason = string.Empty;
                return true;
            }

            // Validate required dependencies before either subsystem gains
            // ownership, so a missing Head adapter cannot partially start Arm.
            if (needsArm && armExecutor == null)
            {
                reason = "free_pose_arm_executor_missing";
                return false;
            }

            if (needsHead && headAdapter == null)
            {
                reason = "free_pose_head_adapter_missing";
                return false;
            }

            try
            {
                if (needsArm &&
                    !armExecutor.TryStartPose(definition, out reason))
                {
                    return false;
                }

                if (needsHead &&
                    !headAdapter.TryApply(definition.HeadIntent, out reason))
                {
                    CleanUpFailedStart();
                    return false;
                }

                if (IsActive)
                {
                    activePoseId = definition.PoseId;
                    activeDefinition = definition;
                }

                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                CleanUpFailedStart();
                reason = "free_pose_start_exception:" +
                    exception.GetType().Name;
                return false;
            }
        }

        /// <summary>
        /// Production-compatible entry point. An inactive executor starts the
        /// pose normally; an active executor validates the replacement before
        /// releasing only its own current resources and starting the new pose.
        /// </summary>
        public bool TryApplyPose(
            FreePoseDefinition definition,
            out string reason)
        {
            if (!TryPreflightPose(definition, out reason))
                return false;

            if (!IsActive)
                return TryStartPose(definition, out reason);

            bool needsArm =
                !definition.RightArm.IsKeep ||
                !definition.LeftArm.IsKeep;
            bool needsHead =
                definition.HeadIntent != FreePoseHeadIntent.Keep;

            try
            {
                if (armExecutor != null &&
                    (armExecutor.IsActive || needsArm) &&
                    !armExecutor.TryApplyPose(definition, out reason))
                {
                    return false;
                }

                if (headAdapter != null &&
                    (headAdapter.HasOwnRequest || needsHead) &&
                    !headAdapter.TryApply(definition.HeadIntent, out reason))
                {
                    CleanUpFailedStart();
                    return false;
                }

                activePoseId = IsActive
                    ? definition.PoseId
                    : string.Empty;
                activeDefinition = IsActive ? definition : null;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                CleanUpFailedStart();
                reason = "free_pose_replace_exception:" +
                    exception.GetType().Name;
                return false;
            }
        }

        /// <summary>
        /// Advances the arm software interpolation. Head intent remains owned
        /// by the existing orientation request service and needs no frame tick.
        /// </summary>
        public bool Tick(float unscaledDeltaSeconds, out string reason)
        {
            if (armExecutor == null)
            {
                reason = string.Empty;
                return true;
            }

            return armExecutor.Tick(unscaledDeltaSeconds, out reason);
        }

        public bool ReleasePose(out string reason)
        {
            // End the semantic head override before releasing the held arm
            // target. Neither release forces Neutral or restores a pose.
            bool headReleased = ReleaseHeadSafely();
            bool armReleased = ReleaseArmSafely();

            activePoseId = string.Empty;
            activeDefinition = null;

            if (headReleased && armReleased)
            {
                reason = string.Empty;
                return true;
            }

            reason = "free_pose_release_incomplete";
            return false;
        }

        public void Dispose()
        {
            ReleasePose(out _);
        }

        private void CleanUpFailedStart()
        {
            ReleaseHeadSafely();
            ReleaseArmSafely();

            activePoseId = string.Empty;
            activeDefinition = null;
        }

        private bool TryPreflightPose(
            FreePoseDefinition definition,
            out string reason)
        {
            if (definition == null)
            {
                reason = "free_pose_definition_missing";
                return false;
            }

            if (!definition.TryValidate(out reason))
                return false;

            bool needsArm =
                !definition.RightArm.IsKeep ||
                !definition.LeftArm.IsKeep;
            bool needsHead =
                definition.HeadIntent != FreePoseHeadIntent.Keep;

            try
            {
                if (needsArm)
                {
                    if (armExecutor == null)
                    {
                        reason = "free_pose_arm_executor_missing";
                        return false;
                    }

                    if (!armExecutor.TryPreflightPose(
                            definition,
                            out reason))
                    {
                        return false;
                    }
                }

                if (needsHead)
                {
                    if (headAdapter == null)
                    {
                        reason = "free_pose_head_adapter_missing";
                        return false;
                    }

                    if (!headAdapter.TryPreflight(
                            definition.HeadIntent,
                            out reason))
                    {
                        return false;
                    }
                }
            }
            catch (Exception exception)
            {
                reason = "free_pose_preflight_exception:" +
                    exception.GetType().Name;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool ReleaseHeadSafely()
        {
            if (headAdapter == null)
                return true;

            try
            {
                return headAdapter.Release(out _);
            }
            catch
            {
                return false;
            }
        }

        private bool ReleaseArmSafely()
        {
            if (armExecutor == null)
                return true;

            try
            {
                return armExecutor.ReleasePose(out _);
            }
            catch
            {
                return false;
            }
        }
    }
}
