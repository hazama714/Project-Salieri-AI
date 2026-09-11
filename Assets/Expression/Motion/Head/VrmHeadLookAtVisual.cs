// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Body.Retargeting.Neck;

namespace SalieriAI.Expression.Motion
{
    [DefaultExecutionOrder(700)]
    [RequireComponent(typeof(Animator))]
    public sealed class VrmHeadLookAtVisual : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField]
        private Transform attentionTarget;

        [Header("LookAt Weights")]
        [Range(0f, 1f)]
        [SerializeField]
        private float overallWeight = 1f;

        [Range(0f, 1f)]
        [SerializeField]
        private float bodyWeight = 0f;

        [Range(0f, 1f)]
        [SerializeField]
        private float headWeight = 0.9f;

        [Range(0f, 1f)]
        [SerializeField]
        private float eyesWeight = 0f;

        [Range(0f, 1f)]
        [SerializeField]
        private float clampWeight = 0.6f;

        [Header("Visual Smoothing")]
        [Min(0.01f)]
        [SerializeField]
        private float smoothTime = 0.15f;

        private Animator animator;

        private Vector3 smoothedLookPosition;
        private Vector3 lookVelocity;
        private bool hasInitializedLookPosition;
        private UnityEngine.Object animatorIkCoordinatorOwner;
        private NeckController neckController;
        private VirtualNeckRetargetShadow retargetShadow;
        private Transform neckBone;
        private Transform headBone;
        private Transform headReferenceFrame;
        private Quaternion neutralHeadReferenceRotation = Quaternion.identity;
        private bool finalPoseNeutralCaptured;

        [Header("Commanded Visual Sync (Read Only)")]
        [SerializeField] private bool lastCommandedVisualPoseApplied;
        [SerializeField] private int lastCommandedVisualPoseAppliedFrame = -1;
        [SerializeField] private float lastRawSolvedYaw;
        [SerializeField] private float lastRawSolvedPitch;
        [SerializeField] private float lastAppliedVirtualYaw;
        [SerializeField] private float lastAppliedVirtualPitch;
        [SerializeField] private float lastAppliedCanonicalYaw;
        [SerializeField] private float lastAppliedCanonicalPitch;

        public bool AnimatorIkCoordinatorOwned =>
            animatorIkCoordinatorOwner != null;
        public int LastLookAtAppliedFrame { get; private set; } = -1;
        public Vector3 LastLookAtAppliedPosition { get; private set; }
        public bool LastCommandedVisualPoseApplied =>
            lastCommandedVisualPoseApplied;
        public int LastCommandedVisualPoseAppliedFrame =>
            lastCommandedVisualPoseAppliedFrame;
        public float LastRawSolvedYaw => lastRawSolvedYaw;
        public float LastRawSolvedPitch => lastRawSolvedPitch;
        public float LastAppliedVirtualYaw => lastAppliedVirtualYaw;
        public float LastAppliedVirtualPitch => lastAppliedVirtualPitch;
        public float LastAppliedCanonicalYaw => lastAppliedCanonicalYaw;
        public float LastAppliedCanonicalPitch => lastAppliedCanonicalPitch;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            ResolveCommandedPoseDependencies();
            CaptureFinalPoseNeutral();
        }

        private void LateUpdate()
        {
            ApplyCommandedVisualPose();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animatorIkCoordinatorOwner != null)
            {
                return;
            }

            ApplyLookAtParameters(animator, Time.frameCount);
        }

        /// <summary>
        /// Claims the LookAt application callback for one coordinator. Existing
        /// scenes without a coordinator continue to use this component's own
        /// OnAnimatorIK path.
        /// </summary>
        public bool TryAcquireAnimatorIkCoordinatorOwnership(
            UnityEngine.Object owner)
        {
            if (owner == null)
                return false;
            if (animatorIkCoordinatorOwner != null &&
                animatorIkCoordinatorOwner != owner)
                return false;
            animatorIkCoordinatorOwner = owner;
            return true;
        }

        public void ReleaseAnimatorIkCoordinatorOwnership(
            UnityEngine.Object owner)
        {
            if (owner != null && animatorIkCoordinatorOwner == owner)
                animatorIkCoordinatorOwner = null;
        }

        public bool ApplyLookAtFromCoordinator(
            UnityEngine.Object owner,
            Animator targetAnimator,
            int frame)
        {
            if (owner == null || animatorIkCoordinatorOwner != owner ||
                targetAnimator == null || targetAnimator != animator)
                return false;
            return ApplyLookAtParameters(targetAnimator, frame);
        }

        private bool ApplyLookAtParameters(
            Animator targetAnimator,
            int frame)
        {
            if (targetAnimator == null || attentionTarget == null)
                return false;

            Vector3 targetPosition = attentionTarget.position;
            bool holdLastSolvedPose = ShouldHoldLastSolvedPose(
                attentionTarget.gameObject.activeInHierarchy,
                hasInitializedLookPosition);

            if (!hasInitializedLookPosition)
            {
                smoothedLookPosition = targetPosition;
                hasInitializedLookPosition = true;
            }
            else if (!holdLastSolvedPose)
            {
                smoothedLookPosition = Vector3.SmoothDamp(
                    smoothedLookPosition,
                    targetPosition,
                    ref lookVelocity,
                    smoothTime
                );
            }

            targetAnimator.SetLookAtWeight(
                overallWeight,
                bodyWeight,
                headWeight,
                eyesWeight,
                clampWeight
            );

            targetAnimator.SetLookAtPosition(smoothedLookPosition);
            LastLookAtAppliedFrame = frame;
            LastLookAtAppliedPosition = smoothedLookPosition;
            return true;
        }

        internal static bool ShouldHoldLastSolvedPose(
            bool targetActiveInHierarchy,
            bool lookPositionInitialized)
        {
            return !targetActiveInHierarchy && lookPositionInitialized;
        }

        private void ResolveCommandedPoseDependencies()
        {
            if (neckController == null)
                neckController = FindObjectOfType<NeckController>();
            if (retargetShadow == null)
                retargetShadow = FindObjectOfType<VirtualNeckRetargetShadow>();
            if (animator == null)
                animator = GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
                return;

            neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            headReferenceFrame = neckBone != null
                ? neckBone.parent
                : null;
        }

        private void CaptureFinalPoseNeutral()
        {
            if (headBone == null || headReferenceFrame == null)
            {
                finalPoseNeutralCaptured = false;
                return;
            }

            neutralHeadReferenceRotation =
                Quaternion.Inverse(headReferenceFrame.rotation) *
                headBone.rotation;
            finalPoseNeutralCaptured = true;
        }

        private void ApplyCommandedVisualPose()
        {
            lastCommandedVisualPoseApplied = false;
            if (neckController == null || retargetShadow == null ||
                neckBone == null || headBone == null ||
                headReferenceFrame == null)
            {
                ResolveCommandedPoseDependencies();
            }
            if (!finalPoseNeutralCaptured)
                CaptureFinalPoseNeutral();
            if (neckController == null || retargetShadow == null ||
                neckBone == null || headBone == null ||
                headReferenceFrame == null || !finalPoseNeutralCaptured)
            {
                return;
            }

            Quaternion referenceRotation = headReferenceFrame.rotation;
            Quaternion rawHeadRotation = headBone.rotation;
            Quaternion rawNeckRotation = neckBone.rotation;
            Quaternion rawHeadReferenceRotation =
                Quaternion.Inverse(referenceRotation) * rawHeadRotation;
            Quaternion rawDelta =
                Quaternion.Inverse(neutralHeadReferenceRotation) *
                rawHeadReferenceRotation;
            Vector3 rawDirection = rawDelta * Vector3.forward;
            VRMNeckCommandedPoseMath.CalculateYawPitch(
                rawDirection,
                out float rawYaw,
                out float rawPitch);
            lastRawSolvedYaw = rawYaw;
            lastRawSolvedPitch = rawPitch;

            if (!VRMNeckCommandedPoseMath.TryBuildVirtualDirection(
                    neckController.CommandedYawRelativeAngle,
                    neckController.CommandedPitchRelativeAngle,
                    retargetShadow.Profile,
                    out Vector3 commandedDirection))
            {
                return;
            }

            Quaternion correction =
                VRMNeckCommandedPoseMath.BuildReferenceCorrection(
                    rawDirection,
                    commandedDirection);
            Quaternion neckCorrection = Quaternion.Slerp(
                Quaternion.identity,
                correction,
                0.5f);
            Quaternion referenceInverse = Quaternion.Inverse(referenceRotation);
            neckBone.rotation =
                referenceRotation * neckCorrection * referenceInverse *
                rawNeckRotation;
            headBone.rotation =
                referenceRotation * correction * referenceInverse *
                rawHeadRotation;

            Quaternion appliedHeadReferenceRotation =
                Quaternion.Inverse(referenceRotation) * headBone.rotation;
            Quaternion appliedDelta =
                Quaternion.Inverse(neutralHeadReferenceRotation) *
                appliedHeadReferenceRotation;
            Vector3 appliedDirection = appliedDelta * Vector3.forward;
            VRMNeckCommandedPoseMath.CalculateYawPitch(
                appliedDirection,
                out float appliedYaw,
                out float appliedPitch);
            lastAppliedVirtualYaw = appliedYaw;
            lastAppliedVirtualPitch = appliedPitch;
            VirtualNeckRetargetProfile profile = retargetShadow.Profile;
            lastAppliedCanonicalYaw =
                appliedYaw * profile.YawSign * profile.YawGain +
                profile.YawOffsetDegrees;
            lastAppliedCanonicalPitch =
                appliedPitch * profile.PitchSign * profile.PitchGain +
                profile.PitchOffsetDegrees;
            lastCommandedVisualPoseApplied = true;
            lastCommandedVisualPoseAppliedFrame = Time.frameCount;
        }
    }
}
