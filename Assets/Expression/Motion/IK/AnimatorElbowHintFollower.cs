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
    /// AvatarIKHintに対応するVRM Humanoidボーン位置を基準に、
    /// Elbow / Knee Hint用GameObjectを追従させる。
    ///
    /// 対応:
    /// - LeftElbow  -> LeftLowerArm
    /// - RightElbow -> RightLowerArm
    /// - LeftKnee   -> LeftLowerLeg
    /// - RightKnee  -> RightLowerLeg
    ///
    /// 既存Scene参照を維持するため、クラス名とSerializeField名は変更しない。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class AnimatorElbowHintFollower : MonoBehaviour
    {
        [Header("VRM Animator")]

        [SerializeField]
        private Animator animator;

        [Header("IK Hint")]

        [SerializeField]
        private AvatarIKHint elbowHint =
            AvatarIKHint.LeftElbow;

        [Header("Source Bone Local Offset")]

        [Tooltip(
            "Hintに対応するLowerArm / LowerLegボーン基準のローカル位置。"
        )]
        [SerializeField]
        private Vector3 localOffset =
            Vector3.zero;

        [Header("Rotation")]

        [Tooltip(
            "有効時はHintに対応するLowerArm / LowerLegボーンの回転もコピーする。"
        )]
        [SerializeField]
        private bool copyLowerArmRotation;

        [Header("Debug")]

        [SerializeField]
        private bool logState;

        private Transform sourceBone;

        private void Reset()
        {
            animator =
                GetComponentInParent<Animator>();
        }

        private void OnEnable()
        {
            CacheSourceBone();
        }

        private void CacheSourceBone()
        {
            sourceBone = null;

            if (animator == null)
            {
                return;
            }

            if (!animator.isHuman)
            {
                return;
            }

            HumanBodyBones bone;

            switch (elbowHint)
            {
                case AvatarIKHint.LeftElbow:
                    bone = HumanBodyBones.LeftLowerArm;
                    break;

                case AvatarIKHint.RightElbow:
                    bone = HumanBodyBones.RightLowerArm;
                    break;

                case AvatarIKHint.LeftKnee:
                    bone = HumanBodyBones.LeftLowerLeg;
                    break;

                case AvatarIKHint.RightKnee:
                    bone = HumanBodyBones.RightLowerLeg;
                    break;

                default:
                    return;
            }

            sourceBone =
                animator.GetBoneTransform(bone);
        }

        private void LateUpdate()
        {
            if (animator == null)
            {
                return;
            }

            if (sourceBone == null)
            {
                CacheSourceBone();

                if (sourceBone == null)
                {
                    return;
                }
            }

            transform.position =
                sourceBone.TransformPoint(localOffset);

            if (copyLowerArmRotation)
            {
                transform.rotation =
                    sourceBone.rotation;
            }

            if (logState)
            {
                Debug.Log(
                    $"[AnimatorElbowHintFollower] " +
                    $"Hint={elbowHint}, " +
                    $"SourceBone={sourceBone.name}, " +
                    $"Position={transform.position}",
                    this
                );
            }
        }

        [ContextMenu("Refresh Hint Bone")]
        private void RefreshHintBone()
        {
            CacheSourceBone();
        }
    }
}