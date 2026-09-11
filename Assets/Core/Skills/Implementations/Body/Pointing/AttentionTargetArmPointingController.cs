// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.IK;
using SalieriAI.Body.SpatialTarget;
using SalieriAI.Core.Perception.Attention;

using UnityEngine;

namespace SalieriAI.Core.Skills.Body.Pointing
{
    public interface IAttentionTargetPointingController
    {
        bool IsPointing { get; }
        string ActiveArmId { get; }
        string LastFailureReason { get; }
        bool BeginPointing();
        void StopPointing(bool restoreTargets);
    }

    public interface ICapturedAttentionTargetPointingController :
        IAttentionTargetPointingController
    {
        bool IsCapturedAttentionPointing { get; }
        string CapturedAttentionTargetKey { get; }
        bool TryBeginCapturedAttentionPointing(
            HandTargetArm arm,
            out string capturedTargetKey);
    }

    /// <summary>
    /// 共有AttentionTargetの方向へ、既存VRMArmTargetIKControllerが読む
    /// HandTargetを移動する。本クラスはAnimator boneや実機Servoへ書き込まない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttentionTargetArmPointingController :
        MonoBehaviour,
        ICapturedAttentionTargetPointingController
    {
        public const float HandTargetSettleToleranceMeters = 0.005f;

        public enum ArmSelection
        {
            Auto = 0,
            Right = 1,
            Left = 2
        }

        [Header("Existing References")]
        [SerializeField] private Animator avatarAnimator;
        [SerializeField] private Transform attentionTarget;
        [SerializeField] private HandTargetAuthority handTargetAuthority;

        [Header("Pointing")]
        [SerializeField] private ArmSelection armSelection = ArmSelection.Auto;
        [SerializeField, Range(0.20f, 0.95f)] private float reachRatio = 0.78f;
        [SerializeField, Min(0.001f)] private float targetMoveSpeed = 0.35f;
        [SerializeField] private float verticalOffset;
        [SerializeField, Min(0f)] private float outwardOffset = 0.015f;
        [SerializeField, Range(-1f, 1f)] private float minimumForwardDot = 0.05f;
        [SerializeField] private bool restoreTargetsOnDisable = true;

        private bool isPointing;
        private ArmSelection activeArm;
        private HandTargetLease activeLease;
        private bool hasActiveLease;
        private string lastFailureReason = string.Empty;
        private bool hasAcceptedPointTarget;
        private Vector3 acceptedAttentionTargetPosition;
        private Vector3 lastDesiredHandTargetPosition;
        private int pointingUpdateCount;
        private int stopCount;
        private bool retainAcceptedPointPose;
        private RobotArmIKSolver activeArmIkSolver;
        private int pointRequestStartedFrame = -1;
        private OrientationResolutionTargetDriver capturedTargetDriver;
        private bool isCapturedAttentionPointing;
        private string capturedAttentionTargetKey = string.Empty;
        private Vector3 capturedAttentionTargetPosition;

        public bool IsPointing => isPointing;
        public string ActiveArmId => activeArm.ToString();
        public string LastFailureReason => lastFailureReason;
        public bool HasAcceptedPointTarget => hasAcceptedPointTarget;
        public Vector3 AcceptedAttentionTargetPosition =>
            acceptedAttentionTargetPosition;
        public Vector3 LastDesiredHandTargetPosition =>
            lastDesiredHandTargetPosition;
        public int PointingUpdateCount => pointingUpdateCount;
        public int StopCount => stopCount;
        public bool IsRetainingAcceptedPointPose => retainAcceptedPointPose;
        public bool IsCapturedAttentionPointing =>
            isCapturedAttentionPointing;
        public string CapturedAttentionTargetKey =>
            capturedAttentionTargetKey;

        private void OnEnable()
        {
            ConversationalAttentionPointRuntime.TryRegister(this);
        }

        private void LateUpdate()
        {
            if (isPointing && !UpdatePointingTarget(Time.deltaTime))
            {
                FailAndRelease(
                    lastFailureReason,
                    HandTargetReleasePolicy.Hold);
            }
        }

        private void OnDisable()
        {
            StopPointing(restoreTargetsOnDisable);
            ConversationalAttentionPointRuntime.Unregister(this);
        }

        public bool BeginPointing()
        {
            if (isCapturedAttentionPointing)
                StopPointing(false);

            return BeginPointingInternal(null);
        }

        public bool TryBeginCapturedAttentionPointing(
            HandTargetArm arm,
            out string capturedTargetKey)
        {
            capturedTargetKey = string.Empty;
            if (isPointing)
            {
                Fail("Another POINT request is already active.");
                return false;
            }

            capturedTargetDriver = ResolveTargetDriver();
            if (capturedTargetDriver == null ||
                !capturedTargetDriver.isActiveAndEnabled ||
                !capturedTargetDriver.HasOutputPosition ||
                string.IsNullOrWhiteSpace(
                    capturedTargetDriver.OutputTargetKey) ||
                !IsPointableAttentionKind(
                    capturedTargetDriver.OutputKind))
            {
                ClearCapturedAttentionState();
                Fail("No valid current AttentionTarget is available.");
                return false;
            }

            isCapturedAttentionPointing = true;
            capturedAttentionTargetKey =
                capturedTargetDriver.OutputTargetKey;
            capturedAttentionTargetPosition =
                capturedTargetDriver.OutputWorldPosition;

            ArmSelection requestedArm = arm == HandTargetArm.Left
                ? ArmSelection.Left
                : ArmSelection.Right;
            if (!BeginPointingInternal(requestedArm))
            {
                ClearCapturedAttentionState();
                return false;
            }

            capturedTargetKey = capturedAttentionTargetKey;
            return true;
        }

        private bool BeginPointingInternal(
            ArmSelection? requestedArm)
        {
            if (isPointing &&
                hasActiveLease &&
                handTargetAuthority != null &&
                handTargetAuthority.IsLeaseActive(activeLease))
            {
                return true;
            }

            if (!ValidateReferences())
                return false;

            activeArm = requestedArm ?? ResolveArm();
            HandTargetArm authorityArm = ToAuthorityArm(activeArm);
            if (!handTargetAuthority.GetSnapshot(authorityArm).TargetAvailable)
            {
                Fail("The selected existing HandTarget is not assigned.");
                return false;
            }

            if (!TryCalculateTarget(
                    activeArm,
                    out Vector3 desired,
                    out string error))
            {
                Fail(error);
                return false;
            }

            if (!handTargetAuthority.TryAcquire(
                    authorityArm,
                    HandTargetOwnerKind.ProductionPoint,
                    out activeLease,
                    out string acquireError))
            {
                Fail(acquireError);
                return false;
            }

            hasActiveLease = true;

            lastFailureReason = string.Empty;
            retainAcceptedPointPose = false;
            acceptedAttentionTargetPosition =
                ResolvePointTargetPosition();
            lastDesiredHandTargetPosition = desired;
            hasAcceptedPointTarget = true;
            pointingUpdateCount = 0;
            pointRequestStartedFrame = Time.frameCount;
            activeArmIkSolver = FindActiveArmIkSolver(activeArm);
            isPointing = true;
            if (!UpdatePointingTarget(0f))
            {
                FailAndRelease(
                    lastFailureReason,
                    HandTargetReleasePolicy.RestoreBaseline);
                return false;
            }

            return true;
        }

        public void StopPointing(bool restoreTargets)
        {
            if (isPointing)
                stopCount++;

            ReleaseActiveLease(
                restoreTargets
                    ? HandTargetReleasePolicy.RestoreBaseline
                    : HandTargetReleasePolicy.Hold);
            isPointing = false;
            retainAcceptedPointPose = false;
            activeArm = ArmSelection.Auto;
            activeArmIkSolver = null;
            pointRequestStartedFrame = -1;
            ClearCapturedAttentionState();

        }

        /// <summary>
        /// WAIT_ANSWER中はBeginPointing時にfreezeしたQuestion target位置を維持する。
        /// 新しいPOINT requestやphysical completionは生成しない。
        /// </summary>
        public bool RetainAcceptedPointPose()
        {
            if (!isPointing ||
                !hasAcceptedPointTarget ||
                !hasActiveLease ||
                handTargetAuthority == null ||
                !handTargetAuthority.IsLeaseActive(activeLease))
                return false;

            retainAcceptedPointPose = true;
            return true;
        }

        public PointSoftwareSettleObservation
            GetSoftwareSettleObservation()
        {
            bool armResolved =
                activeArm == ArmSelection.Right ||
                activeArm == ArmSelection.Left;
            HandTargetAuthoritySnapshot snapshot = armResolved &&
                handTargetAuthority != null
                    ? handTargetAuthority.GetSnapshot(
                        ToAuthorityArm(activeArm))
                    : default;

            bool targetValid =
                isPointing &&
                hasAcceptedPointTarget &&
                attentionTarget != null &&
                hasActiveLease &&
                handTargetAuthority != null &&
                handTargetAuthority.IsLeaseActive(activeLease) &&
                snapshot.TargetAvailable;

            float handTargetError = targetValid
                ? Vector3.Distance(
                    snapshot.AppliedWorldPosition,
                    lastDesiredHandTargetPosition)
                : float.PositiveInfinity;

            RobotArmIKSolver solver = activeArmIkSolver;
            bool solverAvailable =
                solver != null &&
                solver.isActiveAndEnabled &&
                solver.IsContinuousSolveEnabled;
            bool ikFresh =
                solverAvailable &&
                solver.LastSolveFrame > pointRequestStartedFrame;
            bool ikValid =
                solverAvailable &&
                solver.LastSolveResultValid;
            bool ikReached =
                solverAvailable &&
                solver.LastSolveReachedTarget;

            string reason;
            if (!targetValid)
                reason = "Point target is no longer valid.";
            else if (!solverAvailable)
                reason = "Active arm IK solver is unavailable.";
            else if (!ikFresh)
                reason = "Waiting for a post-POINT IK result.";
            else if (!ikValid)
                reason = "Active arm IK result is invalid.";
            else if (handTargetError > HandTargetSettleToleranceMeters)
                reason = "HandTarget is still moving.";
            else if (!ikReached)
                reason = "Virtual arm IK has not reached its target.";
            else
                reason = string.Empty;

            return new PointSoftwareSettleObservation(
                targetValid,
                ikValid,
                ikFresh,
                ikReached,
                handTargetError,
                HandTargetSettleToleranceMeters,
                solverAvailable
                    ? solver.LastDistance
                    : float.PositiveInfinity,
                solverAvailable
                    ? solver.PositionTolerance
                    : 0f,
                reason);
        }

        private bool UpdatePointingTarget(float deltaTime)
        {
            if (!TryCalculateTarget(
                    activeArm,
                    out Vector3 desired,
                    out string error))
            {
                Fail(error);
                return false;
            }

            if (!hasActiveLease ||
                handTargetAuthority == null ||
                !handTargetAuthority.IsLeaseActive(activeLease))
            {
                Fail("The active HandTarget lease is unavailable.");
                return false;
            }

            HandTargetAuthoritySnapshot current =
                handTargetAuthority.GetSnapshot(activeLease.Arm);
            lastDesiredHandTargetPosition = desired;
            Vector3 next = Vector3.MoveTowards(
                current.AppliedWorldPosition,
                desired,
                targetMoveSpeed * Mathf.Max(0f, deltaTime));

            if (!handTargetAuthority.TrySubmitWorldPosition(
                    activeLease,
                    next,
                    out _,
                    out string submitError))
            {
                Fail(submitError);
                return false;
            }

            pointingUpdateCount++;
            return true;
        }

        private ArmSelection ResolveArm()
        {
            if (armSelection != ArmSelection.Auto)
                return armSelection;

            Vector3 local = avatarAnimator.transform
                .InverseTransformPoint(attentionTarget.position);
            ArmSelection preferred = local.x >= 0f
                ? ArmSelection.Right
                : ArmSelection.Left;

            if (preferred == ArmSelection.Right &&
                !handTargetAuthority.GetSnapshot(
                    HandTargetArm.Right).TargetAvailable)
                return ArmSelection.Left;
            if (preferred == ArmSelection.Left &&
                !handTargetAuthority.GetSnapshot(
                    HandTargetArm.Left).TargetAvailable)
                return ArmSelection.Right;
            return preferred;
        }

        private static RobotArmIKSolver FindActiveArmIkSolver(
            ArmSelection selectedArm)
        {
            string expectedLabel = selectedArm.ToString();
            RobotArmIKSolver match = null;
            RobotArmIKSolver[] solvers =
                UnityEngine.Object.FindObjectsByType<RobotArmIKSolver>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0; i < solvers.Length; i++)
            {
                RobotArmIKSolver candidate = solvers[i];
                if (candidate == null ||
                    !candidate.isActiveAndEnabled ||
                    !string.Equals(
                        candidate.ArmLabel,
                        expectedLabel,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (match != null)
                    return null;

                match = candidate;
            }

            return match;
        }

        private bool TryCalculateTarget(
            ArmSelection selectedArm,
            out Vector3 desired,
            out string error)
        {
            desired = Vector3.zero;
            error = string.Empty;

            if (selectedArm != ArmSelection.Right &&
                selectedArm != ArmSelection.Left)
            {
                error = "Pointing arm is not resolved.";
                return false;
            }

            bool right = selectedArm == ArmSelection.Right;
            Transform upper = avatarAnimator.GetBoneTransform(
                right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            Transform lower = avatarAnimator.GetBoneTransform(
                right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            Transform hand = avatarAnimator.GetBoneTransform(
                right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);

            if (upper == null || lower == null || hand == null)
            {
                error = "Required Humanoid arm bones are missing.";
                return false;
            }

            Vector3 targetPosition = retainAcceptedPointPose
                ? acceptedAttentionTargetPosition
                : ResolvePointTargetPosition();
            Vector3 toAttention = targetPosition - upper.position;
            if (toAttention.sqrMagnitude < 0.000001f)
            {
                error = "AttentionTarget is too close to the shoulder.";
                return false;
            }

            Vector3 direction = toAttention.normalized;
            float forwardDot = Vector3.Dot(
                avatarAnimator.transform.forward,
                direction);
            if (forwardDot < minimumForwardDot)
            {
                error = "AttentionTarget is outside the permitted forward area.";
                return false;
            }

            float armLength = Vector3.Distance(upper.position, lower.position) +
                Vector3.Distance(lower.position, hand.position);
            if (armLength <= 0.0001f)
            {
                error = "Estimated arm length is invalid.";
                return false;
            }

            Vector3 outward = right
                ? avatarAnimator.transform.right
                : -avatarAnimator.transform.right;
            desired = upper.position +
                direction * armLength * reachRatio +
                Vector3.up * verticalOffset +
                outward * outwardOffset;
            return true;
        }

        private bool ValidateReferences()
        {
            if (avatarAnimator == null || !avatarAnimator.isHuman)
            {
                Fail("A Humanoid Animator is required.");
                return false;
            }

            if (attentionTarget == null)
            {
                Fail("The shared AttentionTarget is not assigned.");
                return false;
            }

            if (handTargetAuthority == null)
            {
                Fail("HandTargetAuthority is required.");
                return false;
            }

            if (!handTargetAuthority.GetSnapshot(
                    HandTargetArm.Right).TargetAvailable &&
                !handTargetAuthority.GetSnapshot(
                    HandTargetArm.Left).TargetAvailable)
            {
                Fail("At least one existing HandTarget is required.");
                return false;
            }

            return true;
        }

        private void ReleaseActiveLease(HandTargetReleasePolicy policy)
        {
            if (hasActiveLease && handTargetAuthority != null)
            {
                handTargetAuthority.TryRelease(
                    activeLease,
                    policy,
                    out _,
                    out _);
            }

            hasActiveLease = false;
            activeLease = default;
        }

        private void FailAndRelease(
            string reason,
            HandTargetReleasePolicy policy)
        {
            ReleaseActiveLease(policy);
            Fail(reason);
        }

        private static HandTargetArm ToAuthorityArm(ArmSelection arm)
        {
            return arm == ArmSelection.Left
                ? HandTargetArm.Left
                : HandTargetArm.Right;
        }

        private void Fail(string reason)
        {
            isPointing = false;
            ClearCapturedAttentionState();
            lastFailureReason = string.IsNullOrWhiteSpace(reason)
                ? "pointing_failed"
                : reason;
        }

        private Vector3 ResolvePointTargetPosition()
        {
            if (!isCapturedAttentionPointing)
                return attentionTarget.position;

            if (capturedTargetDriver != null &&
                capturedTargetDriver.isActiveAndEnabled &&
                capturedTargetDriver.HasOutputPosition &&
                IsPointableAttentionKind(
                    capturedTargetDriver.OutputKind) &&
                string.Equals(
                    capturedTargetDriver.OutputTargetKey,
                    capturedAttentionTargetKey,
                    StringComparison.Ordinal))
            {
                capturedAttentionTargetPosition =
                    capturedTargetDriver.OutputWorldPosition;
            }

            // If live arbitration switches or loses the target, retain the
            // last position of the captured identity. Never follow a new key.
            return capturedAttentionTargetPosition;
        }

        private static bool IsPointableAttentionKind(
            OrientationResolutionKind kind)
        {
            return kind == OrientationResolutionKind.Face ||
                kind == OrientationResolutionKind.Object ||
                kind == OrientationResolutionKind.HoldPrevious ||
                kind == OrientationResolutionKind.AttentionContinuityHold;
        }

        private OrientationResolutionTargetDriver ResolveTargetDriver()
        {
            if (capturedTargetDriver != null &&
                capturedTargetDriver.isActiveAndEnabled)
            {
                return capturedTargetDriver;
            }

            return UnityEngine.Object
                .FindObjectOfType<OrientationResolutionTargetDriver>();
        }

        private void ClearCapturedAttentionState()
        {
            isCapturedAttentionPointing = false;
            capturedTargetDriver = null;
            capturedAttentionTargetKey = string.Empty;
            capturedAttentionTargetPosition = Vector3.zero;
        }
    }
}
