// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SalieriAI.Body.Servo.Safety;
using SalieriAI.Expression.Motion;

namespace SalieriAI.App.UI
{
    /// <summary>
    /// 実機腕の安全Controller、VRM両脚IK、カメラ表示を
    /// 手動UIから操作する薄いAdapter。
    ///
    /// サーボBridgeや送信Coordinatorは直接操作しない。
    /// 脚IKの有効・無効は実機ARM / DISARM状態とは独立して扱う。
    /// </summary>
    public sealed class RobotManualControlPanel : MonoBehaviour
    {
        [Header("Controllers")]

        [SerializeField]
        private ArmServoOutputController armServoOutputController;

        [SerializeField]
        private VRMLegTargetIKController legIkController;

        [SerializeField]
        private SimpleCameraOrbit cameraOrbit;

        [Header("Arm UI")]

        [SerializeField]
        private Button armButton;

        [SerializeField]
        private Button disarmButton;

        [SerializeField]
        private Text armStateText;

        [Header("Leg IK UI")]

        [SerializeField]
        private Button enableLegIkButton;

        [SerializeField]
        private Button disableLegIkButton;

        [SerializeField]
        private Text legIkStateText;

        [Header("Camera UI")]

        [SerializeField]
        private Button cameraResetButton;

        private readonly HashSet<string> loggedMissingReferences =
            new HashSet<string>();

        private void Awake()
        {
            ValidateReferences();
        }

        private void OnEnable()
        {
            if (armServoOutputController != null)
            {
                armServoOutputController.StateChanged +=
                    HandleArmStateChanged;
            }

            if (legIkController != null)
            {
                legIkController.IkEnabledChanged +=
                    HandleLegIkEnabledChanged;
            }

            RefreshArmStateView();
            RefreshLegIkStateView();
            RefreshCameraView();
        }

        private void OnDisable()
        {
            if (armServoOutputController != null)
            {
                armServoOutputController.StateChanged -=
                    HandleArmStateChanged;
            }

            if (legIkController != null)
            {
                legIkController.IkEnabledChanged -=
                    HandleLegIkEnabledChanged;
            }
        }

        public void OnArmButtonClicked()
        {
            Debug.Log(
                "[RobotManualControlPanel] ARM clicked.",
                this
            );

            if (armServoOutputController == null)
            {
                WarnMissingReferenceOnce(
                    nameof(armServoOutputController)
                );
                return;
            }

            armServoOutputController.ArmAll();
        }

        public void OnDisarmButtonClicked()
        {
            Debug.Log(
                "[RobotManualControlPanel] DISARM clicked.",
                this
            );

            if (armServoOutputController == null)
            {
                WarnMissingReferenceOnce(
                    nameof(armServoOutputController)
                );
                return;
            }

            armServoOutputController.DisarmAll(
                ArmDisarmReason.UserRequest
            );
        }

        public void OnEnableLegIkButtonClicked()
        {
            Debug.Log(
                "[RobotManualControlPanel] ENABLE LEGS IK clicked.",
                this
            );

            if (legIkController == null)
            {
                WarnMissingReferenceOnce(
                    nameof(legIkController)
                );
                return;
            }

            legIkController.SetIkEnabled(true);
        }

        public void OnDisableLegIkButtonClicked()
        {
            Debug.Log(
                "[RobotManualControlPanel] DISABLE LEGS IK clicked.",
                this
            );

            if (legIkController == null)
            {
                WarnMissingReferenceOnce(
                    nameof(legIkController)
                );
                return;
            }

            legIkController.SetIkEnabled(false);
        }

        public void OnCameraResetButtonClicked()
        {
            Debug.Log(
                "[RobotManualControlPanel] CAMERA RESET clicked.",
                this
            );

            if (cameraOrbit == null)
            {
                WarnMissingReferenceOnce(nameof(cameraOrbit));
                return;
            }

            cameraOrbit.ResetCameraPosition();
        }

        private void HandleArmStateChanged(
            ArmServoOutputState newState
        )
        {
            RefreshArmStateView(newState);
        }

        private void HandleLegIkEnabledChanged(bool enabled)
        {
            RefreshLegIkStateView(enabled);
        }

        private void RefreshArmStateView()
        {
            if (armServoOutputController == null)
            {
                if (armStateText != null)
                {
                    armStateText.text = "ARM: UNKNOWN";
                }

                SetButtonInteractable(armButton, false);
                SetButtonInteractable(disarmButton, false);
                return;
            }

            RefreshArmStateView(
                armServoOutputController.State
            );
        }

        private void RefreshArmStateView(
            ArmServoOutputState state
        )
        {
            if (armStateText != null)
            {
                armStateText.text =
                    $"ARM: {state.ToString().ToUpperInvariant()}";
            }

            bool canArm =
                state == ArmServoOutputState.Disarmed;

            bool canDisarm =
                state != ArmServoOutputState.Disarmed;

            SetButtonInteractable(armButton, canArm);
            SetButtonInteractable(disarmButton, canDisarm);
        }

        private void RefreshLegIkStateView()
        {
            if (legIkController == null)
            {
                if (legIkStateText != null)
                {
                    legIkStateText.text =
                        "LEGS IK: UNKNOWN";
                }

                SetButtonInteractable(
                    enableLegIkButton,
                    false
                );

                SetButtonInteractable(
                    disableLegIkButton,
                    false
                );

                return;
            }

            RefreshLegIkStateView(
                legIkController.IsIkEnabled
            );
        }

        private void RefreshLegIkStateView(bool enabled)
        {
            if (legIkStateText != null)
            {
                legIkStateText.text =
                    enabled
                        ? "LEGS IK: ENABLED"
                        : "LEGS IK: DISABLED";
            }

            SetButtonInteractable(
                enableLegIkButton,
                !enabled
            );

            SetButtonInteractable(
                disableLegIkButton,
                enabled
            );
        }

        private void RefreshCameraView()
        {
            SetButtonInteractable(
                cameraResetButton,
                cameraOrbit != null
            );
        }

        private void ValidateReferences()
        {
            if (armServoOutputController == null)
            {
                WarnMissingReferenceOnce(
                    nameof(armServoOutputController)
                );
            }

            if (legIkController == null)
            {
                WarnMissingReferenceOnce(
                    nameof(legIkController)
                );
            }

            if (cameraOrbit == null)
            {
                WarnMissingReferenceOnce(nameof(cameraOrbit));
            }

            if (armButton == null)
            {
                WarnMissingReferenceOnce(nameof(armButton));
            }

            if (disarmButton == null)
            {
                WarnMissingReferenceOnce(nameof(disarmButton));
            }

            if (armStateText == null)
            {
                WarnMissingReferenceOnce(nameof(armStateText));
            }

            if (enableLegIkButton == null)
            {
                WarnMissingReferenceOnce(
                    nameof(enableLegIkButton)
                );
            }

            if (disableLegIkButton == null)
            {
                WarnMissingReferenceOnce(
                    nameof(disableLegIkButton)
                );
            }

            if (legIkStateText == null)
            {
                WarnMissingReferenceOnce(
                    nameof(legIkStateText)
                );
            }

            if (cameraResetButton == null)
            {
                WarnMissingReferenceOnce(
                    nameof(cameraResetButton)
                );
            }
        }

        private void WarnMissingReferenceOnce(
            string referenceName
        )
        {
            if (!loggedMissingReferences.Add(referenceName))
            {
                return;
            }

            Debug.LogWarning(
                $"[RobotManualControlPanel] " +
                $"Reference is not assigned: {referenceName}.",
                this
            );
        }

        private static void SetButtonInteractable(
            Button button,
            bool interactable
        )
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}