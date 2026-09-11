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
    /// 上半身制御の共通入口。
    ///
    /// Phase 2-B では ArmController への委譲のみを担当する。
    /// 将来は肩、胸、胴体などの Controller を追加できる。
    /// </summary>
    public sealed class UpperBodyController : MonoBehaviour
    {
        [Header("Upper Body Parts")]

        [SerializeField]
        private ArmController armController;

        public bool TrySelectRightHandTarget(string targetId)
        {
            if (armController == null)
            {
                Debug.LogError(
                    "[UpperBodyController] ArmController is not assigned.",
                    this
                );

                return false;
            }

            return armController.TrySelectRightHandTarget(targetId);
        }

        public bool TrySelectLeftHandTarget(string targetId)
        {
            if (armController == null)
            {
                Debug.LogError(
                    "[UpperBodyController] ArmController is not assigned.",
                    this
                );

                return false;
            }

            return armController.TrySelectLeftHandTarget(targetId);
        }

        [ContextMenu("Debug Apply Neutral Upper Body Targets")]
        public void DebugApplyNeutralUpperBodyTargets()
        {
            bool rightApplied =
                TrySelectRightHandTarget("neutral_right_hand");

            bool leftApplied =
                TrySelectLeftHandTarget("neutral_left_hand");

            if (!rightApplied || !leftApplied)
            {
                Debug.LogWarning(
                    "[UpperBodyController] " +
                    "Failed to apply one or more neutral upper body targets.",
                    this
                );

                return;
            }

            Debug.Log(
                "[UpperBodyController] Neutral upper body targets applied.",
                this
            );
        }
    }
}