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
    /// OrientationTargetResolverが決定した抽象的な注視方針を、
    /// Scene上の共有AttentionTarget位置へ変換する。
    ///
    /// このクラスは注視対象の優先判定、首角度計算、VRM操作、
    /// Limbo判定、実機サーボ送信を行わない。
    /// </summary>
    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class OrientationResolutionTargetDriver :
        MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private OrientationTargetResolver resolver;

        [Header("Frames")]
        [SerializeField]
        private Transform robotBodyFrame;

        [SerializeField]
        private Transform sensorCameraFrame;

        [Header("Output")]
        [SerializeField]
        private Transform attentionTarget;

        [Header("Square Projection")]
        [Tooltip(
            "正規化座標を投影する正方形平面の半幅・半高さ。"
        )]
        [SerializeField]
        [Min(0.01f)]
        private float projectionHalfExtent = 1f;

        [Tooltip(
            "SensorCameraFrameからAttentionTargetまでの仮距離。"
        )]
        [SerializeField]
        [Min(0.01f)]
        private float fixedDistance = 0.5f;

        [Header("Object Fresh Observation Motion Step")]
        [Tooltip(
            "Maximum body-relative gaze correction authorized by one fresh " +
            "Object SourceFrame. Initial conservative value; not hardware " +
            "calibration."
        )]
        [SerializeField]
        [Min(0.1f)]
        private float maximumObjectMotionStepDegrees = 5f;

        [Header("None Handling")]
        [Tooltip(
            "初回または明示的なDriver停止時の互換動作。" +
            "追跡Lost時は最後のTarget位置を保持して非表示にする。"
        )]
        [SerializeField]
        private bool hideWhenNone = true;

        [Header("Debug")]
        [SerializeField]
        private bool logSemanticChanges = true;

        [Header("Runtime State (Read Only)")]
        [SerializeField]
        private OrientationResolutionKind outputKind =
            OrientationResolutionKind.None;

        [SerializeField]
        private string outputTargetKey = string.Empty;

        [SerializeField]
        private Vector2 outputCenter =
            new Vector2(0.5f, 0.5f);

        [SerializeField]
        private Vector3 outputWorldPosition;

        [SerializeField]
        private Vector3 outputBodyPosition;

        [SerializeField]
        private bool hasOutputPosition;

        [SerializeField]
        private string outputReason = string.Empty;

        private OrientationResolutionKind
            lastLoggedKind =
                OrientationResolutionKind.None;

        private string lastLoggedTargetKey =
            string.Empty;

        private bool hasLoggedState;

        private readonly ObjectObservationMotionStepGate
            objectMotionStepGate =
                new ObjectObservationMotionStepGate();

        private string lastLoggedHoldTargetKey = string.Empty;
        private long lastLoggedHoldSourceFrameId;
        private string lastLoggedInvalidTargetKey = string.Empty;
        private long lastLoggedInvalidSourceFrameId;

        public OrientationResolutionKind OutputKind =>
            outputKind;

        public string OutputTargetKey =>
            outputTargetKey;

        public Vector2 OutputCenter =>
            outputCenter;

        public Vector3 OutputWorldPosition =>
            outputWorldPosition;

        public Vector3 OutputBodyPosition =>
            outputBodyPosition;

        public bool HasOutputPosition =>
            hasOutputPosition;

        public float MaximumObjectMotionStepDegrees =>
            maximumObjectMotionStepDegrees;

        public long LastAcceptedObjectSourceFrameId =>
            objectMotionStepGate.LastSourceFrameId;

        public int AcceptedObjectMotionStepCount =>
            objectMotionStepGate.FreshStepCount;

        /// <summary>
        /// Returns the current gaze direction relative to RobotBodyFrame.
        /// The sensor-to-target vector is used deliberately; OutputBodyPosition
        /// also contains the sensor height and is not a direction.
        /// </summary>
        public bool TryGetCurrentBodyDirectionAngles(
            out float yawDegrees,
            out float pitchDegrees)
        {
            yawDegrees = 0f;
            pitchDegrees = 0f;
            return hasOutputPosition &&
                attentionTarget != null &&
                sensorCameraFrame != null &&
                robotBodyFrame != null &&
                OrientationTargetDirectionMath.TryCalculateBodyRelativeAngles(
                    sensorCameraFrame.position,
                    attentionTarget.position,
                    robotBodyFrame.rotation,
                    out yawDegrees,
                    out pitchDegrees);
        }

        private void OnEnable()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            ApplyNone(
                "Driver enabled before first resolution",
                preserveLastPose: false
            );

            ResetObjectMotionStepGate();
        }

        private void LateUpdate()
        {
            if (resolver == null)
                return;

            ApplyResolution(
                resolver.CurrentResolution
            );
        }

        private void OnDisable()
        {
            if (attentionTarget == null ||
                robotBodyFrame == null ||
                sensorCameraFrame == null)
            {
                return;
            }

            ApplyNone(
                "Orientation resolution target driver disabled",
                preserveLastPose: false
            );

            ResetObjectMotionStepGate();
        }

        private void OnValidate()
        {
            projectionHalfExtent =
                Mathf.Max(
                    0.01f,
                    projectionHalfExtent
                );

            fixedDistance =
                Mathf.Max(
                    0.01f,
                    fixedDistance
                );

            maximumObjectMotionStepDegrees =
                Mathf.Max(
                    0.1f,
                    maximumObjectMotionStepDegrees
                );
        }

        private bool ValidateConfiguration()
        {
            bool valid = true;

            if (resolver == null)
            {
                Debug.LogError(
                    "[OrientationResolutionTargetDriver][ERROR] " +
                    "OrientationTargetResolver is not assigned.",
                    this
                );

                valid = false;
            }

            if (robotBodyFrame == null)
            {
                Debug.LogError(
                    "[OrientationResolutionTargetDriver][ERROR] " +
                    "RobotBodyFrame is not assigned.",
                    this
                );

                valid = false;
            }

            if (sensorCameraFrame == null)
            {
                Debug.LogError(
                    "[OrientationResolutionTargetDriver][ERROR] " +
                    "SensorCameraFrame is not assigned.",
                    this
                );

                valid = false;
            }

            if (attentionTarget == null)
            {
                Debug.LogError(
                    "[OrientationResolutionTargetDriver][ERROR] " +
                    "AttentionTarget is not assigned.",
                    this
                );

                valid = false;
            }

            return valid;
        }

        private void ApplyResolution(
            OrientationResolution resolution)
        {
            if (resolution == null ||
                !resolution.IsValid)
            {
                ApplyNone(
                    "Resolution is null or invalid"
                );

                return;
            }

            switch (resolution.Kind)
            {
                case OrientationResolutionKind.Face:
                case OrientationResolutionKind.Object:
                    ApplyTargetResolution(
                        resolution
                    );
                    break;

                case OrientationResolutionKind.HoldPrevious:
                case OrientationResolutionKind.AttentionContinuityHold:
                    ApplyHoldPrevious(
                        resolution
                    );
                    break;

                case OrientationResolutionKind.TargetDirection:
                    ApplyTargetDirection(
                        resolution
                    );
                    break;

                case OrientationResolutionKind.Neutral:
                    ApplyNeutral(
                        resolution.Reason
                    );
                    break;

                case OrientationResolutionKind.None:
                    ApplyNone(
                        resolution.Reason
                    );
                    break;

                case OrientationResolutionKind.AttentionContinuityExpired:
                    ApplyUnavailable(
                        OrientationResolutionKind
                            .AttentionContinuityExpired,
                        resolution.Reason);
                    break;

                default:
                    ApplyNone(
                        "Unknown resolution kind"
                    );
                    break;
            }
        }

        private void ApplyTargetResolution(
            OrientationResolution resolution)
        {
            OrientationTargetCandidate candidate =
                resolution.Target;

            if (candidate == null ||
                !candidate.IsValid)
            {
                ApplyNone(
                    "Target candidate is invalid"
                );

                return;
            }

            if (candidate.Kind ==
                OrientationTargetCandidateKind.Object)
            {
                ApplyObjectTargetResolution(
                    resolution,
                    candidate
                );
                return;
            }

            float centerX =
                Mathf.Clamp01(
                    candidate.CenterX
                );

            float centerY =
                Mathf.Clamp01(
                    candidate.CenterY
                );

            Vector3 cameraDirection =
                BuildSquareCameraDirection(
                    centerX,
                    centerY
                );

            Vector3 worldDirection =
                sensorCameraFrame
                    .TransformDirection(
                        cameraDirection
                    )
                    .normalized;

            Vector3 worldPosition =
                sensorCameraFrame.position +
                worldDirection *
                fixedDistance;

            SetAttentionTargetPosition(
                worldPosition,
                active: true
            );

            outputKind =
                resolution.Kind;

            outputTargetKey =
                candidate.TargetKey ??
                string.Empty;

            outputCenter =
                new Vector2(
                    centerX,
                    centerY
                );

            outputReason =
                resolution.Reason ??
                string.Empty;

            RebaseObjectMotionStepGateToCurrentOutput();

            LogSemanticChangeIfNeeded();
        }

        private void ApplyHoldPrevious(
            OrientationResolution resolution)
        {
            if (!hasOutputPosition)
            {
                // 起動直後など、保持できる出力がまだない場合だけ、
                // Resolution内の対象を初期位置として使用する。
                ApplyTargetResolution(
                    resolution
                );

                outputKind =
                    resolution.Kind;

                LogSemanticChangeIfNeeded();
                return;
            }

            Vector3 heldWorldPosition =
                robotBodyFrame.TransformPoint(
                    outputBodyPosition
                );

            SetAttentionTargetPosition(
                heldWorldPosition,
                active: true
            );

            outputKind =
                resolution.Kind;

            outputTargetKey =
                resolution.HasTarget
                    ? resolution.Target.TargetKey ??
                      string.Empty
                    : outputTargetKey;

            outputReason =
                resolution.Reason ??
                string.Empty;

            LogSemanticChangeIfNeeded();
        }

        private void ApplyObjectTargetResolution(
            OrientationResolution resolution,
            OrientationTargetCandidate candidate)
        {
            float centerX = Mathf.Clamp01(candidate.CenterX);
            float centerY = Mathf.Clamp01(candidate.CenterY);

            Vector3 requestedCameraDirection =
                BuildSquareCameraDirection(centerX, centerY);
            Vector3 requestedWorldDirection =
                sensorCameraFrame.TransformDirection(
                    requestedCameraDirection).normalized;
            Vector3 requestedBodyDirection =
                robotBodyFrame.InverseTransformDirection(
                    requestedWorldDirection).normalized;
            Vector3 initialBodyDirection =
                GetCurrentOrNeutralBodyDirection();

            ObjectObservationMotionStepDecision decision =
                objectMotionStepGate.Evaluate(
                    candidate.TargetKey,
                    candidate.SourceFrameId,
                    requestedBodyDirection,
                    initialBodyDirection,
                    maximumObjectMotionStepDegrees,
                    out Vector3 acceptedBodyDirection,
                    out string decisionReason);

            if (decision ==
                ObjectObservationMotionStepDecision.InvalidObservation)
            {
                ApplyInvalidObjectObservation(
                    candidate,
                    resolution,
                    decisionReason);
                return;
            }

            Vector3 acceptedWorldDirection =
                robotBodyFrame.TransformDirection(
                    acceptedBodyDirection).normalized;
            Vector3 acceptedWorldPosition =
                sensorCameraFrame.position +
                acceptedWorldDirection * fixedDistance;

            SetAttentionTargetPosition(
                acceptedWorldPosition,
                active: true
            );

            outputKind = resolution.Kind;
            outputTargetKey = candidate.TargetKey ?? string.Empty;
            outputCenter = new Vector2(centerX, centerY);
            outputReason = resolution.Reason ?? string.Empty;

            if (decision ==
                ObjectObservationMotionStepDecision.FreshStepAccepted)
            {
                lastLoggedHoldTargetKey = string.Empty;
                lastLoggedHoldSourceFrameId = 0;
                lastLoggedInvalidTargetKey = string.Empty;
                lastLoggedInvalidSourceFrameId = 0;

                Debug.Log(
                    "[MotionStepGate][FRESH] " +
                    "SourceFrame=" + candidate.SourceFrameId + " " +
                    "Target=" + outputTargetKey + " " +
                    "StepAccepted=true " +
                    "MaxStepDegrees=" +
                    maximumObjectMotionStepDegrees.ToString("F2"),
                    this);
            }
            else
            {
                LogObjectMotionHoldOnce(
                    candidate,
                    decisionReason);
            }

            LogSemanticChangeIfNeeded();
        }

        private void ApplyInvalidObjectObservation(
            OrientationTargetCandidate candidate,
            OrientationResolution resolution,
            string reason)
        {
            if (hasOutputPosition)
            {
                Vector3 heldWorldPosition =
                    robotBodyFrame.TransformPoint(outputBodyPosition);
                SetAttentionTargetPosition(
                    heldWorldPosition,
                    active: true);
                outputKind = resolution.Kind;
                outputTargetKey = candidate.TargetKey ?? string.Empty;
                outputReason = reason ?? string.Empty;
            }
            else
            {
                ApplyNone(
                    reason ?? "Object observation provenance is invalid",
                    preserveLastPose: false);
            }

            bool alreadyLogged =
                lastLoggedInvalidSourceFrameId == candidate.SourceFrameId &&
                string.Equals(
                    lastLoggedInvalidTargetKey,
                    candidate.TargetKey,
                    System.StringComparison.Ordinal);
            if (!alreadyLogged)
            {
                lastLoggedInvalidTargetKey = candidate.TargetKey ?? string.Empty;
                lastLoggedInvalidSourceFrameId = candidate.SourceFrameId;
                Debug.LogWarning(
                    "[MotionStepGate][HOLD] " +
                    "SourceFrame=" + candidate.SourceFrameId + " " +
                    "Target=" + (candidate.TargetKey ?? string.Empty) + " " +
                    "Reason=InvalidObservationProvenance",
                    this);
            }

            LogSemanticChangeIfNeeded();
        }

        private Vector3 GetCurrentOrNeutralBodyDirection()
        {
            if (hasOutputPosition &&
                attentionTarget != null &&
                sensorCameraFrame != null)
            {
                Vector3 currentWorldDirection =
                    attentionTarget.position -
                    sensorCameraFrame.position;
                if (currentWorldDirection.sqrMagnitude >= 0.000001f)
                {
                    return robotBodyFrame
                        .InverseTransformDirection(
                            currentWorldDirection)
                        .normalized;
                }
            }

            return Vector3.forward;
        }

        private void LogObjectMotionHoldOnce(
            OrientationTargetCandidate candidate,
            string reason)
        {
            bool alreadyLogged =
                lastLoggedHoldSourceFrameId == candidate.SourceFrameId &&
                string.Equals(
                    lastLoggedHoldTargetKey,
                    candidate.TargetKey,
                    System.StringComparison.Ordinal);
            if (alreadyLogged)
                return;

            lastLoggedHoldTargetKey = candidate.TargetKey ?? string.Empty;
            lastLoggedHoldSourceFrameId = candidate.SourceFrameId;
            Debug.Log(
                "[MotionStepGate][HOLD] " +
                "SourceFrame=" + candidate.SourceFrameId + " " +
                "Target=" + lastLoggedHoldTargetKey + " " +
                "Reason=AwaitFreshObservation " +
                "Detail=" + (reason ?? string.Empty),
                this);
        }

        private void ResetObjectMotionStepGate()
        {
            objectMotionStepGate.Reset();
            lastLoggedHoldTargetKey = string.Empty;
            lastLoggedHoldSourceFrameId = 0;
            lastLoggedInvalidTargetKey = string.Empty;
            lastLoggedInvalidSourceFrameId = 0;
        }

        private void RebaseObjectMotionStepGateToCurrentOutput()
        {
            objectMotionStepGate.TryRebaseAcceptedDirection(
                GetCurrentOrNeutralBodyDirection());
        }

        private void ApplyTargetDirection(
            OrientationResolution resolution)
        {
            OrientationTargetDirectionRequest target =
                resolution.PriorityRequest != null
                    ? resolution.PriorityRequest.TargetDirection
                    : null;

            if (target == null || !target.IsValid)
            {
                ApplyNone("Target direction request is invalid");
                return;
            }

            if (!OrientationTargetDirectionMath.TryBuildWorldPosition(
                    sensorCameraFrame.position,
                    robotBodyFrame.rotation,
                    target.BodyRelativeYawDegrees,
                    target.BodyRelativePitchDegrees,
                    fixedDistance,
                    out Vector3 worldPosition))
            {
                ApplyNone("Target direction conversion failed");
                return;
            }

            SetAttentionTargetPosition(worldPosition, active: true);

            outputKind = OrientationResolutionKind.TargetDirection;
            outputTargetKey = target.TargetKey ?? string.Empty;
            outputCenter = new Vector2(0.5f, 0.5f);
            outputReason = resolution.Reason ?? string.Empty;

            RebaseObjectMotionStepGateToCurrentOutput();

            LogSemanticChangeIfNeeded();
        }

        private void ApplyNeutral(
            string reason)
        {
            Vector3 worldPosition =
                BuildNeutralWorldPosition();

            SetAttentionTargetPosition(
                worldPosition,
                active: true
            );

            outputKind =
                OrientationResolutionKind.Neutral;

            outputTargetKey =
                string.Empty;

            outputCenter =
                new Vector2(
                    0.5f,
                    0.5f
                );

            outputReason =
                reason ??
                string.Empty;

            RebaseObjectMotionStepGateToCurrentOutput();

            LogSemanticChangeIfNeeded();
        }

        private void ApplyNone(
            string reason,
            bool preserveLastPose = true)
        {
            ApplyUnavailable(
                OrientationResolutionKind.None,
                reason,
                preserveLastPose);
        }

        private void ApplyUnavailable(
            OrientationResolutionKind kind,
            string reason,
            bool preserveLastPose = true)
        {
            if (preserveLastPose && hasOutputPosition)
            {
                // Target loss is not a recenter command. Preserve the last
                // world/body position and deactivate the semantic target so
                // the VRM LookAt layer can hold its last solved pose.
                if (attentionTarget.gameObject.activeSelf)
                    attentionTarget.gameObject.SetActive(false);

                outputKind = kind;
                outputTargetKey = string.Empty;
                outputReason = reason ?? string.Empty;
                hasOutputPosition = false;
                LogSemanticChangeIfNeeded();
                return;
            }

            Vector3 worldPosition =
                BuildNeutralWorldPosition();

            SetAttentionTargetPosition(
                worldPosition,
                active: !hideWhenNone
            );

            outputKind =
                kind;

            outputTargetKey =
                string.Empty;

            outputCenter =
                new Vector2(
                    0.5f,
                    0.5f
                );

            outputReason =
                reason ??
                string.Empty;

            hasOutputPosition = false;

            LogSemanticChangeIfNeeded();
        }

        private Vector3 BuildSquareCameraDirection(
            float centerX,
            float centerY)
        {
            float normalizedX =
                centerX * 2f - 1f;

            float normalizedY =
                centerY * 2f - 1f;

            return new Vector3(
                normalizedX *
                    projectionHalfExtent,
                normalizedY *
                    projectionHalfExtent,
                1f
            ).normalized;
        }

        private Vector3 BuildNeutralWorldPosition()
        {
            Vector3 bodyForward =
                robotBodyFrame.forward;

            if (bodyForward.sqrMagnitude <
                0.000001f)
            {
                bodyForward =
                    Vector3.forward;
            }

            return sensorCameraFrame.position +
                   bodyForward.normalized *
                   fixedDistance;
        }

        private void SetAttentionTargetPosition(
            Vector3 worldPosition,
            bool active)
        {
            attentionTarget.position =
                worldPosition;

            outputWorldPosition =
                worldPosition;

            outputBodyPosition =
                robotBodyFrame.InverseTransformPoint(
                    worldPosition
                );

            hasOutputPosition =
                active;

            if (attentionTarget.gameObject.activeSelf !=
                active)
            {
                attentionTarget.gameObject.SetActive(
                    active
                );
            }
        }

        private void LogSemanticChangeIfNeeded()
        {
            bool changed =
                !hasLoggedState ||
                outputKind != lastLoggedKind ||
                !string.Equals(
                    outputTargetKey,
                    lastLoggedTargetKey,
                    System.StringComparison.Ordinal
                );

            if (!changed)
                return;

            hasLoggedState = true;
            lastLoggedKind =
                outputKind;

            lastLoggedTargetKey =
                outputTargetKey;

            if (!logSemanticChanges)
                return;

            Debug.Log(
                "[OrientationResolutionTargetDriver]" +
                "[OUTPUT_CHANGED] " +
                $"Kind={outputKind} " +
                $"Target=" +
                $"{(outputTargetKey.Length > 0 ? outputTargetKey : "none")} " +
                $"Center=({outputCenter.x:F3}," +
                $"{outputCenter.y:F3}) " +
                $"World={outputWorldPosition} " +
                $"Reason={outputReason}",
                this
            );
        }
    }
}
