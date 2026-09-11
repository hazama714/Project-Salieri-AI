// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// RuntimeConnectionSettings support

using UnityEngine;

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;

using SalieriAI.Runtime;
using SalieriAI.Sensors.Camera.Common;
using SalieriAI.Core.Diagnostics.Performance;
using SalieriAI.Sensors.Camera.OpenCV;

using OpenCVRect = OpenCVForUnity.CoreModule.Rect;

public class FaceDetector_OpenCV : MonoBehaviour, IFacePerception
{
    [Header("Runtime Settings")]
    [SerializeField] private RuntimeConnectionSettings runtimeSettings;

    [Header("Input")]
    [SerializeField] private CameraInput cameraInput;

    [Header("Model")]
    [SerializeField]
    private string cascadeFilePath =
        "OpenCVForUnityExamples/objdetect/haarcascade_frontalface_alt.xml";

    [Header("Detection Settings")]
    [SerializeField] private float scaleFactor = 1.05f;
    [SerializeField] private int minNeighbors = 5;
    [SerializeField] private int minFaceSize = 100;

    [Header("Cadence")]
    [Tooltip("Minimum interval between Haar detections. Default 0.1 seconds = 10 Hz.")]
    [SerializeField] private float faceDetectionIntervalSeconds = 0.1f;

    [Header("Debug")]
    [SerializeField] private float logInterval = 0.5f;

    public bool HasFace { get; private set; }

    [Header("Face Rect")]
    public float FaceX;
    public float FaceY;
    public float FaceCenterX;
    public float FaceCenterY;
    public float FaceWidth;
    public float FaceHeight;

    public int FrameWidth => detectRgbaMat != null ? detectRgbaMat.width() : 0;
    public int FrameHeight => detectRgbaMat != null ? detectRgbaMat.height() : 0;
    public int Rotation => lastRotation;

    float IFacePerception.FrameWidth => FrameWidth;
    float IFacePerception.FrameHeight => FrameHeight;

    float IFacePerception.FaceX => FaceX;
    float IFacePerception.FaceY => FaceY;
    float IFacePerception.FaceCenterX => FaceCenterX;
    float IFacePerception.FaceCenterY => FaceCenterY;
    float IFacePerception.FaceWidth => FaceWidth;
    float IFacePerception.FaceHeight => FaceHeight;

    private CascadeClassifier faceCascade;

    private Mat sourceRgbaMat;
    private Mat detectRgbaMat;
    private Mat grayMat;
    private MatOfRect faces;

    private int lastSourceWidth;
    private int lastSourceHeight;
    private int lastRotation;

    private float logTimer;
    private bool wasDetected;
    private bool isOpenCVEnabled = true;
    private readonly HaarFaceDetectionCadenceGate cadenceGate =
        new HaarFaceDetectionCadenceGate();

    private void Awake()
    {
        if (runtimeSettings == null)
        {
            runtimeSettings = FindObjectOfType<RuntimeConnectionSettings>();
        }

        isOpenCVEnabled = IsOpenCVEnabled();

        if (!isOpenCVEnabled)
        {
            ClearFace();
            Debug.Log("[FaceDetector_OpenCV][DISABLED] OpenCV disabled by RuntimeConnectionSettings.");
        }
    }

    private void Start()
    {
        if (!isOpenCVEnabled)
        {
            enabled = false;
            return;
        }

        Debug.Log("[FaceDetector_OpenCV][START]");

        string fullPath = Utils.getFilePath(cascadeFilePath);
        Debug.Log($"[FaceDetector_OpenCV][MODEL_PATH] {fullPath}");

        faceCascade = new CascadeClassifier(fullPath);

        if (faceCascade.empty())
        {
            Debug.LogError("[FaceDetector_OpenCV][ERROR] Model file is not loaded.");
            ClearFace();
            enabled = false;
            return;
        }

        faces = new MatOfRect();

        cadenceGate.Reset();

        double effectiveInterval =
            HaarFaceDetectionCadenceGate.NormalizeIntervalSeconds(
                faceDetectionIntervalSeconds);
        Debug.Log(
            "[FaceDetector_OpenCV][MODEL_LOADED] "
            + $"DetectionIntervalSeconds:{effectiveInterval:F3} "
            + $"DetectionBudgetHz:{1d / effectiveInterval:F1}");
    }

    private void Update()
    {
        if (!isOpenCVEnabled)
        {
            ClearFace();
            enabled = false;
            return;
        }

        if (runtimeSettings != null && !runtimeSettings.useOpenCV)
        {
            ClearFace();
            ReleaseOpenCVResources();
            Debug.Log("[FaceDetector_OpenCV][DISABLED_RUNTIME] OpenCV disabled while running.");
            enabled = false;
            return;
        }

        if (cameraInput == null)
        {
            Debug.LogError("[FaceDetector_OpenCV][ERROR] cameraInput is null");
            ClearFace();
            enabled = false;
            return;
        }

        if (!cameraInput.IsCameraReady)
        {
            ClearFace();
            return;
        }

        if (faceCascade == null || faceCascade.empty())
        {
            Debug.LogError("[FaceDetector_OpenCV][ERROR] faceCascade is not ready");
            ClearFace();
            enabled = false;
            return;
        }

        WebCamTexture texture = cameraInput.CurrentTexture;

        SalieriRuntimePerformanceProbe.RecordCameraObservation(
            texture.didUpdateThisFrame);

        // Preserve the existing wall-clock-like debug cadence independently
        // from the Haar detection admission cadence.
        logTimer += Time.deltaTime;

        HaarFaceDetectionCadenceDecision cadenceDecision =
            cadenceGate.Evaluate(
                Time.realtimeSinceStartupAsDouble,
                texture.didUpdateThisFrame,
                faceDetectionIntervalSeconds);

        if (cadenceDecision != HaarFaceDetectionCadenceDecision.Run)
        {
            // A skipped detection keeps the last published face result. Existing
            // FacePerceptionBuffer lost/timeout semantics remain authoritative.
            return;
        }

        long imagePreparationStarted =
            SalieriRuntimePerformanceProbe.Timestamp();

        int sourceWidth = texture.width;
        int sourceHeight = texture.height;
        int rotation = texture.videoRotationAngle;

        if (NeedRecreateMats(sourceWidth, sourceHeight, rotation))
        {
            CreateMats(sourceWidth, sourceHeight, rotation);
        }

        Utils.webCamTextureToMat(texture, sourceRgbaMat);

        ApplyRotation(sourceRgbaMat, detectRgbaMat, rotation);

        Imgproc.cvtColor(detectRgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);
        Imgproc.equalizeHist(grayMat, grayMat);

        SalieriRuntimePerformanceProbe.RecordDuration(
            SalieriRuntimePerformanceMetric.HaarImagePreparation,
            imagePreparationStarted);

        long faceDetectionStarted =
            SalieriRuntimePerformanceProbe.Timestamp();

        faceCascade.detectMultiScale(
            grayMat,
            faces,
            scaleFactor,
            minNeighbors,
            0,
            new Size(minFaceSize, minFaceSize),
            new Size()
        );

        OpenCVRect[] faceArray = faces.toArray();

        SalieriRuntimePerformanceProbe.RecordDuration(
            SalieriRuntimePerformanceMetric.HaarDetection,
            faceDetectionStarted,
            faceArray.Length,
            texture.didUpdateThisFrame ? 1L : 0L);

        if (faceArray.Length > 0)
        {
            OpenCVRect largestFace = GetLargestFace(faceArray);

            HasFace = true;

            FaceX = largestFace.x;
            FaceY = largestFace.y;
            FaceWidth = largestFace.width;
            FaceHeight = largestFace.height;

            FaceCenterX = FaceX + FaceWidth * 0.5f;
            FaceCenterY = FaceY + FaceHeight * 0.5f;

            if (logTimer >= logInterval)
            {
                logTimer = 0f;

                Debug.Log(
                    $"[FaceDetector_OpenCV][FOUND] " +
                    $"X:{FaceX:F1} Y:{FaceY:F1} " +
                    $"CenterX:{FaceCenterX:F1} CenterY:{FaceCenterY:F1} " +
                    $"Width:{FaceWidth:F1} Height:{FaceHeight:F1} " +
                    $"DetectFrame:{FrameWidth}x{FrameHeight} " +
                    $"SourceFrame:{sourceWidth}x{sourceHeight} Rotation:{rotation}"
                );
            }

            wasDetected = true;
        }
        else
        {
            ClearFace();

            if (wasDetected || logTimer >= logInterval)
            {
                logTimer = 0f;
                wasDetected = false;

                Debug.Log(
                    $"[FaceDetector_OpenCV][LOST] " +
                    $"DetectFrame:{FrameWidth}x{FrameHeight} " +
                    $"SourceFrame:{sourceWidth}x{sourceHeight} Rotation:{rotation}"
                );
            }
        }
    }

    private bool IsOpenCVEnabled()
    {
        if (runtimeSettings == null)
            return true;

        return runtimeSettings.useOpenCV;
    }

    private void ClearFace()
    {
        HasFace = false;

        FaceX = 0f;
        FaceY = 0f;
        FaceCenterX = 0f;
        FaceCenterY = 0f;
        FaceWidth = 0f;
        FaceHeight = 0f;
    }

    private bool NeedRecreateMats(int sourceWidth, int sourceHeight, int rotation)
    {
        if (sourceRgbaMat == null || detectRgbaMat == null || grayMat == null)
            return true;

        if (lastSourceWidth != sourceWidth)
            return true;

        if (lastSourceHeight != sourceHeight)
            return true;

        if (lastRotation != rotation)
            return true;

        return false;
    }

    private void CreateMats(int sourceWidth, int sourceHeight, int rotation)
    {
        ReleaseMats();

        lastSourceWidth = sourceWidth;
        lastSourceHeight = sourceHeight;
        lastRotation = rotation;

        sourceRgbaMat = new Mat(sourceHeight, sourceWidth, CvType.CV_8UC4);

        int detectWidth = sourceWidth;
        int detectHeight = sourceHeight;

        if (rotation == 90 || rotation == 270)
        {
            detectWidth = sourceHeight;
            detectHeight = sourceWidth;
        }

        detectRgbaMat = new Mat(detectHeight, detectWidth, CvType.CV_8UC4);
        grayMat = new Mat(detectHeight, detectWidth, CvType.CV_8UC1);

        Debug.Log(
            $"[FaceDetector_OpenCV][CREATE_MATS] " +
            $"Source:{sourceWidth}x{sourceHeight} " +
            $"Detect:{detectWidth}x{detectHeight} " +
            $"Rotation:{rotation}"
        );
    }

    private void ApplyRotation(Mat src, Mat dst, int rotation)
    {
        if (rotation == 90)
        {
            Core.rotate(src, dst, Core.ROTATE_90_CLOCKWISE);
        }
        else if (rotation == 270)
        {
            Core.rotate(src, dst, Core.ROTATE_90_COUNTERCLOCKWISE);
        }
        else if (rotation == 180)
        {
            Core.rotate(src, dst, Core.ROTATE_180);
        }
        else
        {
            src.copyTo(dst);
        }
    }

    private OpenCVRect GetLargestFace(OpenCVRect[] faceArray)
    {
        OpenCVRect largest = faceArray[0];
        double largestArea = largest.area();

        for (int i = 1; i < faceArray.Length; i++)
        {
            double area = faceArray[i].area();

            if (area > largestArea)
            {
                largest = faceArray[i];
                largestArea = area;
            }
        }

        return largest;
    }

    private void ReleaseOpenCVResources()
    {
        ReleaseMats();

        if (faces != null)
        {
            faces.Dispose();
            faces = null;
        }

        if (faceCascade != null)
        {
            faceCascade.Dispose();
            faceCascade = null;
        }
    }

    private void ReleaseMats()
    {
        if (sourceRgbaMat != null)
        {
            sourceRgbaMat.Dispose();
            sourceRgbaMat = null;
        }

        if (detectRgbaMat != null)
        {
            detectRgbaMat.Dispose();
            detectRgbaMat = null;
        }

        if (grayMat != null)
        {
            grayMat.Dispose();
            grayMat = null;
        }
    }

    private void OnDestroy()
    {
        ReleaseOpenCVResources();

        Debug.Log("[FaceDetector_OpenCV][DESTROY]");
    }
}
