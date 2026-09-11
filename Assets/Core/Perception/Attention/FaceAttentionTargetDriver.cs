// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Core.Perception.Buffer;

/// <summary>
/// Converts the stable face center into a body-relative direction and places
/// AttentionTarget at a provisional fixed distance.
/// This component does not move the physical neck or arms.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class FaceAttentionTargetDriver : MonoBehaviour
{
    [Header("Perception")]
    [SerializeField] private FaceDetector_OpenCV detector;
    [SerializeField] private FacePerceptionBuffer perceptionBuffer;

    [Header("Frames")]
    [SerializeField] private Transform robotBodyFrame;
    [SerializeField] private Transform sensorCameraFrame;
    [SerializeField] private Transform attentionTarget;

    [Header("Projection")]
    [Range(1f, 179f)]
    [SerializeField] private float verticalFieldOfView = 60f;

    [Min(0.01f)]
    [SerializeField] private float fixedDistance = 1f;

    [Tooltip("Enable only when the detection image is horizontally mirrored.")]
    [SerializeField] private bool mirrorX;

    [Tooltip("Enable only when the detection image is vertically mirrored after OpenCV rotation.")]
    [SerializeField] private bool mirrorY;

    [Header("Display")]
    [SerializeField] private bool hideWhenFullyLost = true;

    public Vector2 ScreenPosition { get; private set; }
    public Vector3 CameraRelativeDirection { get; private set; }
    public Vector3 BodyRelativeDirection { get; private set; }
    public Vector3 EstimatedBodyPosition { get; private set; }

    public bool IsDetected { get; private set; }
    public bool IsCentered { get; private set; }
    public bool HasValidTarget { get; private set; }

    private const float CenterTolerance = 0.05f;

    private void Start()
    {
        if (hideWhenFullyLost &&
            attentionTarget != null &&
            !HasValidTarget)
        {
            attentionTarget.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (!HasRequiredReferences())
            return;

        if (detector.HasFace && perceptionBuffer.IsStableFound)
        {
            UpdateTargetFromFace();
            return;
        }

        IsDetected = false;
        IsCentered = false;

        // Temporary loss keeps the last valid position.
        if (!perceptionBuffer.IsFullyLost)
            return;

        HasValidTarget = false;

        if (hideWhenFullyLost &&
            attentionTarget.gameObject.activeSelf)
        {
            attentionTarget.gameObject.SetActive(false);
        }
    }

    private void UpdateTargetFromFace()
    {
        int frameWidth = detector.FrameWidth;
        int frameHeight = detector.FrameHeight;

        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        float viewportX =
            Mathf.Clamp01(detector.FaceCenterX / frameWidth);

        // OpenCV uses a top-left origin. Unity-style viewport Y is bottom-up.
        float viewportY =
            Mathf.Clamp01(1f - detector.FaceCenterY / frameHeight);

        if (mirrorX)
            viewportX = 1f - viewportX;

        if (mirrorY)
            viewportY = 1f - viewportY;

        ScreenPosition = new Vector2(viewportX, viewportY);

        CameraRelativeDirection =
            BuildCameraDirection(viewportX, viewportY, frameWidth, frameHeight);

        Vector3 worldDirection =
            sensorCameraFrame.TransformDirection(CameraRelativeDirection).normalized;

        BodyRelativeDirection =
            robotBodyFrame.InverseTransformDirection(worldDirection).normalized;

        Vector3 worldPoint =
            sensorCameraFrame.position + worldDirection * fixedDistance;

        EstimatedBodyPosition =
            robotBodyFrame.InverseTransformPoint(worldPoint);

        if (!attentionTarget.gameObject.activeSelf)
            attentionTarget.gameObject.SetActive(true);

        attentionTarget.position = worldPoint;

        IsDetected = true;
        IsCentered =
            Mathf.Abs(viewportX - 0.5f) <= CenterTolerance &&
            Mathf.Abs(viewportY - 0.5f) <= CenterTolerance;

        HasValidTarget = true;
    }

    private Vector3 BuildCameraDirection(
        float viewportX,
        float viewportY,
        int frameWidth,
        int frameHeight)
    {
        float normalizedX = viewportX * 2f - 1f;
        float normalizedY = viewportY * 2f - 1f;

        float aspect = (float)frameWidth / frameHeight;
        float tanHalfVerticalFov =
            Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);

        return new Vector3(
            normalizedX * aspect * tanHalfVerticalFov,
            normalizedY * tanHalfVerticalFov,
            1f
        ).normalized;
    }

    private bool HasRequiredReferences()
    {
        return detector != null &&
               perceptionBuffer != null &&
               robotBodyFrame != null &&
               sensorCameraFrame != null &&
               attentionTarget != null;
    }
}
