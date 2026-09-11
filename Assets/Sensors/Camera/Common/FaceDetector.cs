// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

public class FaceDetector : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private CameraInput cameraInput;

    [Header("Debug")]
    [SerializeField] private float logInterval = 1.0f;

    private float logTimer;

    private void Start()
    {
        Debug.Log("[FaceDetector][START]");
    }

    private void Update()
    {
        if (cameraInput == null)
        {
            Debug.LogError("[FaceDetector][ERROR] cameraInput is null");
            enabled = false;
            return;
        }

        if (!cameraInput.IsCameraReady)
            return;

        logTimer += Time.deltaTime;

        if (logTimer < logInterval)
            return;

        logTimer = 0f;

        WebCamTexture texture = cameraInput.CurrentTexture;

        Debug.Log(
            $"[FaceDetector][CAMERA_READY] " +
            $"Width:{texture.width} Height:{texture.height} " +
            $"Playing:{texture.isPlaying} Rotation:{texture.videoRotationAngle}"
        );
    }
}