// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Core.Perception.Attention
{
    /// <summary>
    /// Converts the final orientation target produced by
    /// OrientationResolutionTargetDriver into body-relative neck yaw/pitch.
    ///
    /// This component only calculates angles.
    /// It does not call NeckController, perform Limbo/mode checks,
    /// or send commands to physical servos.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class OrientationResolutionNeckAngleConverter :
        MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private OrientationResolutionTargetDriver targetDriver;

        [Header("Frames")]
        [SerializeField]
        private Transform robotBodyFrame;

        [SerializeField]
        private Transform sensorCameraFrame;

        [Header("Range")]
        [SerializeField]
        [Min(0f)]
        private float yawRange = 90f;

        [SerializeField]
        [Min(0f)]
        private float pitchRange = 30f;

        [Header("Direction")]
        [SerializeField]
        private bool invertYaw = true;

        [SerializeField]
        private bool invertPitch;

        [Header("Startup Guard")]
        [SerializeField]
        [Min(0f)]
        private float startupDelay = 3f;

        [Header("Smoothing")]
        [SerializeField]
        [Min(0f)]
        private float smoothSpeed = 4f;

        [Header("Update Control")]
        [SerializeField]
        [Min(0.001f)]
        private float updateInterval = 0.1f;

        [SerializeField]
        [Min(0f)]
        private float minAngleDelta = 2f;

        [Header("Safety")]
        [SerializeField]
        [Min(0f)]
        private float maxStepPerUpdate = 3f;

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLog = true;

        [Header("Runtime State (Read Only)")]
        [SerializeField]
        private bool hasValidAngles;

        [SerializeField]
        private OrientationResolutionKind sourceKind =
            OrientationResolutionKind.None;

        [SerializeField]
        private string sourceTargetKey = string.Empty;

        [SerializeField]
        private float calculatedYaw;

        [SerializeField]
        private float calculatedPitch;

        [SerializeField]
        private float smoothedYaw;

        [SerializeField]
        private float smoothedPitch;

        [SerializeField]
        private float outputYaw;

        [SerializeField]
        private float outputPitch;

        private float updateTimer;
        private float startTime;

        private bool hasLoggedState;
        private bool lastLoggedValid;
        private OrientationResolutionKind lastLoggedKind =
            OrientationResolutionKind.None;
        private string lastLoggedTargetKey = string.Empty;

        private const float MinimumDirectionSqrMagnitude =
            0.000001f;

        public bool HasValidAngles =>
            hasValidAngles;

        public OrientationResolutionKind SourceKind =>
            sourceKind;

        public string SourceTargetKey =>
            sourceTargetKey;

        public float CalculatedYaw =>
            calculatedYaw;

        public float CalculatedPitch =>
            calculatedPitch;

        public float OutputYaw =>
            outputYaw;

        public float OutputPitch =>
            outputPitch;

        private void OnEnable()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            startTime = Time.time;
            updateTimer = 0f;

            hasValidAngles = false;
            sourceKind = OrientationResolutionKind.None;
            sourceTargetKey = string.Empty;

            calculatedYaw = 0f;
            calculatedPitch = 0f;
            smoothedYaw = 0f;
            smoothedPitch = 0f;
            outputYaw = 0f;
            outputPitch = 0f;

            hasLoggedState = false;
        }

        private void LateUpdate()
        {
            if (targetDriver == null ||
                robotBodyFrame == null ||
                sensorCameraFrame == null)
            {
                return;
            }

            if (Time.time - startTime < startupDelay)
            {
                SetUnavailable(
                    OrientationResolutionKind.None,
                    string.Empty
                );

                return;
            }

            OrientationResolutionKind kind =
                targetDriver.OutputKind;

            string targetKey =
                targetDriver.OutputTargetKey ??
                string.Empty;

            if (!CanCalculate(kind) ||
                !targetDriver.HasOutputPosition)
            {
                SetUnavailable(
                    kind,
                    targetKey
                );

                return;
            }

            Vector3 worldDirection =
                targetDriver.OutputWorldPosition -
                sensorCameraFrame.position;

            if (worldDirection.sqrMagnitude <
                MinimumDirectionSqrMagnitude)
            {
                SetUnavailable(
                    kind,
                    targetKey
                );

                return;
            }

            Vector3 bodyDirection =
                robotBodyFrame
                    .InverseTransformDirection(
                        worldDirection
                    )
                    .normalized;

            calculatedYaw =
                Mathf.Atan2(
                    bodyDirection.x,
                    bodyDirection.z
                ) * Mathf.Rad2Deg;

            float horizontal =
                Mathf.Sqrt(
                    bodyDirection.x *
                    bodyDirection.x +
                    bodyDirection.z *
                    bodyDirection.z
                );

            calculatedPitch =
                Mathf.Atan2(
                    bodyDirection.y,
                    horizontal
                ) * Mathf.Rad2Deg;

            if (invertYaw)
                calculatedYaw *= -1f;

            if (invertPitch)
                calculatedPitch *= -1f;

            calculatedYaw =
                Mathf.Clamp(
                    calculatedYaw,
                    -yawRange,
                    yawRange
                );

            calculatedPitch =
                Mathf.Clamp(
                    calculatedPitch,
                    -pitchRange,
                    pitchRange
                );

            bool becameValid =
                !hasValidAngles;

            hasValidAngles = true;
            sourceKind = kind;
            sourceTargetKey = targetKey;

            LogStateChangeIfNeeded();

            if (becameValid)
                updateTimer = updateInterval;

            updateTimer += Time.deltaTime;

            if (updateTimer < updateInterval)
                return;

            updateTimer = 0f;

            float yawLerp =
                Mathf.Lerp(
                    smoothedYaw,
                    calculatedYaw,
                    Time.deltaTime *
                    smoothSpeed
                );

            float pitchLerp =
                Mathf.Lerp(
                    smoothedPitch,
                    calculatedPitch,
                    Time.deltaTime *
                    smoothSpeed
                );

            smoothedYaw =
                Mathf.MoveTowards(
                    smoothedYaw,
                    yawLerp,
                    maxStepPerUpdate
                );

            smoothedPitch =
                Mathf.MoveTowards(
                    smoothedPitch,
                    pitchLerp,
                    maxStepPerUpdate
                );

            bool changed =
                Mathf.Abs(
                    smoothedYaw -
                    outputYaw
                ) >= minAngleDelta ||
                Mathf.Abs(
                    smoothedPitch -
                    outputPitch
                ) >= minAngleDelta;

            if (!changed)
                return;

            outputYaw = smoothedYaw;
            outputPitch = smoothedPitch;

            if (enableDebugLog)
            {
                Debug.Log(
                    "[OrientationResolutionNeckAngleConverter]" +
                    "[ANGLE_UPDATED] " +
                    $"Kind={sourceKind} " +
                    $"Target=" +
                    $"{(sourceTargetKey.Length > 0 ? sourceTargetKey : "none")} " +
                    $"CalculatedYaw={calculatedYaw:F1} " +
                    $"CalculatedPitch={calculatedPitch:F1} " +
                    $"OutputYaw={outputYaw:F1} " +
                    $"OutputPitch={outputPitch:F1}",
                    this
                );
            }
        }

        private static bool CanCalculate(
            OrientationResolutionKind kind)
        {
            switch (kind)
            {
                case OrientationResolutionKind.Face:
                case OrientationResolutionKind.Object:
                case OrientationResolutionKind.HoldPrevious:
                case OrientationResolutionKind.AttentionContinuityHold:
                case OrientationResolutionKind.TargetDirection:
                case OrientationResolutionKind.Neutral:
                    return true;

                case OrientationResolutionKind.None:
                case OrientationResolutionKind.AttentionContinuityExpired:
                default:
                    return false;
            }
        }

        private void SetUnavailable(
            OrientationResolutionKind kind,
            string targetKey)
        {
            hasValidAngles = false;
            sourceKind = kind;
            sourceTargetKey =
                targetKey ??
                string.Empty;

            updateTimer = 0f;

            LogStateChangeIfNeeded();
        }

        private void LogStateChangeIfNeeded()
        {
            bool changed =
                !hasLoggedState ||
                hasValidAngles !=
                    lastLoggedValid ||
                sourceKind !=
                    lastLoggedKind ||
                !string.Equals(
                    sourceTargetKey,
                    lastLoggedTargetKey,
                    System.StringComparison.Ordinal
                );

            if (!changed)
                return;

            hasLoggedState = true;
            lastLoggedValid =
                hasValidAngles;
            lastLoggedKind =
                sourceKind;
            lastLoggedTargetKey =
                sourceTargetKey;

            if (!enableDebugLog)
                return;

            Debug.Log(
                "[OrientationResolutionNeckAngleConverter]" +
                "[STATE_CHANGED] " +
                $"Valid={hasValidAngles} " +
                $"Kind={sourceKind} " +
                $"Target=" +
                $"{(sourceTargetKey.Length > 0 ? sourceTargetKey : "none")}",
                this
            );
        }

        private bool ValidateConfiguration()
        {
            bool valid = true;

            if (targetDriver == null)
            {
                Debug.LogError(
                    "[OrientationResolutionNeckAngleConverter]" +
                    "[ERROR] " +
                    "OrientationResolutionTargetDriver is not assigned.",
                    this
                );

                valid = false;
            }

            if (robotBodyFrame == null)
            {
                Debug.LogError(
                    "[OrientationResolutionNeckAngleConverter]" +
                    "[ERROR] " +
                    "RobotBodyFrame is not assigned.",
                    this
                );

                valid = false;
            }

            if (sensorCameraFrame == null)
            {
                Debug.LogError(
                    "[OrientationResolutionNeckAngleConverter]" +
                    "[ERROR] " +
                    "SensorCameraFrame is not assigned.",
                    this
                );

                valid = false;
            }

            return valid;
        }

        private void OnValidate()
        {
            yawRange =
                Mathf.Max(
                    0f,
                    yawRange
                );

            pitchRange =
                Mathf.Max(
                    0f,
                    pitchRange
                );

            startupDelay =
                Mathf.Max(
                    0f,
                    startupDelay
                );

            smoothSpeed =
                Mathf.Max(
                    0f,
                    smoothSpeed
                );

            updateInterval =
                Mathf.Max(
                    0.001f,
                    updateInterval
                );

            minAngleDelta =
                Mathf.Max(
                    0f,
                    minAngleDelta
                );

            maxStepPerUpdate =
                Mathf.Max(
                    0f,
                    maxStepPerUpdate
                );
        }
    }
}
