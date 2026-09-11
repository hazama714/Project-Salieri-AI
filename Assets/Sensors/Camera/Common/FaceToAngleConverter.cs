// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

/// <summary>
/// Legacy diagnostic angle calculator retained under 99_Debug.
/// Phase 4 does not forward its calculated values to physical output.
/// </summary>
[DefaultExecutionOrder(200)]
public class FaceToAngleConverter : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private FaceAttentionTargetDriver attentionDriver;
    [SerializeField] private Transform attentionTarget;
    [SerializeField] private Transform robotBodyFrame;
    [SerializeField] private Transform sensorCameraFrame;

    [Header("Range")]
    public float yawRange = 60f;
    public float pitchRange = 30f;

    [Header("Direction")]
    public bool invertYaw = true;
    public bool invertPitch = false;

    [Header("Startup Guard")]
    public float startupDelay = 1.5f;

    [Header("Smoothing")]
    public float smoothSpeed = 4f;

    [Header("Update Control")]
    public float updateInterval = 0.1f;
    public float minAngleDelta = 2f;

    [Header("Safety")]
    public float maxStepPerUpdate = 5f;

    [Header("Debug")]
    public bool enableDebugLog = true;

    private float currentYaw = 0f;
    private float currentPitch = 0f;

    private float lastSentYaw = 0f;
    private float lastSentPitch = 0f;

    private float updateTimer;
    private float startTime;

    private bool isTracking;

    private const float MinimumTargetSqrMagnitude = 0.000001f;

    private void Start()
    {
        startTime = Time.time;

        currentYaw = 0f;
        currentPitch = 0f;

        lastSentYaw = 0f;
        lastSentPitch = 0f;

        updateTimer = 0f;
        isTracking = false;
    }

    private void LateUpdate()
    {
        if (attentionDriver == null ||
            attentionTarget == null ||
            robotBodyFrame == null ||
            sensorCameraFrame == null)
        {
            return;
        }

        if (Time.time - startTime < startupDelay)
            return;

        if (!attentionDriver.HasValidTarget)
        {
            updateTimer = 0f;
            isTracking = false;
            return;
        }

        Vector3 worldDirection =
            attentionTarget.position - sensorCameraFrame.position;

        if (worldDirection.sqrMagnitude < MinimumTargetSqrMagnitude)
            return;

        Vector3 direction =
            robotBodyFrame
                .InverseTransformDirection(worldDirection)
                .normalized;

        float targetYaw =
            Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        float horizontal = Mathf.Sqrt(
            direction.x * direction.x +
            direction.z * direction.z
        );

        float targetPitch =
            Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;

        if (invertYaw)
            targetYaw *= -1f;

        if (invertPitch)
            targetPitch *= -1f;

        targetYaw = Mathf.Clamp(targetYaw, -yawRange, yawRange);
        targetPitch = Mathf.Clamp(targetPitch, -pitchRange, pitchRange);

        if (!isTracking)
        {
            isTracking = true;
            updateTimer = 0f;

            if (enableDebugLog)
                Debug.Log("[FaceToAngleConverter] Tracking started");
        }

        updateTimer += Time.deltaTime;

        if (updateTimer < updateInterval)
            return;

        updateTimer = 0f;

        float smoothedYaw = Mathf.Lerp(
            currentYaw,
            targetYaw,
            Time.deltaTime * smoothSpeed
        );

        float smoothedPitch = Mathf.Lerp(
            currentPitch,
            targetPitch,
            Time.deltaTime * smoothSpeed
        );

        currentYaw = Mathf.MoveTowards(
            currentYaw,
            smoothedYaw,
            maxStepPerUpdate
        );

        currentPitch = Mathf.MoveTowards(
            currentPitch,
            smoothedPitch,
            maxStepPerUpdate
        );

        bool changed =
            Mathf.Abs(currentYaw - lastSentYaw) >= minAngleDelta ||
            Mathf.Abs(currentPitch - lastSentPitch) >= minAngleDelta;

        if (!changed)
            return;

        lastSentYaw = currentYaw;
        lastSentPitch = currentPitch;

        if (enableDebugLog)
        {
            Debug.Log(
                $"[FaceToAngleConverter] " +
                $"World:{attentionTarget.position} " +
                $"BodyDirection:{direction} " +
                $"CalculatedYaw:{targetYaw:F1} " +
                $"CalculatedPitch:{targetPitch:F1} " +
                $"OutputYaw:{currentYaw:F1} " +
                $"OutputPitch:{currentPitch:F1} " +
                $"HasValidTarget:{attentionDriver.HasValidTarget}"
            );
        }

        // Diagnostic only. Canonical production output is solved VRM pose
        // -> VirtualNeckRetarget -> PhysicalNeckOutputOwner.
    }
}
