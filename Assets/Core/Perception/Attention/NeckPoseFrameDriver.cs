// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Core.Perception.Attention;

/// <summary>
/// Estimates a sensor frame from the current software-commanded neck pose, or
/// preserves a fixed sensor pose. This does not command the physical neck and
/// does not report measured physical feedback.
/// </summary>
public sealed class NeckPoseFrameDriver : MonoBehaviour
{
    [Header("Authority")]
    [Tooltip(
        "EstimatedFromNeck follows the current software-commanded neck pose. " +
        "Fixed preserves the frame rotations captured at Play start.")]
    [SerializeField]
    private SensorPoseAuthority sensorPoseAuthority =
        SensorPoseAuthority.EstimatedFromNeck;

    [Header("Source")]
    [SerializeField] private NeckController neckController;

    [Header("Frames")]
    [SerializeField] private Transform neckYawFrame;
    [SerializeField] private Transform neckPitchFrame;

    [Header("Axis Calibration")]
    [Tooltip("Use 1 or -1 to match the physical yaw direction.")]
    [SerializeField] private float yawSign = 1f;

    [Tooltip("Use 1 or -1 to match the physical pitch direction.")]
    [SerializeField] private float pitchSign = 1f;

    [Header("Runtime State (Read Only)")]
    [SerializeField] private bool fixedPoseCaptured;
    [SerializeField] private Quaternion fixedYawLocalRotation =
        Quaternion.identity;
    [SerializeField] private Quaternion fixedPitchLocalRotation =
        Quaternion.identity;

    public SensorPoseAuthority Authority => sensorPoseAuthority;
    public Quaternion FixedYawLocalRotation => fixedYawLocalRotation;
    public Quaternion FixedPitchLocalRotation => fixedPitchLocalRotation;
    public Quaternion CurrentYawLocalRotation => neckYawFrame != null
        ? neckYawFrame.localRotation
        : Quaternion.identity;
    public Quaternion CurrentPitchLocalRotation => neckPitchFrame != null
        ? neckPitchFrame.localRotation
        : Quaternion.identity;
    public NeckOrientation EstimatedNeckOrientation { get; private set; } =
        NeckOrientation.Zero;
    public bool HasEstimatedNeckOrientation { get; private set; }

    private void Awake()
    {
        CaptureFixedPose();
    }

    private void OnEnable()
    {
        if (!fixedPoseCaptured)
            CaptureFixedPose();
    }

    private void LateUpdate()
    {
        if (neckYawFrame == null ||
            neckPitchFrame == null)
        {
            return;
        }

        if (sensorPoseAuthority == SensorPoseAuthority.Fixed)
        {
            if (!fixedPoseCaptured)
                CaptureFixedPose();

            neckYawFrame.localRotation = fixedYawLocalRotation;
            neckPitchFrame.localRotation = fixedPitchLocalRotation;
            EstimatedNeckOrientation = NeckOrientation.Zero;
            HasEstimatedNeckOrientation = false;
            return;
        }

        if (neckController == null)
        {
            EstimatedNeckOrientation = NeckOrientation.Zero;
            HasEstimatedNeckOrientation = false;
            return;
        }

        float yaw =
            neckController.CommandedYawRelativeAngle * Mathf.Sign(yawSign);

        float pitch =
            neckController.CommandedPitchRelativeAngle * Mathf.Sign(pitchSign);

        // The current physical neck has two actuated axes. Roll is explicitly
        // represented as zero so this boundary can extend to a three-axis neck
        // without redefining the orientation contract.
        EstimatedNeckOrientation = new NeckOrientation(yaw, pitch, 0f);
        HasEstimatedNeckOrientation = true;

        neckYawFrame.localRotation = Quaternion.Euler(0f, yaw, 0f);
        neckPitchFrame.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    [ContextMenu("Recapture Fixed Sensor Pose")]
    public void CaptureFixedPose()
    {
        if (neckYawFrame == null || neckPitchFrame == null)
        {
            fixedPoseCaptured = false;
            return;
        }

        fixedYawLocalRotation = neckYawFrame.localRotation;
        fixedPitchLocalRotation = neckPitchFrame.localRotation;
        fixedPoseCaptured = true;
    }
}
