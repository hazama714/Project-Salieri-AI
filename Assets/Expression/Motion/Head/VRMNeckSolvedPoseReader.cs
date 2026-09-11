// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Perception.Attention;

using UnityEngine;

namespace SalieriAI.Expression.Motion
{
    /// <summary>
    /// Reads the VRM Humanoid neck/head pose after Animator LookAt has solved.
    /// It is observation-only and never writes VRM bones or physical output.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class VRMNeckSolvedPoseReader : MonoBehaviour
    {
        [Header("Solved Pose Source")]
        [SerializeField] private Animator animator;
        [SerializeField]
        private OrientationResolutionTargetDriver targetDriver;

        [Header("Runtime State (Read Only)")]
        [SerializeField]
        private VirtualNeckPoseState currentPose =
            new VirtualNeckPoseState();
        [SerializeField] private bool neutralCaptured;
        [SerializeField] private Quaternion neutralNeckLocalRotation =
            Quaternion.identity;
        [SerializeField] private Quaternion neutralHeadLocalRotation =
            Quaternion.identity;
        [SerializeField] private Quaternion neutralHeadReferenceRotation =
            Quaternion.identity;
        [SerializeField]
        private Quaternion currentCombinedHeadNeutralRelativeRotation =
            Quaternion.identity;
        [SerializeField]
        private Vector3 currentHeadForwardInBodyFrame = Vector3.forward;

        private Transform neckBone;
        private Transform headBone;
        private Transform leftEyeBone;
        private Transform rightEyeBone;
        private Transform headReferenceFrame;

        public VirtualNeckPoseState CurrentPose => currentPose;
        public bool NeutralCaptured => neutralCaptured;
        public Quaternion NeutralNeckLocalRotation =>
            neutralNeckLocalRotation;
        public Quaternion NeutralHeadLocalRotation =>
            neutralHeadLocalRotation;
        public Quaternion NeutralHeadReferenceRotation =>
            neutralHeadReferenceRotation;
        public Quaternion CombinedHeadNeutralRelativeRotation =>
            currentCombinedHeadNeutralRelativeRotation;
        public Vector3 HeadForwardInBodyFrame =>
            currentHeadForwardInBodyFrame;
        public OrientationResolutionKind CurrentResolutionKind =>
            targetDriver != null
                ? targetDriver.OutputKind
                : OrientationResolutionKind.None;

        private void Awake()
        {
            CacheBones();
            CaptureNeutralPose();
        }

        private void OnEnable()
        {
            if (!neutralCaptured)
            {
                CacheBones();
                CaptureNeutralPose();
            }
        }

        private void LateUpdate()
        {
            CaptureSolvedPose(DateTime.UtcNow, Time.frameCount);
        }

        [ContextMenu("Recapture VRM Neck Neutral Pose")]
        public void CaptureNeutralPose()
        {
            if (neckBone == null || headBone == null)
                CacheBones();

            if (neckBone == null || headBone == null)
            {
                neutralCaptured = false;
                return;
            }

            headReferenceFrame = neckBone.parent != null
                ? neckBone.parent
                : animator != null ? animator.transform : transform;

            neutralNeckLocalRotation = neckBone.localRotation;
            neutralHeadLocalRotation = headBone.localRotation;
            neutralHeadReferenceRotation =
                Quaternion.Inverse(headReferenceFrame.rotation) *
                headBone.rotation;
            neutralCaptured = true;
        }

        public void CaptureSolvedPose(DateTime capturedAtUtc, int frame)
        {
            if (currentPose == null)
                currentPose = new VirtualNeckPoseState();

            string targetKey = targetDriver != null
                ? targetDriver.OutputTargetKey
                : string.Empty;

            if (!neutralCaptured || neckBone == null || headBone == null)
            {
                CacheBones();
                if (!neutralCaptured)
                    CaptureNeutralPose();
            }

            bool hasNeck = neckBone != null;
            bool hasHead = headBone != null;
            bool hasLeftEye = leftEyeBone != null;
            bool hasRightEye = rightEyeBone != null;

            if (!neutralCaptured || !hasNeck || !hasHead ||
                headReferenceFrame == null)
            {
                currentCombinedHeadNeutralRelativeRotation =
                    Quaternion.identity;
                currentHeadForwardInBodyFrame = Vector3.forward;
                currentPose.SetUnavailable(
                    frame,
                    capturedAtUtc,
                    targetKey,
                    hasNeck,
                    hasHead,
                    hasLeftEye,
                    hasRightEye);
                return;
            }

            Quaternion neckDelta = Normalize(
                Quaternion.Inverse(neutralNeckLocalRotation) *
                neckBone.localRotation);
            Quaternion headDelta = Normalize(
                Quaternion.Inverse(neutralHeadLocalRotation) *
                headBone.localRotation);

            Quaternion currentHeadReferenceRotation =
                Quaternion.Inverse(headReferenceFrame.rotation) *
                headBone.rotation;
            Quaternion combinedHeadDelta = Normalize(
                Quaternion.Inverse(neutralHeadReferenceRotation) *
                currentHeadReferenceRotation);
            currentCombinedHeadNeutralRelativeRotation = combinedHeadDelta;
            currentHeadForwardInBodyFrame =
                combinedHeadDelta * Vector3.forward;

            CalculateYawPitch(
                combinedHeadDelta,
                out float yaw,
                out float pitch);

            Vector3 eyeMidpoint = hasLeftEye && hasRightEye
                ? (leftEyeBone.position + rightEyeBone.position) * 0.5f
                : Vector3.zero;

            currentPose.SetSolved(
                frame,
                capturedAtUtc,
                targetKey,
                neckDelta,
                headDelta,
                combinedHeadDelta,
                currentHeadForwardInBodyFrame,
                yaw,
                pitch,
                hasLeftEye,
                hasRightEye,
                eyeMidpoint);
        }

        public static Quaternion CalculateNeutralRelativeRotation(
            Quaternion neutral,
            Quaternion solved)
        {
            return Normalize(Quaternion.Inverse(neutral) * solved);
        }

        public static void CalculateYawPitch(
            Quaternion neutralRelativeRotation,
            out float yaw,
            out float pitch)
        {
            Vector3 direction =
                neutralRelativeRotation * Vector3.forward;
            if (direction.sqrMagnitude < 0.000001f)
            {
                yaw = 0f;
                pitch = 0f;
                return;
            }

            direction.Normalize();
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float horizontal = Mathf.Sqrt(
                direction.x * direction.x +
                direction.z * direction.z);
            pitch = Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;
        }

        private void CacheBones()
        {
            neckBone = null;
            headBone = null;
            leftEyeBone = null;
            rightEyeBone = null;
            headReferenceFrame = null;

            if (animator == null || !animator.isHuman)
                return;

            neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            leftEyeBone = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            rightEyeBone = animator.GetBoneTransform(HumanBodyBones.RightEye);
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            if (magnitude < 0.000001f)
                return Quaternion.identity;

            float inverse = 1f / magnitude;
            return new Quaternion(
                value.x * inverse,
                value.y * inverse,
                value.z * inverse,
                value.w * inverse);
        }
    }
}
