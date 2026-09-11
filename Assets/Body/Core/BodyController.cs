// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Body.Core
{
    /// <summary>
    /// 身体制御全体の共通出口。
    ///
    /// 上位 Runtime は腕や各関節を直接操作せず、
    /// 原則として BodyController を入口にする。
    ///
    /// Phase 2 では UpperBodyController への委譲を担当する。
    /// 将来は LowerBodyController などを追加できる。
    /// </summary>
    public sealed class BodyController : MonoBehaviour
    {
        [Header("Body Sections")]

        [Tooltip("上半身制御の共通入口")]
        [SerializeField]
        private UpperBodyController upperBodyController;

        [Header("Debug Target Selection")]

        [Tooltip("Inspector から右手へ反映する Target ID")]
        [SerializeField]
        private string debugRightTargetId = "test_right_hand";

        [Tooltip("Inspector から左手へ反映する Target ID")]
        [SerializeField]
        private string debugLeftTargetId = "test_left_hand";

        /// <summary>
        /// 右手用の SpatialTarget を選択する。
        /// </summary>
        public bool TrySelectRightHandTarget(string targetId)
        {
            if (upperBodyController == null)
            {
                Debug.LogError(
                    "[BodyController] UpperBodyController is not assigned.",
                    this
                );

                return false;
            }

            return upperBodyController.TrySelectRightHandTarget(targetId);
        }

        /// <summary>
        /// 左手用の SpatialTarget を選択する。
        /// </summary>
        public bool TrySelectLeftHandTarget(string targetId)
        {
            if (upperBodyController == null)
            {
                Debug.LogError(
                    "[BodyController] UpperBodyController is not assigned.",
                    this
                );

                return false;
            }

            return upperBodyController.TrySelectLeftHandTarget(targetId);
        }

        /// <summary>
        /// Inspector で指定した Target を右手へ反映する。
        /// </summary>
        [ContextMenu("Debug Apply Right Body Target")]
        public void DebugApplyRightBodyTarget()
        {
            if (!TrySelectRightHandTarget(debugRightTargetId))
            {
                Debug.LogWarning(
                    $"[BodyController] Failed to apply right body target: " +
                    $"{debugRightTargetId}",
                    this
                );

                return;
            }

            Debug.Log(
                $"[BodyController] Right body target applied: " +
                $"{debugRightTargetId}",
                this
            );
        }

        /// <summary>
        /// Inspector で指定した Target を左手へ反映する。
        /// </summary>
        [ContextMenu("Debug Apply Left Body Target")]
        public void DebugApplyLeftBodyTarget()
        {
            if (!TrySelectLeftHandTarget(debugLeftTargetId))
            {
                Debug.LogWarning(
                    $"[BodyController] Failed to apply left body target: " +
                    $"{debugLeftTargetId}",
                    this
                );

                return;
            }

            Debug.Log(
                $"[BodyController] Left body target applied: " +
                $"{debugLeftTargetId}",
                this
            );
        }

        /// <summary>
        /// 左右の手を Neutral Target へ戻す。
        /// </summary>
        [ContextMenu("Debug Apply Neutral Body Targets")]
        public void DebugApplyNeutralBodyTargets()
        {
            bool rightApplied =
                TrySelectRightHandTarget("neutral_right_hand");

            bool leftApplied =
                TrySelectLeftHandTarget("neutral_left_hand");

            if (!rightApplied || !leftApplied)
            {
                Debug.LogWarning(
                    "[BodyController] " +
                    "Failed to apply one or more neutral body targets.",
                    this
                );

                return;
            }

            Debug.Log(
                "[BodyController] Neutral body targets applied.",
                this
            );
        }
    }
}