// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.DnnModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityIntegration.Worker.DnnModule;

using SalieriAI.Sensors.Camera.Common;
using SalieriAI.Core.Diagnostics.Performance;
using SalieriAI.Core.Perception.VisualSnapshots;

using ObjectDetectionData = OpenCVForUnity.UnityIntegration.Worker.DataStruct.ObjectDetectionData;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SalieriAI.Core.Perception.ObjectDetection
{
    /// <summary>
    /// CameraInputの最新映像を一定間隔でYOLOXへ渡し、
    /// Unity/OpenCVオブジェクトを含まないVisibleObjectSetとして公開する。
    ///
    /// このServiceはTracking、対象選択、発話、身体制御を行わない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class YoloXObjectDetectionService :
        MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private CameraInput cameraInput;

        [Header("Model")]
        [SerializeField]
        private string modelRelativePath =
            "OpenCVForUnityExamples/dnn/yolox_tiny.onnx";

        [SerializeField]
        private string classesRelativePath =
            "OpenCVForUnityExamples/dnn/coco.names";

        [Header("YOLOX")]
        [SerializeField]
        private int modelInputWidth = 416;

        [SerializeField]
        private int modelInputHeight = 416;

        [SerializeField]
        [Range(0.01f, 1f)]
        private float confidenceThreshold = 0.40f;

        [SerializeField]
        [Range(0.01f, 1f)]
        private float nmsThreshold = 0.45f;

        [SerializeField]
        [Min(1)]
        private int topK = 100;

        [Header("Execution")]
        [SerializeField]
        [Min(0.1f)]
        private float detectionIntervalSeconds = 0.75f;

        [Header("SourceFrame Visual Snapshot")]
        [SerializeField]
        [Min(1)]
        private int visualSnapshotCapacity = 16;

        [SerializeField]
        [Min(0.1f)]
        private float visualSnapshotTtlSeconds = 20f;

        [Header("Debug")]
        [SerializeField]
        private bool logSnapshots = true;

        [SerializeField]
        private bool logEmptySnapshots = false;

        [SerializeField]
        [Min(1)]
        private int maximumLoggedObjects = 5;

        private YOLOXObjectDetector detector;

        private Mat sourceRgbaMat;

        private int currentWidth;
        private int currentHeight;

        private float nextDetectionTime;

        private bool initialized;
        private bool fatalError;

        private long frameId;
        private long requestSequence;

        private string[] classLabels = Array.Empty<string>();
        private int mainThreadId;
        private int destroyed;
        private int disposeDetectorAfterDrain;

        private readonly YoloInferenceSingleFlightAdmission admission =
            new YoloInferenceSingleFlightAdmission();

        private readonly ConcurrentQueue<YoloInferenceWorkerResult>
            completedWorkers =
                new ConcurrentQueue<YoloInferenceWorkerResult>();

        private Task activeWorkerTask;

        private VisualFrameSnapshotStore visualSnapshotStore;

        private readonly string sessionId =
            Guid.NewGuid().ToString("N");

        /// <summary>
        /// 新しい検出結果が作成されたときに通知する。
        /// </summary>
        public event Action<VisibleObjectSet>
            SnapshotUpdated;

        /// <summary>
        /// YOLOXモデルが初期化済みか。
        /// </summary>
        public bool IsReady =>
            initialized &&
            !fatalError;

        /// <summary>
        /// 最新の検出結果。
        /// 検出数0件のSnapshotも保持する。
        /// </summary>
        public VisibleObjectSet LatestSnapshot
        {
            get;
            private set;
        }

        public bool HasSnapshot =>
            LatestSnapshot != null;

        public string SessionId =>
            sessionId;

        public bool TryGetVisualFrameSnapshot(
            string expectedSessionId,
            long sourceFrameId,
            DateTime evaluatedAtUtc,
            out VisualFrameSnapshot snapshot,
            out string error)
        {
            EnsureVisualSnapshotStore();
            return visualSnapshotStore.TryGetExact(
                expectedSessionId,
                sourceFrameId,
                evaluatedAtUtc,
                out snapshot,
                out error);
        }

        private void Start()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EnsureVisualSnapshotStore();
            TryInitializeDetector();
        }

        private void Update()
        {
            PublishCompletedWorkers();

            if (fatalError || !initialized)
                return;

            if (cameraInput == null)
            {
                FailInitialization(
                    "[YoloXObjectDetectionService][ERROR] " +
                    "CameraInput is null."
                );

                return;
            }

            if (!cameraInput.IsCameraReady)
                return;

            WebCamTexture texture =
                cameraInput.CurrentTexture;

            if (texture == null ||
                !texture.isPlaying ||
                texture.width <= 16 ||
                texture.height <= 16)
            {
                return;
            }

            if (!texture.didUpdateThisFrame)
                return;

            if (Time.unscaledTime < nextDetectionTime)
                return;

            nextDetectionTime =
                Time.unscaledTime +
                detectionIntervalSeconds;

            long requestId = ++requestSequence;
            YoloInferenceAdmissionDecision decision =
                admission.TryAdmit(requestId);

            if (decision != YoloInferenceAdmissionDecision.Accepted)
            {
                return;
            }

            Mat ownedRgbaSnapshot = null;

            try
            {
                long totalStarted =
                    SalieriRuntimePerformanceProbe.Timestamp();
                long snapshotStarted =
                    SalieriRuntimePerformanceProbe.Timestamp();
                long snapshotWallStarted = Stopwatch.GetTimestamp();

                EnsureFrameMats(texture.width, texture.height);

                // WebCamTexture access remains on the Unity Main Thread. The
                // worker receives a deep-owned snapshot which the camera path
                // can no longer mutate.
                Utils.webCamTextureToMat(texture, sourceRgbaMat);
                ownedRgbaSnapshot = sourceRgbaMat.clone();

                byte[] ownedRgbaPixels = new byte[
                    checked(texture.width * texture.height * 4)];
                int copiedBytes = sourceRgbaMat.get(
                    0,
                    0,
                    ownedRgbaPixels);
                if (copiedBytes != ownedRgbaPixels.Length)
                {
                    throw new InvalidOperationException(
                        "SourceFrame RGBA snapshot copy was incomplete. " +
                        $"Expected={ownedRgbaPixels.Length} Actual={copiedBytes}");
                }

                SalieriRuntimePerformanceProbe.RecordDuration(
                    SalieriRuntimePerformanceMetric.YoloPreprocess,
                    snapshotStarted);

                var request = new YoloInferenceWorkerRequest(
                    requestId,
                    ownedRgbaSnapshot,
                    ownedRgbaPixels,
                    texture.width,
                    texture.height,
                    texture.videoRotationAngle,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Time.frameCount,
                    totalStarted,
                    ElapsedMilliseconds(snapshotWallStarted));

                ownedRgbaSnapshot = null;
                activeWorkerTask = Task.Run(() => RunWorkerAndQueue(request));
            }
            catch (Exception ex)
            {
                ownedRgbaSnapshot?.Dispose();
                admission.TryComplete(requestId);

                Debug.LogError(
                    $"[YoloXObjectDetectionService]" +
                    $"[START_ERROR] Request={requestId} {ex}",
                    this
                );
            }
        }

        private void TryInitializeDetector()
        {
            if (cameraInput == null)
            {
                FailInitialization(
                    "[YoloXObjectDetectionService][ERROR] " +
                    "CameraInput is not assigned."
                );

                return;
            }

            try
            {
                string modelPath =
                    Utils.getFilePath(
                        modelRelativePath
                    );

                string classesPath =
                    Utils.getFilePath(
                        classesRelativePath
                    );

                detector =
                    new YOLOXObjectDetector(
                        modelPath,
                        classesPath,
                        new Size(
                            modelInputWidth,
                            modelInputHeight
                        ),
                        confidenceThreshold,
                        nmsThreshold,
                        topK,
                        Dnn.DNN_BACKEND_OPENCV,
                        Dnn.DNN_TARGET_CPU
                    );

                detector.SelectedNMSStrategy =
                    YOLOXObjectDetector
                        .NMSStrategy
                        .ClassWise;

                classLabels = detector.GetClassLabels();

                initialized = true;

                Debug.Log(
                    $"[YoloXObjectDetectionService]" +
                    $"[READY] " +
                    $"Session={sessionId} " +
                    $"Input={modelInputWidth}x" +
                    $"{modelInputHeight} " +
                    $"Confidence=" +
                    $"{confidenceThreshold:F2} " +
                    $"NMS={nmsThreshold:F2}",
                    this
                );
            }
            catch (Exception ex)
            {
                FailInitialization(
                    $"[YoloXObjectDetectionService]" +
                    $"[INIT_ERROR] {ex}"
                );
            }
        }

        private void EnsureFrameMats(
            int width,
            int height
        )
        {
            if (sourceRgbaMat != null &&
                currentWidth == width &&
                currentHeight == height)
            {
                return;
            }

            ReleaseFrameMats();

            currentWidth = width;
            currentHeight = height;

            sourceRgbaMat =
                new Mat(
                    height,
                    width,
                    CvType.CV_8UC4
                );

            Debug.Log(
                $"[YoloXObjectDetectionService]" +
                $"[CREATE_MATS] " +
                $"{width}x{height}",
                this
            );
        }

        private void RunWorkerAndQueue(YoloInferenceWorkerRequest request)
        {
            YoloInferenceWorkerResult workerResult = ExecuteWorker(request);

            if (Volatile.Read(ref destroyed) != 0)
            {
                CompleteAfterShutdown(workerResult.RequestId);
                return;
            }

            completedWorkers.Enqueue(workerResult);

            // OnDestroy may begin between the first destroyed check and the
            // enqueue. In that race no later Main Thread Update exists, so the
            // worker removes the abandoned completion and finishes the drain.
            if (Volatile.Read(ref destroyed) != 0)
            {
                while (completedWorkers.TryDequeue(
                    out YoloInferenceWorkerResult abandoned))
                {
                    admission.TryComplete(abandoned.RequestId);
                }

                CompleteAfterShutdown(workerResult.RequestId);
            }
        }

        private void CompleteAfterShutdown(long requestId)
        {
            admission.TryComplete(requestId);

            if (Interlocked.Exchange(
                    ref disposeDetectorAfterDrain,
                    0) == 0)
            {
                return;
            }

            YOLOXObjectDetector drainedDetector =
                Interlocked.Exchange(ref detector, null);
            drainedDetector?.Dispose();
        }

        private YoloInferenceWorkerResult ExecuteWorker(
            YoloInferenceWorkerRequest request)
        {
            int workerThreadId = Thread.CurrentThread.ManagedThreadId;
            long workerStarted = Stopwatch.GetTimestamp();
            long workerProbeStarted =
                SalieriRuntimePerformanceProbe.Timestamp();
            long detectorMillisecondsStarted = 0L;
            double detectorMilliseconds = 0d;
            Mat bgrSnapshot = null;
            Mat result = null;

            try
            {
                bgrSnapshot = new Mat(
                    request.Height,
                    request.Width,
                    CvType.CV_8UC3);

                Imgproc.cvtColor(
                    request.OwnedRgbaSnapshot,
                    bgrSnapshot,
                    Imgproc.COLOR_RGBA2BGR);

                detectorMillisecondsStarted = Stopwatch.GetTimestamp();
                result = detector.Detect(
                    bgrSnapshot,
                    useCopyOutput: true);
                detectorMilliseconds =
                    ElapsedMilliseconds(detectorMillisecondsStarted);

                ObjectDetectionData[] rawDetections =
                    detector.ToStructuredData(result);

                SalieriRuntimePerformanceProbe.RecordDuration(
                    SalieriRuntimePerformanceMetric.YoloInference,
                    workerProbeStarted,
                    workerThreadId,
                    request.RequestId);

                return YoloInferenceWorkerResult.Succeeded(
                    request,
                    rawDetections,
                    workerThreadId,
                    ElapsedMilliseconds(workerStarted),
                    detectorMilliseconds);
            }
            catch (Exception exception)
            {
                SalieriRuntimePerformanceProbe.RecordDuration(
                    SalieriRuntimePerformanceMetric.YoloInference,
                    workerProbeStarted,
                    workerThreadId,
                    request.RequestId);

                return YoloInferenceWorkerResult.Failed(
                    request,
                    exception,
                    workerThreadId,
                    ElapsedMilliseconds(workerStarted),
                    detectorMilliseconds);
            }
            finally
            {
                result?.Dispose();
                bgrSnapshot?.Dispose();
                request.OwnedRgbaSnapshot?.Dispose();
            }
        }

        private void PublishCompletedWorkers()
        {
            while (completedWorkers.TryDequeue(
                out YoloInferenceWorkerResult result))
            {
                PublishWorkerResult(result);
            }
        }

        private void PublishWorkerResult(YoloInferenceWorkerResult result)
        {
            if (Volatile.Read(ref destroyed) != 0 ||
                !admission.IsActive(result.RequestId))
            {
                return;
            }

            long completionStarted =
                SalieriRuntimePerformanceProbe.Timestamp();
            long completionWallStarted = Stopwatch.GetTimestamp();
            int completionThreadId = Thread.CurrentThread.ManagedThreadId;
            int framesAdvanced =
                Math.Max(0, Time.frameCount - result.StartFrame);

            try
            {
                if (!result.Success)
                {
                    Debug.LogError(
                        "[YoloXObjectDetectionService][A4][FAILED] "
                        + $"Request={result.RequestId} "
                        + $"MainThread={mainThreadId} "
                        + $"WorkerThread={result.WorkerThreadId} "
                        + $"CompletionThread={completionThreadId} "
                        + $"FramesAdvanced={framesAdvanced} "
                        + $"Error={result.Error}",
                        this);
                    return;
                }

                frameId++;
                VisibleObjectSet snapshot = BuildSnapshot(
                    result.RawDetections,
                    result.Width,
                    result.Height,
                    result.Rotation,
                    result.CapturedAtUnixMilliseconds,
                    frameId);

                EnsureVisualSnapshotStore();
                VisualFrameSnapshot visualSnapshot =
                    VisualFrameSnapshot.FromOwnedRgba32(
                        sessionId,
                        frameId,
                        result.Width,
                        result.Height,
                        result.Rotation,
                        result.CapturedAtUnixMilliseconds,
                        result.OwnedRgbaPixels);
                if (!visualSnapshotStore.TryAdd(
                        visualSnapshot,
                        DateTime.UtcNow,
                        out string visualError))
                {
                    Debug.LogError(
                        "[YoloXObjectDetectionService]" +
                        "[VISUAL_SNAPSHOT_ERROR] " + visualError,
                        this);
                }

                LatestSnapshot = snapshot;
                InvokeSnapshotUpdated(snapshot);
                LogSnapshot(snapshot);

                SalieriRuntimePerformanceProbe.RecordDuration(
                    SalieriRuntimePerformanceMetric.YoloPostprocess,
                    completionStarted,
                    result.RawDetections == null
                        ? 0L
                        : result.RawDetections.Length,
                    snapshot == null ? 0L : snapshot.Count);

                SalieriRuntimePerformanceProbe.RecordDuration(
                    SalieriRuntimePerformanceMetric.YoloTotal,
                    result.TotalStartedTimestamp,
                    framesAdvanced,
                    frameId);

                var threadObservation = new YoloInferenceThreadObservation(
                    mainThreadId,
                    result.WorkerThreadId,
                    completionThreadId);

                Debug.Log(
                    "[YoloXObjectDetectionService][A4][COMPLETED] "
                    + $"Request={result.RequestId} "
                    + $"MainThread={mainThreadId} "
                    + $"WorkerThread={result.WorkerThreadId} "
                    + $"CompletionThread={completionThreadId} "
                    + $"WorkerOffMain={threadObservation.WorkerOffMain} "
                    + $"CompletionOnMain={threadObservation.CompletionOnMain} "
                    + $"FramesAdvanced={framesAdvanced} "
                    + $"SnapshotMs={result.SnapshotMilliseconds:F3} "
                    + $"WorkerMs={result.WorkerMilliseconds:F3} "
                    + $"DetectorMs={result.DetectorMilliseconds:F3} "
                    + $"CompletionMs={ElapsedMilliseconds(completionWallStarted):F3}",
                    this);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[YoloXObjectDetectionService][A4][COMPLETION_ERROR] "
                    + $"Request={result.RequestId} {exception}",
                    this);
            }
            finally
            {
                admission.TryComplete(result.RequestId);
                activeWorkerTask = null;
            }
        }

        private VisibleObjectSet BuildSnapshot(
            ObjectDetectionData[] rawDetections,
            int frameWidth,
            int frameHeight,
            int rotation,
            long capturedAtUnixMilliseconds,
            long currentFrameId
        )
        {
            List<VisibleObject> objects =
                new List<VisibleObject>();

            if (rawDetections != null)
            {
                for (int i = 0;
                     i < rawDetections.Length;
                     i++)
                {
                    var detection =
                        rawDetections[i];

                    float x1 =
                        Mathf.Clamp(
                            detection.X1,
                            0f,
                            frameWidth
                        );

                    float y1 =
                        Mathf.Clamp(
                            detection.Y1,
                            0f,
                            frameHeight
                        );

                    float x2 =
                        Mathf.Clamp(
                            detection.X2,
                            0f,
                            frameWidth
                        );

                    float y2 =
                        Mathf.Clamp(
                            detection.Y2,
                            0f,
                            frameHeight
                        );

                    if (x2 <= x1 || y2 <= y1)
                        continue;

                    VisibleObject item =
                        new VisibleObject
                        {
                            DetectionId =
                                $"{sessionId}:" +
                                $"{currentFrameId}:" +
                                $"{i}",

                            ClassId =
                                detection.ClassId,

                            ClassLabel =
                                ResolveClassLabel(detection.ClassId),

                            Confidence =
                                detection.Confidence,

                            X1 = x1,
                            Y1 = y1,
                            X2 = x2,
                            Y2 = y2,

                            NormalizedX1 =
                                x1 / frameWidth,

                            NormalizedY1 =
                                y1 / frameHeight,

                            NormalizedX2 =
                                x2 / frameWidth,

                            NormalizedY2 =
                                y2 / frameHeight
                        };

                    objects.Add(item);
                }
            }

            objects.Sort(
                (left, right) =>
                    right
                        .Confidence
                        .CompareTo(
                            left.Confidence
                        )
            );

            return new VisibleObjectSet
            {
                SessionId =
                    sessionId,

                FrameId =
                    currentFrameId,

                CapturedAtUnixMilliseconds =
                    capturedAtUnixMilliseconds,

                FrameWidth =
                    frameWidth,

                FrameHeight =
                    frameHeight,

                Rotation =
                    rotation,

                Objects =
                    objects.ToArray()
            };
        }

        private string ResolveClassLabel(int classId)
        {
            if (classId >= 0 &&
                classLabels != null &&
                classId < classLabels.Length)
            {
                return classLabels[classId];
            }

            return classId.ToString();
        }

        private static double ElapsedMilliseconds(long startedTimestamp)
        {
            if (startedTimestamp <= 0L)
                return 0d;

            return Math.Max(
                0d,
                (Stopwatch.GetTimestamp() - startedTimestamp)
                    * 1000d / Stopwatch.Frequency);
        }

        private void InvokeSnapshotUpdated(
            VisibleObjectSet snapshot
        )
        {
            try
            {
                SnapshotUpdated?.Invoke(snapshot);
            }
            catch (Exception ex)
            {
                // 購読側の例外で検出Service本体を停止させない。
                Debug.LogError(
                    $"[YoloXObjectDetectionService]" +
                    $"[SUBSCRIBER_ERROR] {ex}",
                    this
                );
            }
        }

        private void LogSnapshot(
            VisibleObjectSet snapshot
        )
        {
            if (!logSnapshots)
                return;

            if (snapshot == null)
                return;

            if (snapshot.Count == 0)
            {
                if (logEmptySnapshots)
                {
                    Debug.Log(
                        $"[YoloXObjectDetectionService]" +
                        $"[SNAPSHOT] " +
                        $"FrameId={snapshot.FrameId} " +
                        $"Count=0",
                        this
                    );
                }

                return;
            }

            int count =
                Mathf.Min(
                    snapshot.Count,
                    maximumLoggedObjects
                );

            StringBuilder builder =
                new StringBuilder(512);

            builder.Append(
                $"[YoloXObjectDetectionService]" +
                $"[SNAPSHOT] " +
                $"FrameId={snapshot.FrameId} " +
                $"Count={snapshot.Count}"
            );

            for (int i = 0; i < count; i++)
            {
                VisibleObject item =
                    snapshot.Objects[i];

                builder.AppendLine();

                builder.Append(
                    $"  #{i + 1} " +
                    $"Label={item.ClassLabel} " +
                    $"Confidence={item.Confidence:F3} " +
                    $"Center=(" +
                    $"{item.NormalizedCenterX:F3}," +
                    $"{item.NormalizedCenterY:F3}) " +
                    $"Size=(" +
                    $"{item.NormalizedWidth:F3}," +
                    $"{item.NormalizedHeight:F3})"
                );
            }

            Debug.Log(
                builder.ToString(),
                this
            );
        }

        private void FailInitialization(
            string message
        )
        {
            fatalError = true;
            initialized = false;
            enabled = false;

            Debug.LogError(
                message,
                this
            );
        }

        private void OnDestroy()
        {
            Interlocked.Exchange(ref destroyed, 1);
            admission.BeginShutdown();
            SnapshotUpdated = null;

            while (completedWorkers.TryDequeue(
                out YoloInferenceWorkerResult completed))
            {
                admission.TryComplete(completed.RequestId);
            }

            ReleaseFrameMats();

            Task worker = activeWorkerTask;
            Volatile.Write(ref disposeDetectorAfterDrain, 1);
            bool requiresWorkerDrain =
                worker != null && !worker.IsCompleted;
            if (requiresWorkerDrain)
            {
                // Native/OpenCV inferenceはhard cancelしない。worker return後に
                // ownershipを解放し、Unity Main Threadを待機させない。
            }
            else if (Interlocked.Exchange(
                    ref disposeDetectorAfterDrain,
                    0) != 0)
            {
                admission.TryComplete(admission.ActiveRequestId);
                YOLOXObjectDetector ownedDetector =
                    Interlocked.Exchange(ref detector, null);
                ownedDetector?.Dispose();
            }

            classLabels = Array.Empty<string>();
            visualSnapshotStore?.Clear();
            initialized = false;
        }

        private void EnsureVisualSnapshotStore()
        {
            if (visualSnapshotStore != null)
                return;

            visualSnapshotStore = new VisualFrameSnapshotStore(
                Math.Max(1, visualSnapshotCapacity),
                TimeSpan.FromSeconds(
                    Math.Max(0.1f, visualSnapshotTtlSeconds)));
        }

        private void ReleaseFrameMats()
        {
            sourceRgbaMat?.Dispose();
            sourceRgbaMat = null;

            currentWidth = 0;
            currentHeight = 0;
        }

        private sealed class YoloInferenceWorkerRequest
        {
            internal readonly long RequestId;
            internal readonly Mat OwnedRgbaSnapshot;
            internal readonly byte[] OwnedRgbaPixels;
            internal readonly int Width;
            internal readonly int Height;
            internal readonly int Rotation;
            internal readonly long CapturedAtUnixMilliseconds;
            internal readonly int StartFrame;
            internal readonly long TotalStartedTimestamp;
            internal readonly double SnapshotMilliseconds;

            internal YoloInferenceWorkerRequest(
                long requestId,
                Mat ownedRgbaSnapshot,
                byte[] ownedRgbaPixels,
                int width,
                int height,
                int rotation,
                long capturedAtUnixMilliseconds,
                int startFrame,
                long totalStartedTimestamp,
                double snapshotMilliseconds)
            {
                RequestId = requestId;
                OwnedRgbaSnapshot = ownedRgbaSnapshot;
                OwnedRgbaPixels = ownedRgbaPixels ?? Array.Empty<byte>();
                Width = width;
                Height = height;
                Rotation = rotation;
                CapturedAtUnixMilliseconds = capturedAtUnixMilliseconds;
                StartFrame = startFrame;
                TotalStartedTimestamp = totalStartedTimestamp;
                SnapshotMilliseconds = snapshotMilliseconds;
            }
        }

        private sealed class YoloInferenceWorkerResult
        {
            internal readonly long RequestId;
            internal readonly ObjectDetectionData[] RawDetections;
            internal readonly byte[] OwnedRgbaPixels;
            internal readonly int Width;
            internal readonly int Height;
            internal readonly int Rotation;
            internal readonly long CapturedAtUnixMilliseconds;
            internal readonly int StartFrame;
            internal readonly long TotalStartedTimestamp;
            internal readonly double SnapshotMilliseconds;
            internal readonly int WorkerThreadId;
            internal readonly double WorkerMilliseconds;
            internal readonly double DetectorMilliseconds;
            internal readonly bool Success;
            internal readonly string Error;

            private YoloInferenceWorkerResult(
                YoloInferenceWorkerRequest request,
                ObjectDetectionData[] rawDetections,
                int workerThreadId,
                double workerMilliseconds,
                double detectorMilliseconds,
                bool success,
                string error)
            {
                RequestId = request.RequestId;
                RawDetections = rawDetections;
                OwnedRgbaPixels = request.OwnedRgbaPixels;
                Width = request.Width;
                Height = request.Height;
                Rotation = request.Rotation;
                CapturedAtUnixMilliseconds = request.CapturedAtUnixMilliseconds;
                StartFrame = request.StartFrame;
                TotalStartedTimestamp = request.TotalStartedTimestamp;
                SnapshotMilliseconds = request.SnapshotMilliseconds;
                WorkerThreadId = workerThreadId;
                WorkerMilliseconds = workerMilliseconds;
                DetectorMilliseconds = detectorMilliseconds;
                Success = success;
                Error = error ?? string.Empty;
            }

            internal static YoloInferenceWorkerResult Succeeded(
                YoloInferenceWorkerRequest request,
                ObjectDetectionData[] rawDetections,
                int workerThreadId,
                double workerMilliseconds,
                double detectorMilliseconds)
            {
                return new YoloInferenceWorkerResult(
                    request,
                    rawDetections ?? Array.Empty<ObjectDetectionData>(),
                    workerThreadId,
                    workerMilliseconds,
                    detectorMilliseconds,
                    true,
                    string.Empty);
            }

            internal static YoloInferenceWorkerResult Failed(
                YoloInferenceWorkerRequest request,
                Exception exception,
                int workerThreadId,
                double workerMilliseconds,
                double detectorMilliseconds)
            {
                return new YoloInferenceWorkerResult(
                    request,
                    Array.Empty<ObjectDetectionData>(),
                    workerThreadId,
                    workerMilliseconds,
                    detectorMilliseconds,
                    false,
                    exception == null ? "Unknown worker failure." : exception.ToString());
            }
        }
    }
}
