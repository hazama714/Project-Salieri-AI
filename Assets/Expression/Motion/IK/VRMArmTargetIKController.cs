// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Expression.Motion
{
    /// <summary>
    /// 左右のHandTargetをVRM HumanoidのAnimator IKへ渡す。
    ///
    /// Elbow Hintは扱わない。
    /// VRMで解決されたLowerArm位置をバーチャルボディへ渡す処理は、
    /// AnimatorElbowHintFollowerが別責任として担当する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class VRMArmTargetIKController : MonoBehaviour
    {
        [Header("Existing IK Targets")]
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform leftHandTarget;

        [Header("Follow Speed")]
        [SerializeField, Min(0.001f)]
        private float handMoveSpeed = 0.20f;

        [Header("IK Weight")]
        [SerializeField, Range(0f, 1f)]
        private float handIkWeight = 1f;

        private Animator animator;
        private Vector3 currentRightHandPosition;
        private Vector3 currentLeftHandPosition;
        private bool initialized;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            initialized = false;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            if (!initialized)
            {
                InitializeCurrentPositions();
            }

            float handStep = handMoveSpeed * Time.deltaTime;

            ApplyHandTarget(
                AvatarIKGoal.RightHand,
                rightHandTarget,
                ref currentRightHandPosition,
                handStep
            );

            ApplyHandTarget(
                AvatarIKGoal.LeftHand,
                leftHandTarget,
                ref currentLeftHandPosition,
                handStep
            );

            // Elbow Hintは使用しない。
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
        }

        private void InitializeCurrentPositions()
        {
            currentRightHandPosition = GetBonePosition(
                HumanBodyBones.RightHand,
                rightHandTarget
            );

            currentLeftHandPosition = GetBonePosition(
                HumanBodyBones.LeftHand,
                leftHandTarget
            );

            initialized = true;
        }

        private Vector3 GetBonePosition(
            HumanBodyBones bone,
            Transform fallbackTarget
        )
        {
            Transform boneTransform = animator.GetBoneTransform(bone);

            if (boneTransform != null)
            {
                return boneTransform.position;
            }

            if (fallbackTarget != null)
            {
                return fallbackTarget.position;
            }

            return transform.position;
        }

        private void ApplyHandTarget(
            AvatarIKGoal goal,
            Transform target,
            ref Vector3 currentPosition,
            float moveStep
        )
        {
            if (target == null)
            {
                animator.SetIKPositionWeight(goal, 0f);
                animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            currentPosition = Vector3.MoveTowards(
                currentPosition,
                target.position,
                moveStep
            );

            animator.SetIKPositionWeight(goal, handIkWeight);
            animator.SetIKRotationWeight(goal, 0f);
            animator.SetIKPosition(goal, currentPosition);
        }
    }
}