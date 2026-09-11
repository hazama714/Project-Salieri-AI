// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SalieriAI.Expression.Motion
{
    /// <summary>
    /// 左右のFootTargetを使用して、
    /// VRM Humanoidの両脚Animator IKを制御する。
    ///
    /// Knee Hint、バーチャルボディ、BodyJointConstraint、
    /// BodyJointServoBridge、実機サーボには接続しない。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class VRMLegTargetIKController : MonoBehaviour
    {
        [Header("IK Targets")]

        [SerializeField]
        private Transform rightFootTarget;

        [SerializeField]
        private Transform leftFootTarget;

        [Header("Control")]

        [SerializeField]
        private bool ikEnabled;

        [FormerlySerializedAs("initializeTargetFromCurrentPose")]
        [SerializeField]
        private bool initializeTargetsFromCurrentPose = true;

        [Header("Follow Speed")]

        [SerializeField]
        [Min(0.001f)]
        private float footMoveSpeed = 0.10f;

        [Header("IK Weight")]

        [SerializeField]
        [Range(0f, 1f)]
        private float targetFootPositionWeight = 1f;

        [SerializeField]
        [Min(0f)]
        private float blendInSeconds = 0.50f;

        [SerializeField]
        [Min(0f)]
        private float blendOutSeconds = 0.25f;

        private Animator animator;

        private Transform hips;

        private Transform rightUpperLeg;
        private Transform rightLowerLeg;
        private Transform rightFoot;

        private Transform leftUpperLeg;
        private Transform leftLowerLeg;
        private Transform leftFoot;

        private Vector3 currentRightFootPosition;
        private Vector3 currentLeftFootPosition;

        private float currentFootWeight;
        private bool initialized;
        private string lastFailureReason = string.Empty;

        public event Action<bool> IkEnabledChanged;

        public bool IsInitialized => initialized;
        public bool IsIkEnabled => ikEnabled;
        public float CurrentFootWeight => currentFootWeight;
        public string LastFailureReason => lastFailureReason;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        private void OnDisable()
        {
            currentFootWeight = 0f;
        }

        private void OnValidate()
        {
            footMoveSpeed =
                Mathf.Max(0.001f, footMoveSpeed);

            targetFootPositionWeight =
                Mathf.Clamp01(targetFootPositionWeight);

            blendInSeconds =
                Mathf.Max(0f, blendInSeconds);

            blendOutSeconds =
                Mathf.Max(0f, blendOutSeconds);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!ValidateAndCacheReferences())
            {
                ClearLegIK();
                return;
            }

            if (!initialized)
            {
                if (!InitializeTargetsFromCurrentPoseInternal())
                {
                    ClearLegIK();
                    return;
                }
            }

            Vector3 requestedRightFootPosition =
                rightFootTarget.position;

            Vector3 requestedLeftFootPosition =
                leftFootTarget.position;

            if (!IsFinite(requestedRightFootPosition))
            {
                Fail(
                    "RightFootTarget position contains " +
                    "NaN or Infinity."
                );

                ClearLegIK();
                return;
            }

            if (!IsFinite(requestedLeftFootPosition))
            {
                Fail(
                    "LeftFootTarget position contains " +
                    "NaN or Infinity."
                );

                ClearLegIK();
                return;
            }

            UpdateWeight();

            float footStep =
                footMoveSpeed * Time.deltaTime;

            currentRightFootPosition =
                Vector3.MoveTowards(
                    currentRightFootPosition,
                    requestedRightFootPosition,
                    footStep
                );

            currentLeftFootPosition =
                Vector3.MoveTowards(
                    currentLeftFootPosition,
                    requestedLeftFootPosition,
                    footStep
                );

            ApplyFootIK(
                AvatarIKGoal.RightFoot,
                AvatarIKHint.RightKnee,
                currentRightFootPosition
            );

            ApplyFootIK(
                AvatarIKGoal.LeftFoot,
                AvatarIKHint.LeftKnee,
                currentLeftFootPosition
            );
        }

        public void SetIkEnabled(bool enabled)
        {
            if (ikEnabled == enabled)
            {
                return;
            }

            if (enabled && !initialized)
            {
                if (!InitializeTargetsFromCurrentPoseInternal())
                {
                    return;
                }
            }

            ikEnabled = enabled;
            IkEnabledChanged?.Invoke(ikEnabled);
        }

        public void ToggleIkEnabled()
        {
            SetIkEnabled(!ikEnabled);
        }

        private void ApplyFootIK(
            AvatarIKGoal goal,
            AvatarIKHint kneeHint,
            Vector3 footPosition
        )
        {
            animator.SetIKPositionWeight(
                goal,
                currentFootWeight
            );

            animator.SetIKRotationWeight(
                goal,
                0f
            );

            animator.SetIKHintPositionWeight(
                kneeHint,
                0f
            );

            if (currentFootWeight > 0f)
            {
                animator.SetIKPosition(
                    goal,
                    footPosition
                );
            }
        }

        private void ResetRuntimeState()
        {
            initialized = false;
            currentFootWeight = 0f;
            lastFailureReason = string.Empty;

            hips = null;

            rightUpperLeg = null;
            rightLowerLeg = null;
            rightFoot = null;

            leftUpperLeg = null;
            leftLowerLeg = null;
            leftFoot = null;
        }

        private bool ValidateAndCacheReferences()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                return Fail("Animator is missing.");
            }

            if (!animator.isHuman)
            {
                return Fail(
                    "Animator is not configured as Humanoid."
                );
            }

            if (rightFootTarget == null)
            {
                return Fail(
                    "RightFootTarget is not assigned."
                );
            }

            if (leftFootTarget == null)
            {
                return Fail(
                    "LeftFootTarget is not assigned."
                );
            }

            CacheHumanoidBones();

            if (hips == null)
            {
                return Fail(
                    "Humanoid Hips bone is missing."
                );
            }

            if (rightUpperLeg == null)
            {
                return Fail(
                    "Humanoid RightUpperLeg bone is missing."
                );
            }

            if (rightLowerLeg == null)
            {
                return Fail(
                    "Humanoid RightLowerLeg bone is missing."
                );
            }

            if (rightFoot == null)
            {
                return Fail(
                    "Humanoid RightFoot bone is missing."
                );
            }

            if (leftUpperLeg == null)
            {
                return Fail(
                    "Humanoid LeftUpperLeg bone is missing."
                );
            }

            if (leftLowerLeg == null)
            {
                return Fail(
                    "Humanoid LeftLowerLeg bone is missing."
                );
            }

            if (leftFoot == null)
            {
                return Fail(
                    "Humanoid LeftFoot bone is missing."
                );
            }

            lastFailureReason = string.Empty;
            return true;
        }

        private void CacheHumanoidBones()
        {
            if (hips == null)
            {
                hips = animator.GetBoneTransform(
                    HumanBodyBones.Hips
                );
            }

            if (rightUpperLeg == null)
            {
                rightUpperLeg = animator.GetBoneTransform(
                    HumanBodyBones.RightUpperLeg
                );
            }

            if (rightLowerLeg == null)
            {
                rightLowerLeg = animator.GetBoneTransform(
                    HumanBodyBones.RightLowerLeg
                );
            }

            if (rightFoot == null)
            {
                rightFoot = animator.GetBoneTransform(
                    HumanBodyBones.RightFoot
                );
            }

            if (leftUpperLeg == null)
            {
                leftUpperLeg = animator.GetBoneTransform(
                    HumanBodyBones.LeftUpperLeg
                );
            }

            if (leftLowerLeg == null)
            {
                leftLowerLeg = animator.GetBoneTransform(
                    HumanBodyBones.LeftLowerLeg
                );
            }

            if (leftFoot == null)
            {
                leftFoot = animator.GetBoneTransform(
                    HumanBodyBones.LeftFoot
                );
            }
        }

        private bool InitializeTargetsFromCurrentPoseInternal()
        {
            if (!ValidateAndCacheReferences())
            {
                return false;
            }

            Vector3 rightFootPosition =
                rightFoot.position;

            Vector3 leftFootPosition =
                leftFoot.position;

            if (!IsFinite(rightFootPosition))
            {
                return Fail(
                    "Current RightFoot position contains " +
                    "NaN or Infinity."
                );
            }

            if (!IsFinite(leftFootPosition))
            {
                return Fail(
                    "Current LeftFoot position contains " +
                    "NaN or Infinity."
                );
            }

            if (initializeTargetsFromCurrentPose)
            {
                rightFootTarget.position =
                    rightFootPosition;

                leftFootTarget.position =
                    leftFootPosition;
            }

            currentRightFootPosition =
                rightFootPosition;

            currentLeftFootPosition =
                leftFootPosition;

            currentFootWeight = 0f;
            initialized = true;
            lastFailureReason = string.Empty;

            return true;
        }

        private void UpdateWeight()
        {
            float desiredFootWeight =
                ikEnabled
                    ? targetFootPositionWeight
                    : 0f;

            float blendSeconds =
                ikEnabled
                    ? blendInSeconds
                    : blendOutSeconds;

            if (blendSeconds <= 0f)
            {
                currentFootWeight =
                    desiredFootWeight;

                return;
            }

            float maxDelta =
                Time.deltaTime / blendSeconds;

            currentFootWeight =
                Mathf.MoveTowards(
                    currentFootWeight,
                    desiredFootWeight,
                    maxDelta
                );
        }

        private void ClearLegIK()
        {
            if (animator == null)
            {
                return;
            }

            ClearFootIK(
                AvatarIKGoal.RightFoot,
                AvatarIKHint.RightKnee
            );

            ClearFootIK(
                AvatarIKGoal.LeftFoot,
                AvatarIKHint.LeftKnee
            );

            currentFootWeight = 0f;
        }

        private void ClearFootIK(
            AvatarIKGoal goal,
            AvatarIKHint kneeHint
        )
        {
            animator.SetIKPositionWeight(goal, 0f);
            animator.SetIKRotationWeight(goal, 0f);
            animator.SetIKHintPositionWeight(kneeHint, 0f);
        }

        private bool Fail(string reason)
        {
            if (lastFailureReason == reason)
            {
                return false;
            }

            lastFailureReason = reason;

            Debug.LogWarning(
                "[VRMLegTargetIKController] " +
                reason,
                this
            );

            return false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return
                IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return
                !float.IsNaN(value)
                && !float.IsInfinity(value);
        }
    }
}
