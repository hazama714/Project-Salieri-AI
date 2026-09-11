// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.SpatialTarget;

using UnityEngine;

namespace SalieriAI.Body.FreePose
{
    internal delegate bool FreePoseSubmitWorldPosition(
        HandTargetLease lease,
        Vector3 worldPosition,
        out HandTargetAuthoritySnapshot snapshot,
        out string reason);

    /// <summary>
    /// Executes only the arm portion of a resolved Free Pose definition.
    /// HeadIntent is intentionally ignored in Micro Stage 2C.
    /// </summary>
    public sealed class FreePoseArmExecutor : IDisposable
    {
        public const float DefaultTransitionDurationSeconds = 0.75f;

        private readonly SpatialTargetRegistry targetRegistry;
        private readonly HandTargetAuthority handTargetAuthority;
        private readonly FreePoseSubmitWorldPosition submitWorldPosition;
        private readonly float transitionDurationSeconds;

        private bool isActive;
        private string activePoseId = string.Empty;
        private bool ownsRight;
        private HandTargetLease rightLease;
        private bool ownsLeft;
        private HandTargetLease leftLease;

        private bool isTransitioning;
        private float transitionElapsedSeconds;
        private bool transitionRight;
        private Vector3 rightTransitionStart;
        private Vector3 rightTransitionDestination;
        private bool transitionLeft;
        private Vector3 leftTransitionStart;
        private Vector3 leftTransitionDestination;

        public bool IsActive => isActive;
        public string ActivePoseId => activePoseId;
        public bool OwnsRightArm => ownsRight;
        public bool OwnsLeftArm => ownsLeft;
        public bool IsTransitioning => isTransitioning;
        public bool IsInterpolationCompleted => isActive && !isTransitioning;
        public float TransitionDurationSeconds => transitionDurationSeconds;
        public float TransitionProgress => !isTransitioning
            ? (isActive ? 1f : 0f)
            : Mathf.Clamp01(
                transitionElapsedSeconds / transitionDurationSeconds);

        public FreePoseArmExecutor(
            SpatialTargetRegistry targetRegistry,
            HandTargetAuthority handTargetAuthority)
            : this(
                targetRegistry,
                handTargetAuthority,
                DefaultTransitionDurationSeconds)
        {
        }

        public FreePoseArmExecutor(
            SpatialTargetRegistry targetRegistry,
            HandTargetAuthority handTargetAuthority,
            float transitionDurationSeconds)
            : this(
                targetRegistry,
                handTargetAuthority,
                handTargetAuthority != null
                    ? handTargetAuthority.TrySubmitWorldPosition
                    : null,
                transitionDurationSeconds)
        {
        }

        internal FreePoseArmExecutor(
            SpatialTargetRegistry targetRegistry,
            HandTargetAuthority handTargetAuthority,
            FreePoseSubmitWorldPosition submitWorldPosition)
            : this(
                targetRegistry,
                handTargetAuthority,
                submitWorldPosition,
                0f)
        {
        }

        internal FreePoseArmExecutor(
            SpatialTargetRegistry targetRegistry,
            HandTargetAuthority handTargetAuthority,
            FreePoseSubmitWorldPosition submitWorldPosition,
            float transitionDurationSeconds)
        {
            this.targetRegistry = targetRegistry;
            this.handTargetAuthority = handTargetAuthority;
            this.submitWorldPosition = submitWorldPosition;
            this.transitionDurationSeconds =
                IsFiniteNonNegative(transitionDurationSeconds)
                    ? transitionDurationSeconds
                    : 0f;
        }

        internal bool TryPreflightPose(
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

            bool needsRight = !definition.RightArm.IsKeep;
            bool needsLeft = !definition.LeftArm.IsKeep;
            if (!needsRight && !needsLeft)
            {
                reason = string.Empty;
                return true;
            }

            if (targetRegistry == null ||
                handTargetAuthority == null ||
                submitWorldPosition == null)
            {
                reason = "free_pose_arm_dependency_missing";
                return false;
            }

            if (needsRight &&
                !FreePoseSpatialTargetLookup.TryResolve(
                    targetRegistry,
                    definition.RightArm,
                    SpatialTargetHandScope.RightOnly,
                    out _))
            {
                reason = "free_pose_right_target_lookup_failed";
                return false;
            }

            if (needsLeft &&
                !FreePoseSpatialTargetLookup.TryResolve(
                    targetRegistry,
                    definition.LeftArm,
                    SpatialTargetHandScope.LeftOnly,
                    out _))
            {
                reason = "free_pose_left_target_lookup_failed";
                return false;
            }

            if (needsRight &&
                !CanAcquireAfterOwnRelease(
                    HandTargetArm.Right,
                    ownsRight,
                    rightLease,
                    out reason))
            {
                return false;
            }

            if (needsLeft &&
                !CanAcquireAfterOwnRelease(
                    HandTargetArm.Left,
                    ownsLeft,
                    leftLease,
                    out reason))
            {
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryStartPose(
            FreePoseDefinition definition,
            out string reason)
        {
            if (isActive)
            {
                reason = "free_pose_arm_executor_already_active";
                return false;
            }

            if (definition == null)
            {
                reason = "free_pose_definition_missing";
                return false;
            }

            if (!definition.TryValidate(out reason))
            {
                if (string.IsNullOrWhiteSpace(reason))
                    reason = "free_pose_definition_invalid";
                return false;
            }

            bool needsRight = !definition.RightArm.IsKeep;
            bool needsLeft = !definition.LeftArm.IsKeep;

            // Both KEEP is an explicit harmless arm NoOp. No lease is
            // acquired and this arm-only executor does not become active.
            if (!needsRight && !needsLeft)
            {
                reason = string.Empty;
                return true;
            }

            if (targetRegistry == null ||
                handTargetAuthority == null ||
                submitWorldPosition == null)
            {
                reason = "free_pose_arm_dependency_missing";
                return false;
            }

            Transform rightTarget = null;
            Transform leftTarget = null;

            // Resolve every required target before changing Authority state.
            if (needsRight &&
                !FreePoseSpatialTargetLookup.TryResolve(
                    targetRegistry,
                    definition.RightArm,
                    SpatialTargetHandScope.RightOnly,
                    out rightTarget))
            {
                reason = "free_pose_right_target_lookup_failed";
                return false;
            }

            if (needsLeft &&
                !FreePoseSpatialTargetLookup.TryResolve(
                    targetRegistry,
                    definition.LeftArm,
                    SpatialTargetHandScope.LeftOnly,
                    out leftTarget))
            {
                reason = "free_pose_left_target_lookup_failed";
                return false;
            }

            Vector3 rightStart = handTargetAuthority
                .GetSnapshot(HandTargetArm.Right).AppliedWorldPosition;
            Vector3 leftStart = handTargetAuthority
                .GetSnapshot(HandTargetArm.Left).AppliedWorldPosition;

            HandTargetLease acquiredRight = default;
            HandTargetLease acquiredLeft = default;
            bool rightAcquired = false;
            bool leftAcquired = false;

            if (needsRight &&
                !handTargetAuthority.TryAcquire(
                    HandTargetArm.Right,
                    HandTargetOwnerKind.FreePose,
                    out acquiredRight,
                    out reason))
            {
                return false;
            }

            rightAcquired = needsRight;

            if (needsLeft &&
                !handTargetAuthority.TryAcquire(
                    HandTargetArm.Left,
                    HandTargetOwnerKind.FreePose,
                    out acquiredLeft,
                    out reason))
            {
                RollBackAcquired(
                    rightAcquired,
                    acquiredRight,
                    rightStart,
                    leftAcquired,
                    acquiredLeft,
                    leftStart);
                return false;
            }

            leftAcquired = needsLeft;

            isActive = true;
            activePoseId = definition.PoseId;
            ownsRight = rightAcquired;
            rightLease = acquiredRight;
            ownsLeft = leftAcquired;
            leftLease = acquiredLeft;

            BeginTransition(
                rightAcquired,
                rightStart,
                rightTarget != null ? rightTarget.position : Vector3.zero,
                leftAcquired,
                leftStart,
                leftTarget != null ? leftTarget.position : Vector3.zero);

            if (transitionDurationSeconds <= 0f &&
                !TryAdvanceTransition(1f, out reason))
            {
                RollBackAcquired(
                    rightAcquired,
                    acquiredRight,
                    rightStart,
                    leftAcquired,
                    acquiredLeft,
                    leftStart);
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Applies a new arm pose while retaining any still-required active
        /// Free Pose lease. A replacement always starts from the Authority's
        /// currently applied position rather than a previous requested target.
        /// </summary>
        public bool TryApplyPose(
            FreePoseDefinition definition,
            out string reason)
        {
            if (!isActive)
                return TryStartPose(definition, out reason);

            if (!TryPreflightPose(definition, out reason))
                return false;

            bool needsRight = !definition.RightArm.IsKeep;
            bool needsLeft = !definition.LeftArm.IsKeep;

            Transform rightTarget = null;
            Transform leftTarget = null;
            if (needsRight &&
                !FreePoseSpatialTargetLookup.TryResolve(
                    targetRegistry,
                    definition.RightArm,
                    SpatialTargetHandScope.RightOnly,
                    out rightTarget))
            {
                reason = "free_pose_right_target_lookup_failed";
                return false;
            }

            if (needsLeft &&
                !FreePoseSpatialTargetLookup.TryResolve(
                    targetRegistry,
                    definition.LeftArm,
                    SpatialTargetHandScope.LeftOnly,
                    out leftTarget))
            {
                reason = "free_pose_left_target_lookup_failed";
                return false;
            }

            Vector3 rightStart = handTargetAuthority
                .GetSnapshot(HandTargetArm.Right).AppliedWorldPosition;
            Vector3 leftStart = handTargetAuthority
                .GetSnapshot(HandTargetArm.Left).AppliedWorldPosition;

            HandTargetLease acquiredRight = default;
            HandTargetLease acquiredLeft = default;
            bool acquiredNewRight = false;
            bool acquiredNewLeft = false;

            if (needsRight && !ownsRight)
            {
                if (!handTargetAuthority.TryAcquire(
                        HandTargetArm.Right,
                        HandTargetOwnerKind.FreePose,
                        out acquiredRight,
                        out reason))
                {
                    return false;
                }

                acquiredNewRight = true;
            }

            if (needsLeft && !ownsLeft)
            {
                if (!handTargetAuthority.TryAcquire(
                        HandTargetArm.Left,
                        HandTargetOwnerKind.FreePose,
                        out acquiredLeft,
                        out reason))
                {
                    ReleaseNewLease(acquiredNewRight, acquiredRight);
                    return false;
                }

                acquiredNewLeft = true;
            }

            if (ownsRight && !needsRight &&
                !ReleaseIfStillOwned(
                    rightLease,
                    HandTargetReleasePolicy.Hold))
            {
                ReleaseNewLease(acquiredNewRight, acquiredRight);
                ReleaseNewLease(acquiredNewLeft, acquiredLeft);
                ReleasePose(out _);
                reason = "free_pose_right_release_ownership_lost";
                return false;
            }

            if (ownsLeft && !needsLeft &&
                !ReleaseIfStillOwned(
                    leftLease,
                    HandTargetReleasePolicy.Hold))
            {
                ReleaseNewLease(acquiredNewRight, acquiredRight);
                ReleaseNewLease(acquiredNewLeft, acquiredLeft);
                ReleasePose(out _);
                reason = "free_pose_left_release_ownership_lost";
                return false;
            }

            ownsRight = needsRight;
            if (acquiredNewRight)
                rightLease = acquiredRight;
            else if (!needsRight)
                rightLease = default;

            ownsLeft = needsLeft;
            if (acquiredNewLeft)
                leftLease = acquiredLeft;
            else if (!needsLeft)
                leftLease = default;

            if (!needsRight && !needsLeft)
            {
                ClearExecutionState();
                reason = string.Empty;
                return true;
            }

            isActive = true;
            activePoseId = definition.PoseId;
            BeginTransition(
                needsRight,
                rightStart,
                rightTarget != null ? rightTarget.position : Vector3.zero,
                needsLeft,
                leftStart,
                leftTarget != null ? leftTarget.position : Vector3.zero);

            if (transitionDurationSeconds <= 0f &&
                !TryAdvanceTransition(1f, out reason))
            {
                RollBackAcquired(
                    ownsRight,
                    rightLease,
                    rightStart,
                    ownsLeft,
                    leftLease,
                    leftStart);
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Advances only the software Hand Target interpolation. Completion
        /// does not imply that a physical servo has reached its destination.
        /// </summary>
        public bool Tick(float unscaledDeltaSeconds, out string reason)
        {
            if (!isActive || !isTransitioning)
            {
                reason = string.Empty;
                return true;
            }

            if (!IsFiniteNonNegative(unscaledDeltaSeconds))
            {
                reason = "free_pose_transition_delta_invalid";
                return false;
            }

            transitionElapsedSeconds = Mathf.Min(
                transitionDurationSeconds,
                transitionElapsedSeconds + unscaledDeltaSeconds);
            float normalized = transitionDurationSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(
                    transitionElapsedSeconds / transitionDurationSeconds);
            float smoothed = Mathf.SmoothStep(0f, 1f, normalized);
            if (TryAdvanceTransition(smoothed, out reason))
                return true;

            string failureReason = reason;
            ReleasePose(out _);
            reason = failureReason;
            return false;
        }

        public bool ReleasePose(out string reason)
        {
            if (!isActive)
            {
                reason = string.Empty;
                return true;
            }

            bool released = true;

            if (ownsRight)
            {
                released &= ReleaseIfStillOwned(
                    rightLease,
                    HandTargetReleasePolicy.Hold);
            }

            if (ownsLeft)
            {
                released &= ReleaseIfStillOwned(
                    leftLease,
                    HandTargetReleasePolicy.Hold);
            }

            ClearExecutionState();
            reason = released
                ? string.Empty
                : "free_pose_arm_release_ownership_lost";
            return released;
        }

        public void Dispose()
        {
            ReleasePose(out _);
        }

        private void BeginTransition(
            bool moveRight,
            Vector3 rightStart,
            Vector3 rightDestination,
            bool moveLeft,
            Vector3 leftStart,
            Vector3 leftDestination)
        {
            transitionElapsedSeconds = 0f;
            transitionRight = moveRight;
            rightTransitionStart = rightStart;
            rightTransitionDestination = rightDestination;
            transitionLeft = moveLeft;
            leftTransitionStart = leftStart;
            leftTransitionDestination = leftDestination;
            isTransitioning = moveRight || moveLeft;
        }

        private bool TryAdvanceTransition(
            float smoothedProgress,
            out string reason)
        {
            if (transitionRight &&
                !handTargetAuthority.IsLeaseActive(rightLease))
            {
                reason = "free_pose_right_transition_ownership_lost";
                return false;
            }

            if (transitionLeft &&
                !handTargetAuthority.IsLeaseActive(leftLease))
            {
                reason = "free_pose_left_transition_ownership_lost";
                return false;
            }

            if (transitionRight)
            {
                Vector3 position = Vector3.LerpUnclamped(
                    rightTransitionStart,
                    rightTransitionDestination,
                    smoothedProgress);
                if (!submitWorldPosition(
                        rightLease,
                        position,
                        out _,
                        out reason))
                {
                    if (string.IsNullOrWhiteSpace(reason))
                        reason = "free_pose_right_transition_submit_failed";
                    return false;
                }
            }

            if (transitionLeft)
            {
                Vector3 position = Vector3.LerpUnclamped(
                    leftTransitionStart,
                    leftTransitionDestination,
                    smoothedProgress);
                if (!submitWorldPosition(
                        leftLease,
                        position,
                        out _,
                        out reason))
                {
                    if (string.IsNullOrWhiteSpace(reason))
                        reason = "free_pose_left_transition_submit_failed";
                    return false;
                }
            }

            if (smoothedProgress >= 1f)
                ClearTransitionState();

            reason = string.Empty;
            return true;
        }

        private void ReleaseNewLease(
            bool acquired,
            HandTargetLease lease)
        {
            if (!acquired || !handTargetAuthority.IsLeaseActive(lease))
                return;

            handTargetAuthority.TryRelease(
                lease,
                HandTargetReleasePolicy.Hold,
                out _,
                out _);
        }

        private void RollBackAcquired(
            bool rightAcquired,
            HandTargetLease acquiredRight,
            Vector3 rightStart,
            bool leftAcquired,
            HandTargetLease acquiredLeft,
            Vector3 leftStart)
        {
            RestoreAndReleaseIfStillOwned(
                rightAcquired,
                acquiredRight,
                rightStart);
            RestoreAndReleaseIfStillOwned(
                leftAcquired,
                acquiredLeft,
                leftStart);
            ClearExecutionState();
        }

        private void RestoreAndReleaseIfStillOwned(
            bool acquired,
            HandTargetLease lease,
            Vector3 startPosition)
        {
            if (!acquired ||
                !handTargetAuthority.IsLeaseActive(lease))
            {
                return;
            }

            submitWorldPosition(
                lease,
                startPosition,
                out _,
                out _);
            handTargetAuthority.TryRelease(
                lease,
                HandTargetReleasePolicy.Hold,
                out _,
                out _);
        }

        private bool ReleaseIfStillOwned(
            HandTargetLease lease,
            HandTargetReleasePolicy policy)
        {
            if (!handTargetAuthority.IsLeaseActive(lease))
                return false;

            return handTargetAuthority.TryRelease(
                lease,
                policy,
                out _,
                out _);
        }

        private bool CanAcquireAfterOwnRelease(
            HandTargetArm arm,
            bool ownsArm,
            HandTargetLease ownLease,
            out string reason)
        {
            HandTargetAuthoritySnapshot snapshot =
                handTargetAuthority.GetSnapshot(arm);
            if (!snapshot.TargetAvailable)
            {
                reason = arm + " HandTarget is not available.";
                return false;
            }

            if (!snapshot.HasOwner)
            {
                reason = string.Empty;
                return true;
            }

            if (ownsArm && handTargetAuthority.IsLeaseActive(ownLease))
            {
                reason = string.Empty;
                return true;
            }

            reason = arm + " HandTarget is busy. owner=" + snapshot.Owner;
            return false;
        }

        private void ClearExecutionState()
        {
            ClearTransitionState();
            isActive = false;
            activePoseId = string.Empty;
            ownsRight = false;
            rightLease = default;
            ownsLeft = false;
            leftLease = default;
        }

        private void ClearTransitionState()
        {
            isTransitioning = false;
            transitionElapsedSeconds = 0f;
            transitionRight = false;
            rightTransitionStart = Vector3.zero;
            rightTransitionDestination = Vector3.zero;
            transitionLeft = false;
            leftTransitionStart = Vector3.zero;
            leftTransitionDestination = Vector3.zero;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f &&
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
