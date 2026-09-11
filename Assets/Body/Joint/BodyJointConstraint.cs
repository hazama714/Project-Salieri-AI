// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Body.Joint
{
    /// <summary>
    /// 実機サーボに対応するTransformを、指定したlocal軸のみに制限する。
    ///
    /// 用途:
    /// - Robot Transform Rig の軸確認
    /// - 関節可動域の制限
    /// - home角への復帰
    /// - 将来のBodyController / IK Solverからの安全な角度適用
    ///
    /// 注意:
    /// このコンポーネントはServoBodyではなく、
    /// シャフト位置に置いたAxis Emptyへ付ける。
    /// </summary>
    [ExecuteAlways]
    public sealed class BodyJointConstraint : MonoBehaviour
    {
        public enum LocalRotationAxis
        {
            X,
            Y,
            Z
        }

        [Header("Joint Identity")]
        [SerializeField]
        private string jointId = "unassigned";

        [Header("Allowed Local Axis")]
        [SerializeField]
        private LocalRotationAxis rotationAxis = LocalRotationAxis.X;

        [Header("Angle Limit")]
        [SerializeField]
        private float minAngle = -90f;

        [SerializeField]
        private float maxAngle = 90f;

        [SerializeField]
        private float homeAngle = 0f;

        [Header("Debug Control")]
        [SerializeField]
        private float targetAngle = 0f;

        [SerializeField]
        private bool applyInEditMode = true;

        [SerializeField]
        private bool logAppliedAngle = false;

        [Header("Captured Base Rotation")]
        [SerializeField]
        private Quaternion baseLocalRotation = Quaternion.identity;

        [SerializeField]
        private bool hasCapturedBaseRotation = false;

        public string JointId => jointId;

        public LocalRotationAxis RotationAxis => rotationAxis;

        public float MinAngle => minAngle;

        public float MaxAngle => maxAngle;

        public float HomeAngle => homeAngle;

        public float TargetAngle => targetAngle;

        public float AppliedAngle { get; private set; }

        private void OnEnable()
        {
            CaptureBaseRotationIfNeeded();

            if (Application.isPlaying || applyInEditMode)
            {
                ApplyTargetAngle();
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying && !applyInEditMode)
            {
                return;
            }

            ApplyTargetAngle();
        }

        private void OnValidate()
        {
            CaptureBaseRotationIfNeeded();

            // Inspector編集中に一時的に
            // minAngle > maxAngle になっても値を書き換えない。
            //
            // 有効な範囲になるまで角度適用を停止する。
            if (!HasValidAngleRange())
            {
                return;
            }

            homeAngle = Mathf.Clamp(
                homeAngle,
                minAngle,
                maxAngle
            );

            targetAngle = Mathf.Clamp(
                targetAngle,
                minAngle,
                maxAngle
            );

            if (Application.isPlaying || applyInEditMode)
            {
                ApplyTargetAngle();
            }
        }

        /// <summary>
        /// ControllerやIK Solverから角度を指定する入口。
        /// 必ずmin/maxへClampされる。
        ///
        /// minAngle > maxAngle の不正状態では
        /// 角度適用を拒否する。
        /// </summary>
        public void SetTargetAngle(float angle)
        {
            if (!HasValidAngleRange())
            {
                Debug.LogError(
                    $"[BodyJointConstraint] Invalid angle range. " +
                    $"Joint={jointId} " +
                    $"Min={minAngle:F1} " +
                    $"Max={maxAngle:F1}",
                    this
                );

                return;
            }

            targetAngle = Mathf.Clamp(
                angle,
                minAngle,
                maxAngle
            );

            ApplyTargetAngle();
        }

        /// <summary>
        /// 現在の角度へ差分を加える。
        /// Debug UIや微調整で使用できる。
        /// </summary>
        public void AddTargetAngle(float deltaAngle)
        {
            SetTargetAngle(
                targetAngle + deltaAngle
            );
        }

        /// <summary>
        /// home角へ戻す。
        /// </summary>
        public void ReturnHome()
        {
            SetTargetAngle(
                homeAngle
            );
        }

        /// <summary>
        /// 現在のTransform.localRotationを基準姿勢として保存する。
        /// BlenderからImportした初期姿勢を基準にする場合に使用する。
        /// </summary>
        [ContextMenu("Capture Current Rotation As Base")]
        public void CaptureCurrentRotationAsBase()
        {
            baseLocalRotation =
                transform.localRotation;

            hasCapturedBaseRotation =
                true;

            targetAngle = 0f;
            AppliedAngle = 0f;

            ApplyTargetAngle();

            Debug.Log(
                $"[BodyJointConstraint] " +
                $"Captured base rotation: {jointId}",
                this
            );
        }

        /// <summary>
        /// home角を適用する。
        /// Inspector右上メニューからも実行できる。
        /// </summary>
        [ContextMenu("Apply Home Angle")]
        public void ApplyHomeAngle()
        {
            ReturnHome();
        }

        /// <summary>
        /// 基準姿勢の取得状態を解除する。
        /// 次回OnEnableまたはOnValidateで現在姿勢を再取得する。
        /// </summary>
        [ContextMenu("Reset Base Rotation Capture")]
        public void ResetBaseRotationCapture()
        {
            hasCapturedBaseRotation = false;

            CaptureBaseRotationIfNeeded();

            ApplyTargetAngle();

            Debug.Log(
                $"[BodyJointConstraint] " +
                $"Reset base rotation capture: {jointId}",
                this
            );
        }

        /// <summary>
        /// Angle Limit が有効な数値範囲か確認する。
        ///
        /// minAngle と maxAngle の意味は常に:
        /// minAngle <= maxAngle
        ///
        /// 回転方向はmin/maxの並び順ではなく、
        /// 正負の角度範囲によって表現する。
        ///
        /// 例:
        /// -90 ～ 0  : マイナス方向のみ
        /// 0 ～ 90   : プラス方向のみ
        /// -90 ～ 90 : 両方向
        /// </summary>
        private bool HasValidAngleRange()
        {
            return minAngle <= maxAngle;
        }

        private void CaptureBaseRotationIfNeeded()
        {
            if (hasCapturedBaseRotation)
            {
                return;
            }

            baseLocalRotation =
                transform.localRotation;

            hasCapturedBaseRotation =
                true;
        }

        private void ApplyTargetAngle()
        {
            if (!hasCapturedBaseRotation)
            {
                return;
            }

            // Inspector編集中などで
            // minAngle > maxAngle になっている場合は、
            // Transformへ不正な角度を適用しない。
            if (!HasValidAngleRange())
            {
                return;
            }

            float clampedAngle =
                Mathf.Clamp(
                    targetAngle,
                    minAngle,
                    maxAngle
                );

            Vector3 localAxis =
                GetLocalAxisVector(
                    rotationAxis
                );

            transform.localRotation =
                baseLocalRotation *
                Quaternion.AngleAxis(
                    clampedAngle,
                    localAxis
                );

            AppliedAngle =
                clampedAngle;

            if (logAppliedAngle)
            {
                Debug.Log(
                    $"[BodyJointConstraint] {jointId} " +
                    $"Axis={rotationAxis} " +
                    $"Angle={AppliedAngle:F1}",
                    this
                );
            }
        }

        private static Vector3 GetLocalAxisVector(
            LocalRotationAxis axis
        )
        {
            switch (axis)
            {
                case LocalRotationAxis.X:
                    return Vector3.right;

                case LocalRotationAxis.Y:
                    return Vector3.up;

                case LocalRotationAxis.Z:
                    return Vector3.forward;

                default:
                    return Vector3.right;
            }
        }
    }
}