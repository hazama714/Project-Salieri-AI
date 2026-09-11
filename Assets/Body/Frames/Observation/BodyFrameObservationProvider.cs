// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

namespace SalieriAI.Body.Frames.Observation
{
    /// <summary>
    /// Read-only observation boundary between existing Scene/VRM frames and
    /// the Transform-independent U0-1 body orientation contracts.
    /// It does not select an authority and never writes a Transform or bone.
    /// </summary>
    [DefaultExecutionOrder(75)]
    [DisallowMultipleComponent]
    public sealed class BodyFrameObservationProvider : MonoBehaviour
    {
        public const string ObservationVersion = "body-frame-observation-v0.1";

        [Header("Existing Frame Sources (Read Only)")]
        [SerializeField] private Transform salieriRobotBodyRoot;
        [SerializeField] private Transform robotBodyFrame;
        [SerializeField] private Animator vrmAnimator;
        [SerializeField] private Transform sensorCameraFrame;
        [SerializeField] private Transform ikTargets;
        [SerializeField] private Transform spatialTargets;
        [SerializeField] private Transform worldTargets;
        [SerializeField] private Transform virtualBodyRoot;

        [Header("Neutral Capture")]
        [Min(0)]
        [SerializeField] private int neutralCaptureDelayFrames = 2;
        [SerializeField] private bool neutralCaptured;
        [SerializeField] private int neutralCapturedFrame = -1;
        [SerializeField] private string neutralCaptureStatus = "NotStarted";
        [SerializeField] private bool animatorInitializedWhenNeutralCaptured;

        [Header("Frame Observations (Read Only)")]
        [SerializeField] private BodyFrameObservationSnapshot
            salieriRobotBodyRootObservation = new BodyFrameObservationSnapshot();
        [SerializeField] private BodyFrameObservationSnapshot
            robotBodyFrameObservation = new BodyFrameObservationSnapshot();
        [SerializeField] private BodyFrameObservationSnapshot
            animatorRootObservation = new BodyFrameObservationSnapshot();
        [SerializeField] private BodyFrameObservationSnapshot
            sensorCameraFrameObservation = new BodyFrameObservationSnapshot();
        [SerializeField] private BodyFrameObservationSnapshot
            ikTargetsObservation = new BodyFrameObservationSnapshot();
        [SerializeField] private BodyFrameObservationSnapshot
            spatialTargetsObservation = new BodyFrameObservationSnapshot();
        [SerializeField] private BodyFrameObservationSnapshot
            worldTargetsObservation = new BodyFrameObservationSnapshot();
        [SerializeField] private BodyFrameObservationSnapshot
            virtualBodyRootObservation = new BodyFrameObservationSnapshot();

        [Header("Animator Segment Observations (Read Only)")]
        [SerializeField] private BodySegmentObservationSnapshot pelvis =
            new BodySegmentObservationSnapshot();
        [SerializeField] private BodySegmentObservationSnapshot spine =
            new BodySegmentObservationSnapshot();
        [SerializeField] private BodySegmentObservationSnapshot chest =
            new BodySegmentObservationSnapshot();
        [SerializeField] private BodySegmentObservationSnapshot upperChest =
            new BodySegmentObservationSnapshot();
        [SerializeField] private BodySegmentObservationSnapshot neck =
            new BodySegmentObservationSnapshot();
        [SerializeField] private BodySegmentObservationSnapshot head =
            new BodySegmentObservationSnapshot();

        [Header("Comparison Diagnostics (Read Only)")]
        [SerializeField] private int capturedFrame = -1;
        [SerializeField] private string capturedAtUtc = string.Empty;
        [SerializeField] private float salieriToAnimatorRootAngle;
        [SerializeField] private float salieriToRobotBodyFrameAngle;
        [SerializeField] private float animatorRootToRobotBodyFrameAngle;
        [SerializeField] private float animatorRootToPelvisAngle;
        [SerializeField] private float pelvisToChestAngle;
        [SerializeField] private float chestToHeadAngle;

        [Header("Optional Diagnostics")]
        [SerializeField] private bool logObservation;
        [Min(1)]
        [SerializeField] private int logIntervalFrames = 120;

        private Transform pelvisBone;
        private Transform spineBone;
        private Transform chestBone;
        private Transform upperChestBone;
        private Transform neckBone;
        private Transform headBone;
        private int initializedObservationCount;

        public bool NeutralCaptured => neutralCaptured;
        public int NeutralCapturedFrame => neutralCapturedFrame;
        public string NeutralCaptureStatus => neutralCaptureStatus;
        public bool AnimatorInitializedWhenNeutralCaptured =>
            animatorInitializedWhenNeutralCaptured;
        public bool AnimatorAvailable => vrmAnimator != null;
        public bool AnimatorIsHuman => vrmAnimator != null && vrmAnimator.isHuman;
        public bool AnimatorIsInitialized =>
            vrmAnimator != null && vrmAnimator.isInitialized;
        public int CapturedFrame => capturedFrame;
        public string CapturedAtUtc => capturedAtUtc;
        public BodyFrameObservationSnapshot SalieriRobotBodyRootObservation =>
            salieriRobotBodyRootObservation;
        public BodyFrameObservationSnapshot RobotBodyFrameObservation =>
            robotBodyFrameObservation;
        public BodyFrameObservationSnapshot AnimatorRootObservation =>
            animatorRootObservation;
        public BodyFrameObservationSnapshot SensorCameraFrameObservation =>
            sensorCameraFrameObservation;
        public BodyFrameObservationSnapshot IkTargetsObservation =>
            ikTargetsObservation;
        public BodyFrameObservationSnapshot SpatialTargetsObservation =>
            spatialTargetsObservation;
        public BodyFrameObservationSnapshot WorldTargetsObservation =>
            worldTargetsObservation;
        public BodyFrameObservationSnapshot VirtualBodyRootObservation =>
            virtualBodyRootObservation;
        public BodySegmentObservationSnapshot Pelvis => pelvis;
        public BodySegmentObservationSnapshot Spine => spine;
        public BodySegmentObservationSnapshot Chest => chest;
        public BodySegmentObservationSnapshot UpperChest => upperChest;
        public BodySegmentObservationSnapshot Neck => neck;
        public BodySegmentObservationSnapshot Head => head;
        public BodyOrientationState SalieriPlacementCandidate { get; private set; }
        public BodyOrientationState AnimatorRootPlacementCandidate { get; private set; }
        public BodyOrientationState RobotBodyCompatibilityCandidate { get; private set; }
        public float SalieriToAnimatorRootAngle => salieriToAnimatorRootAngle;
        public float SalieriToRobotBodyFrameAngle => salieriToRobotBodyFrameAngle;
        public float AnimatorRootToRobotBodyFrameAngle =>
            animatorRootToRobotBodyFrameAngle;

        private void Awake()
        {
            initializedObservationCount = 0;
            CacheAnimatorBones();
            ConfigureSegments();
        }

        private void OnEnable()
        {
            initializedObservationCount = 0;
            if (pelvisBone == null && vrmAnimator != null)
                CacheAnimatorBones();
            ConfigureSegments();
        }

        private void LateUpdate()
        {
            DateTime nowUtc = DateTime.UtcNow;
            int frame = Time.frameCount;
            TryCaptureNeutralWhenReady(frame);
            CaptureObservation(nowUtc, frame);
            if (logObservation && frame % Mathf.Max(1, logIntervalFrames) == 0)
                LogCurrentObservation();
        }

        /// <summary>
        /// Captures every configured source with one explicit frame/time pair.
        /// Exposed for diagnostics and deterministic self tests only.
        /// </summary>
        public void CaptureObservation(DateTime capturedUtc, int frame)
        {
            if (capturedUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Capture time must be UTC.", nameof(capturedUtc));
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame));

            capturedFrame = frame;
            capturedAtUtc = capturedUtc.ToString("O");
            CaptureFrameSnapshots(frame, capturedUtc);
            CaptureCanonicalCandidates(frame, capturedUtc);
            CaptureSegmentSnapshots(frame, capturedUtc);
            CaptureComparisons();
        }

        [ContextMenu("Recapture Body Segment Neutral Pose")]
        public void RecaptureNeutralPose()
        {
            CacheAnimatorBones();
            ConfigureSegments();
            neutralCaptured = CaptureAllSegmentNeutrals();
            neutralCapturedFrame = neutralCaptured ? Time.frameCount : -1;
            animatorInitializedWhenNeutralCaptured =
                vrmAnimator != null && vrmAnimator.isInitialized;
            neutralCaptureStatus = neutralCaptured
                ? "CapturedExplicitly"
                : "RequiredBoneUnavailable";
        }

        [ContextMenu("Log Current Body Frame Observation")]
        public void LogCurrentObservation()
        {
            Debug.Log(
                "[BodyFrameObservation] Version=" + ObservationVersion +
                " Frame=" + capturedFrame +
                " SalieriForward=" + salieriRobotBodyRootObservation.Forward +
                " AnimatorForward=" + animatorRootObservation.Forward +
                " RobotBodyForward=" + robotBodyFrameObservation.Forward +
                " PelvisDelta=" + pelvis.NeutralRelativeRotation +
                " ChestDelta=" + chest.NeutralRelativeRotation +
                " HeadDelta=" + head.NeutralRelativeRotation +
                " SensorRotation=" + sensorCameraFrameObservation.WorldRotation,
                this);
        }

        private void TryCaptureNeutralWhenReady(int frame)
        {
            if (neutralCaptured)
                return;

            if (vrmAnimator == null)
            {
                neutralCaptureStatus = "WaitingAnimatorReference";
                return;
            }

            if (!vrmAnimator.isHuman)
            {
                neutralCaptureStatus = "WaitingHumanoidAnimator";
                return;
            }

            if (!vrmAnimator.isInitialized)
            {
                initializedObservationCount = 0;
                neutralCaptureStatus = "WaitingAnimatorInitialized";
                return;
            }

            initializedObservationCount++;
            if (initializedObservationCount <=
                Mathf.Max(0, neutralCaptureDelayFrames))
            {
                neutralCaptureStatus = "WaitingStableFrameDelay";
                return;
            }

            CacheAnimatorBones();
            ConfigureSegments();
            neutralCaptured = CaptureAllSegmentNeutrals();
            neutralCapturedFrame = neutralCaptured ? frame : -1;
            animatorInitializedWhenNeutralCaptured = vrmAnimator.isInitialized;
            neutralCaptureStatus = neutralCaptured
                ? "CapturedAfterAnimatorInitialized"
                : "RequiredBoneUnavailable";
        }

        private bool CaptureAllSegmentNeutrals()
        {
            bool required =
                pelvis.CaptureNeutral(pelvisBone) &
                spine.CaptureNeutral(spineBone) &
                chest.CaptureNeutral(chestBone) &
                neck.CaptureNeutral(neckBone) &
                head.CaptureNeutral(headBone);
            upperChest.CaptureNeutral(upperChestBone);
            return required;
        }

        private void CacheAnimatorBones()
        {
            pelvisBone = null;
            spineBone = null;
            chestBone = null;
            upperChestBone = null;
            neckBone = null;
            headBone = null;
            if (vrmAnimator == null || !vrmAnimator.isHuman)
                return;

            pelvisBone = vrmAnimator.GetBoneTransform(HumanBodyBones.Hips);
            spineBone = vrmAnimator.GetBoneTransform(HumanBodyBones.Spine);
            chestBone = vrmAnimator.GetBoneTransform(HumanBodyBones.Chest);
            upperChestBone = vrmAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            neckBone = vrmAnimator.GetBoneTransform(HumanBodyBones.Neck);
            headBone = vrmAnimator.GetBoneTransform(HumanBodyBones.Head);
        }

        private void ConfigureSegments()
        {
            pelvis.Configure(
                BodySegment.Pelvis,
                BodyReferenceFrame.BodyOrientation,
                BodySegment.Unknown,
                "AnimatorRootCandidate");
            spine.Configure(
                BodySegment.Spine,
                BodyReferenceFrame.ParentSegment,
                BodySegment.Pelvis,
                "Pelvis");
            chest.Configure(
                BodySegment.Chest,
                BodyReferenceFrame.ParentSegment,
                BodySegment.Spine,
                "Spine");
            upperChest.Configure(
                BodySegment.UpperChest,
                BodyReferenceFrame.ParentSegment,
                BodySegment.Chest,
                "Chest");
            neck.Configure(
                BodySegment.Neck,
                BodyReferenceFrame.ParentSegment,
                upperChestBone != null ? BodySegment.UpperChest : BodySegment.Chest,
                upperChestBone != null ? "UpperChest" : "Chest");
            head.Configure(
                BodySegment.Head,
                BodyReferenceFrame.ParentSegment,
                BodySegment.Neck,
                "Neck");
        }

        private void CaptureFrameSnapshots(int frame, DateTime capturedUtc)
        {
            salieriRobotBodyRootObservation.Capture(
                "SalieriRobotBodyRoot", salieriRobotBodyRoot, frame, capturedUtc);
            robotBodyFrameObservation.Capture(
                "RobotBodyFrame", robotBodyFrame, frame, capturedUtc);
            animatorRootObservation.Capture(
                "AnimatorRoot", vrmAnimator != null ? vrmAnimator.transform : null,
                frame, capturedUtc);
            sensorCameraFrameObservation.Capture(
                "SensorCameraFrame", sensorCameraFrame, frame, capturedUtc);
            ikTargetsObservation.Capture("IKTargets", ikTargets, frame, capturedUtc);
            spatialTargetsObservation.Capture(
                "SpatialTargets", spatialTargets, frame, capturedUtc);
            worldTargetsObservation.Capture(
                "61_WorldTargets", worldTargets, frame, capturedUtc);
            virtualBodyRootObservation.Capture(
                "63_VirtualBody", virtualBodyRoot, frame, capturedUtc);
        }

        private void CaptureCanonicalCandidates(int frame, DateTime capturedUtc)
        {
            SalieriPlacementCandidate = CreateCandidate(
                salieriRobotBodyRoot, BodyOrientationSource.VirtualBody,
                frame, capturedUtc);
            AnimatorRootPlacementCandidate = CreateCandidate(
                vrmAnimator != null ? vrmAnimator.transform : null,
                BodyOrientationSource.VirtualBody,
                frame, capturedUtc);
            RobotBodyCompatibilityCandidate = CreateCandidate(
                robotBodyFrame, BodyOrientationSource.Explicit,
                frame, capturedUtc);
        }

        private void CaptureSegmentSnapshots(int frame, DateTime capturedUtc)
        {
            pelvis.Capture(pelvisBone, frame, capturedUtc);
            spine.Capture(spineBone, frame, capturedUtc);
            chest.Capture(chestBone, frame, capturedUtc);
            upperChest.Capture(upperChestBone, frame, capturedUtc);
            neck.Capture(neckBone, frame, capturedUtc);
            head.Capture(headBone, frame, capturedUtc);
        }

        private void CaptureComparisons()
        {
            salieriToAnimatorRootAngle = Angle(
                salieriRobotBodyRoot, vrmAnimator != null ? vrmAnimator.transform : null);
            salieriToRobotBodyFrameAngle = Angle(
                salieriRobotBodyRoot, robotBodyFrame);
            animatorRootToRobotBodyFrameAngle = Angle(
                vrmAnimator != null ? vrmAnimator.transform : null, robotBodyFrame);
            animatorRootToPelvisAngle = Angle(
                vrmAnimator != null ? vrmAnimator.transform : null, pelvisBone);
            pelvisToChestAngle = Angle(pelvisBone, chestBone);
            chestToHeadAngle = Angle(chestBone, headBone);
        }

        private static BodyOrientationState CreateCandidate(
            Transform source,
            BodyOrientationSource orientationSource,
            int frame,
            DateTime capturedUtc)
        {
            if (source == null || !BodyOrientationState.TryCreate(
                    frame,
                    capturedUtc,
                    source.rotation,
                    Quaternion.identity,
                    orientationSource,
                    out BodyOrientationState state,
                    out _))
            {
                return BodyOrientationState.CreateInvalid(
                    frame, capturedUtc, orientationSource);
            }

            return state;
        }

        private static float Angle(Transform left, Transform right)
        {
            return left != null && right != null
                ? Quaternion.Angle(left.rotation, right.rotation)
                : -1f;
        }
    }
}
