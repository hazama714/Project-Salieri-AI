// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using UnityEngine.UI;

public class CameraInput : MonoBehaviour
{
    [Header("Permission")]
    [SerializeField] private AndroidCameraPermission permission;

    [Header("Preview")]
    [SerializeField] private RawImage previewImage;

    [Header("Camera Settings")]
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 480;
    [SerializeField] private int requestedFPS = 30;

    private WebCamTexture webcamTexture;
    private bool cameraStarted;
    private bool aspectApplied;
    private float logTimer;

    public WebCamTexture CurrentTexture => webcamTexture;

    public bool IsCameraReady =>
        webcamTexture != null &&
        webcamTexture.isPlaying &&
        webcamTexture.width > 16 &&
        webcamTexture.height > 16;

    private void Start()
    {
        Debug.Log("[CameraInput][START]");
    }

    private void Update()
    {
        if (!cameraStarted)
        {
            TryStartCamera();
            return;
        }

        ApplyAspectWhenReady();
        LogFrameInfo();
    }

    private void TryStartCamera()
    {
        if (permission != null && !permission.IsGranted)
            return;

        WebCamDevice[] devices = WebCamTexture.devices;

        Debug.Log($"[CameraInput][DEVICE_COUNT] {devices.Length}");

        if (devices.Length == 0)
        {
            Debug.LogError("[CameraInput][ERROR] No camera device found");
            return;
        }

        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log(
                $"[CameraInput][DEVICE] " +
                $"Index:{i} Name:{devices[i].name} Front:{devices[i].isFrontFacing}"
            );
        }

        WebCamDevice selectedDevice = devices[0];

        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i].isFrontFacing)
            {
                selectedDevice = devices[i];
                break;
            }
        }

        Debug.Log($"[CameraInput][SELECTED] {selectedDevice.name}");

        webcamTexture = new WebCamTexture(
            selectedDevice.name,
            requestedWidth,
            requestedHeight,
            requestedFPS
        );

        if (previewImage != null)
            previewImage.texture = webcamTexture;

        webcamTexture.Play();
        cameraStarted = true;

        ApplyPreviewAspect();
        ApplyPreviewRotation();

        Debug.Log("[CameraInput][START_CAMERA]");
    }

    private void ApplyAspectWhenReady()
    {
        if (aspectApplied)
            return;

        if (!IsCameraReady)
            return;

        ApplyPreviewAspect();
        ApplyPreviewRotation();

        aspectApplied = true;

        Debug.Log("[CameraInput][ASPECT_APPLIED]");
    }

    private void ApplyPreviewAspect()
    {
        if (previewImage == null || webcamTexture == null)
            return;

        AspectRatioFitter fitter =
            previewImage.GetComponent<AspectRatioFitter>();

        if (fitter != null)
            Destroy(fitter);

        Debug.Log(
            $"[CameraInput][ASPECT_KEEP_RECT] " +
            $"Width:{webcamTexture.width} Height:{webcamTexture.height}"
        );
    }

    private void ApplyPreviewRotation()
    {
        if (previewImage == null || webcamTexture == null)
            return;

        int rotation = webcamTexture.videoRotationAngle;

        previewImage.rectTransform.localEulerAngles =
            new Vector3(0f, 0f, -rotation);

        Debug.Log($"[CameraInput][PREVIEW_ROTATION] {rotation}");
    }

    private void LogFrameInfo()
    {
        if (webcamTexture == null)
            return;

        logTimer += Time.deltaTime;

        if (logTimer < 1.0f)
            return;

        logTimer = 0f;

        Debug.Log(
            $"[CameraInput][FRAME] " +
            $"Width:{webcamTexture.width} " +
            $"Height:{webcamTexture.height} " +
            $"FPS:{requestedFPS} " +
            $"Playing:{webcamTexture.isPlaying} " +
            $"Rotation:{webcamTexture.videoRotationAngle}"
        );
    }

    private void OnDestroy()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
            Debug.Log("[CameraInput][STOP_CAMERA]");
        }
    }
}