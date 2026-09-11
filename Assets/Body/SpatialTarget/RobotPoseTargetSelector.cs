// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Body.SpatialTarget
{
    /// <summary>
    /// SpatialTargetRegistry から固定 Target を取得し、
    /// IK 用 HandTarget Transform へ位置と回転をコピーする。
    ///
    /// Phase 1 では Target の選択と反映だけを担当する。
    /// IK計算、関節角度生成、サーボ送信は担当しない。
    /// </summary>
    public sealed class RobotPoseTargetSelector : MonoBehaviour
    {
        [Header("Registry")]

        [Tooltip("固定 SpatialTarget を管理する Registry")]
        [SerializeField]
        private SpatialTargetRegistry spatialTargetRegistry;

        [Header("IK Target References")]

        [Tooltip("右腕 IK 用 HandTarget")]
        [SerializeField]
        private Transform rightHandTarget;

        [Tooltip("左腕 IK 用 HandTarget")]
        [SerializeField]
        private Transform leftHandTarget;

        [Header("Debug Selection")]

        [Tooltip("Inspector から右腕へ反映する Target ID")]
        [SerializeField]
        private string debugRightTargetId = "neutral_right_hand";

        [Tooltip("Inspector から左腕へ反映する Target ID")]
        [SerializeField]
        private string debugLeftTargetId = "neutral_left_hand";

        [Header("Diagnostics")]

        [SerializeField]
        private bool logSelection = true;

        public string CurrentRightTargetId { get; private set; } = string.Empty;

        public string CurrentLeftTargetId { get; private set; } = string.Empty;

        public bool TrySelectRightHandTarget(string targetId)
        {
            return TryApplyTarget(
                targetId,
                rightHandTarget,
                isRightHand: true
            );
        }

        public bool TrySelectLeftHandTarget(string targetId)
        {
            return TryApplyTarget(
                targetId,
                leftHandTarget,
                isRightHand: false
            );
        }

        [ContextMenu("Debug Apply Right Target")]
        public void DebugApplyRightTarget()
        {
            TrySelectRightHandTarget(debugRightTargetId);
        }

        [ContextMenu("Debug Apply Left Target")]
        public void DebugApplyLeftTarget()
        {
            TrySelectLeftHandTarget(debugLeftTargetId);
        }

        [ContextMenu("Debug Apply Neutral Targets")]
        public void DebugApplyNeutralTargets()
        {
            bool rightApplied =
                TrySelectRightHandTarget("neutral_right_hand");

            bool leftApplied =
                TrySelectLeftHandTarget("neutral_left_hand");

            if (!rightApplied || !leftApplied)
            {
                Debug.LogWarning(
                    "[RobotPoseTargetSelector] " +
                    "Failed to apply one or more neutral targets.",
                    this
                );
            }
        }

        private bool TryApplyTarget(
            string targetId,
            Transform destination,
            bool isRightHand
        )
        {
            if (spatialTargetRegistry == null)
            {
                Debug.LogError(
                    "[RobotPoseTargetSelector] " +
                    "SpatialTargetRegistry is not assigned.",
                    this
                );

                return false;
            }

            if (destination == null)
            {
                string handLabel = isRightHand ? "Right" : "Left";

                Debug.LogError(
                    $"[RobotPoseTargetSelector] " +
                    $"{handLabel}HandTarget is not assigned.",
                    this
                );

                return false;
            }

            if (!spatialTargetRegistry.TryGetTarget(
                    targetId,
                    out SpatialTargetData target
                ))
            {
                Debug.LogWarning(
                    $"[RobotPoseTargetSelector] " +
                    $"Target not found or unavailable: {targetId}",
                    this
                );

                return false;
            }

            bool canApplyToSelectedHand =
                isRightHand
                    ? target.CanApplyToRightHand()
                    : target.CanApplyToLeftHand();

            if (!canApplyToSelectedHand)
            {
                string handLabel = isRightHand ? "Right" : "Left";

                Debug.LogWarning(
                    $"[RobotPoseTargetSelector] " +
                    $"Target hand scope mismatch. " +
                    $"{handLabel}HandTarget <- {target.TargetId}, " +
                    $"scope={target.HandScope}",
                    this
                );

                return false;
            }

            destination.SetPositionAndRotation(
                target.Position,
                target.Rotation
            );

            if (isRightHand)
            {
                CurrentRightTargetId = target.TargetId;
            }
            else
            {
                CurrentLeftTargetId = target.TargetId;
            }

            if (logSelection)
            {
                string handLabel = isRightHand ? "Right" : "Left";

                Debug.Log(
                    $"[RobotPoseTargetSelector] " +
                    $"{handLabel}HandTarget <- {target.TargetId} " +
                    $"position={target.Position}",
                    this
                );
            }

            return true;
        }
    }
}