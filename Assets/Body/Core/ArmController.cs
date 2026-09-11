// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Body.IK;
using SalieriAI.Body.SpatialTarget;

namespace SalieriAI.Body.Core
{
    /// <summary>
    /// 左右腕の共通入口。
    ///
    /// SpatialTarget の選択を RobotPoseTargetSelector へ委譲し、
    /// Target 反映成功後に、必要に応じて対象腕の IK を実行する。
    ///
    /// 通常 Target:
    ///     RobotArmIKSolver.SolveOnce() を実行する。
    ///
    /// Neutral Target:
    ///     手先座標だけではなく腕全体を基準姿勢へ戻すため、
    ///     RobotArmIKSolver.ReturnArmHome() を実行する。
    ///
    /// 関節角度の生成は RobotArmIKSolver、
    /// 単軸制約と可動域 Clamp は BodyJointConstraint が担当する。
    /// サーボ送信は後工程。
    /// </summary>
    public sealed class ArmController : MonoBehaviour
    {
        [Header("Target Selector")]

        [Tooltip("固定 SpatialTarget を HandTarget へ反映する Selector")]
        [SerializeField]
        private RobotPoseTargetSelector robotPoseTargetSelector;

        [Header("IK Solvers")]

        [Tooltip("右腕 IK Solver")]
        [SerializeField]
        private RobotArmIKSolver rightArmIkSolver;

        [Tooltip("左腕 IK Solver")]
        [SerializeField]
        private RobotArmIKSolver leftArmIkSolver;

        [Header("IK Execution")]

        [Tooltip(
            "ON の場合、SpatialTarget 選択成功後に対象腕の IK を自動実行する。"
        )]
        [SerializeField]
        private bool autoSolveAfterTargetSelection = true;

        [Header("Debug Target Selection")]

        [Tooltip("Inspector から右腕へ適用する通常 Target ID")]
        [SerializeField]
        private string debugRightTargetId = "test_right_hand";

        [Tooltip("Inspector から左腕へ適用する通常 Target ID")]
        [SerializeField]
        private string debugLeftTargetId = "test_left_hand";

        /// <summary>
        /// 右腕用の Fixed SpatialTarget を選択する。
        ///
        /// neutral_right_hand の場合は IK 探索を行わず、
        /// 腕全体を Home 姿勢へ戻す。
        /// </summary>
        public bool TrySelectRightHandTarget(string targetId)
        {
            if (robotPoseTargetSelector == null)
            {
                Debug.LogError(
                    "[ArmController] RobotPoseTargetSelector is not assigned.",
                    this
                );

                return false;
            }

            bool applied =
                robotPoseTargetSelector.TrySelectRightHandTarget(targetId);

            if (!applied)
            {
                return false;
            }

            if (!autoSolveAfterTargetSelection)
            {
                return true;
            }

            if (rightArmIkSolver == null)
            {
                Debug.LogError(
                    "[ArmController] Right RobotArmIKSolver is not assigned.",
                    this
                );

                return false;
            }

            if (targetId == "neutral_right_hand")
            {
                rightArmIkSolver.ReturnArmHome();
            }
            else
            {
                rightArmIkSolver.SolveOnce();
            }

            return true;
        }

        /// <summary>
        /// 左腕用の Fixed SpatialTarget を選択する。
        ///
        /// neutral_left_hand の場合は IK 探索を行わず、
        /// 腕全体を Home 姿勢へ戻す。
        /// </summary>
        public bool TrySelectLeftHandTarget(string targetId)
        {
            if (robotPoseTargetSelector == null)
            {
                Debug.LogError(
                    "[ArmController] RobotPoseTargetSelector is not assigned.",
                    this
                );

                return false;
            }

            bool applied =
                robotPoseTargetSelector.TrySelectLeftHandTarget(targetId);

            if (!applied)
            {
                return false;
            }

            if (!autoSolveAfterTargetSelection)
            {
                return true;
            }

            if (leftArmIkSolver == null)
            {
                Debug.LogError(
                    "[ArmController] Left RobotArmIKSolver is not assigned.",
                    this
                );

                return false;
            }

            if (targetId == "neutral_left_hand")
            {
                leftArmIkSolver.ReturnArmHome();
            }
            else
            {
                leftArmIkSolver.SolveOnce();
            }

            return true;
        }

        /// <summary>
        /// Inspector の debugRightTargetId に指定した通常 Target を右腕へ適用する。
        /// Target 反映後、autoSolveAfterTargetSelection が ON なら IK を自動実行する。
        /// </summary>
        [ContextMenu("Debug Apply Right Target")]
        public void DebugApplyRightTarget()
        {
            bool applied =
                TrySelectRightHandTarget(debugRightTargetId);

            if (!applied)
            {
                Debug.LogWarning(
                    $"[ArmController] " +
                    $"Failed to apply right target: {debugRightTargetId}",
                    this
                );

                return;
            }

            Debug.Log(
                $"[ArmController] " +
                $"Right target applied: {debugRightTargetId}",
                this
            );
        }

        /// <summary>
        /// Inspector の debugLeftTargetId に指定した通常 Target を左腕へ適用する。
        /// Target 反映後、autoSolveAfterTargetSelection が ON なら IK を自動実行する。
        /// </summary>
        [ContextMenu("Debug Apply Left Target")]
        public void DebugApplyLeftTarget()
        {
            bool applied =
                TrySelectLeftHandTarget(debugLeftTargetId);

            if (!applied)
            {
                Debug.LogWarning(
                    $"[ArmController] " +
                    $"Failed to apply left target: {debugLeftTargetId}",
                    this
                );

                return;
            }

            Debug.Log(
                $"[ArmController] " +
                $"Left target applied: {debugLeftTargetId}",
                this
            );
        }

        /// <summary>
        /// 左右腕を Neutral Target 経由で Home 姿勢へ戻す。
        ///
        /// Neutral Target は通常 IK ではなく ReturnArmHome() を使用する。
        /// </summary>
        [ContextMenu("Debug Apply Neutral Arm Targets")]
        public void DebugApplyNeutralArmTargets()
        {
            bool rightApplied =
                TrySelectRightHandTarget("neutral_right_hand");

            bool leftApplied =
                TrySelectLeftHandTarget("neutral_left_hand");

            if (!rightApplied || !leftApplied)
            {
                Debug.LogWarning(
                    "[ArmController] " +
                    "Failed to apply one or more neutral arm targets.",
                    this
                );

                return;
            }

            Debug.Log(
                "[ArmController] Neutral arm targets applied.",
                this
            );
        }
    }
}