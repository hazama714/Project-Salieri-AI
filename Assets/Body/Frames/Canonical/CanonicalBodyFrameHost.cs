// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.Frames.Observation;

using UnityEngine;

namespace SalieriAI.Body.Frames.Canonical
{
    /// <summary>
    /// Read-only semantic host for the explicit Canonical Body Frame
    /// hierarchy. It observes the three frame Transforms and projects their
    /// values into the frozen U0-1 contracts. It owns no orientation policy,
    /// solver, retargeting, or physical output.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class CanonicalBodyFrameHost : MonoBehaviour
    {
        public const string ContractVersion = "canonical-body-frames-v0.1";

        [Header("Canonical Frames")]
        [SerializeField] private Transform bodyPlacementFrame;
        [SerializeField] private Transform bodyOrientationFrame;
        [SerializeField] private Transform virtualHipsFrame;

        [Header("Read-only Comparison Source")]
        [SerializeField] private BodyFrameObservationProvider observationProvider;

        [Header("Explicit Initial Source")]
        [SerializeField] private BodyOrientationSource orientationSource =
            BodyOrientationSource.InitialPose;
        [SerializeField] private string placementSourceKey =
            "VRM Animator Root initial world pose";

        [Header("Semantic Responsibilities")]
        [SerializeField] private string bodyPlacementResponsibility =
            "Canonical virtual-body world position and placement rotation";
        [SerializeField] private string bodyOrientationResponsibility =
            "Canonical +Z forward / +Y up / +X right relative to placement";
        [SerializeField] private string virtualHipsResponsibility =
            "Virtual pelvis orientation delta relative to body orientation";

        [Header("Runtime State (Read Only)")]
        [SerializeField] private bool valid;
        [SerializeField] private int capturedFrame = -1;
        [SerializeField] private string capturedAtUtc = string.Empty;
        [SerializeField] private Quaternion placementWorldRotation =
            Quaternion.identity;
        [SerializeField] private Quaternion orientationRelativeToPlacement =
            Quaternion.identity;
        [SerializeField] private Quaternion bodyOrientationWorldRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 forward = Vector3.forward;
        [SerializeField] private Vector3 up = Vector3.up;
        [SerializeField] private Vector3 right = Vector3.right;
        [SerializeField] private Quaternion virtualHipsLocalRotation =
            Quaternion.identity;

        [Header("Shadow Comparison (Read Only)")]
        [SerializeField] private float canonicalToSalieriRootAngle = -1f;
        [SerializeField] private float canonicalToAnimatorRootAngle = -1f;
        [SerializeField] private float canonicalToRobotBodyFrameAngle = -1f;
        [SerializeField] private float canonicalToVrmHipsAngle = -1f;
        [SerializeField] private float canonicalToVrmChestAngle = -1f;

        [Header("Optional Diagnostics")]
        [SerializeField] private bool logObservation;
        [Min(1)]
        [SerializeField] private int logIntervalFrames = 120;

        public bool Valid => valid;
        public int CapturedFrame => capturedFrame;
        public string CapturedAtUtc => capturedAtUtc;
        public BodyOrientationSource OrientationSource => orientationSource;
        public string PlacementSourceKey => placementSourceKey;
        public string BodyPlacementResponsibility =>
            bodyPlacementResponsibility;
        public string BodyOrientationResponsibility =>
            bodyOrientationResponsibility;
        public string VirtualHipsResponsibility =>
            virtualHipsResponsibility;
        public Transform BodyPlacementFrame => bodyPlacementFrame;
        public Transform BodyOrientationFrame => bodyOrientationFrame;
        public Transform VirtualHipsFrame => virtualHipsFrame;
        public BodyOrientationState CurrentBodyOrientation { get; private set; }
        public BodySegmentOrientationState CurrentVirtualHips { get; private set; }
        public Vector3 Forward => forward;
        public Vector3 Up => up;
        public Vector3 Right => right;
        public float CanonicalToSalieriRootAngle => canonicalToSalieriRootAngle;
        public float CanonicalToAnimatorRootAngle => canonicalToAnimatorRootAngle;
        public float CanonicalToRobotBodyFrameAngle =>
            canonicalToRobotBodyFrameAngle;
        public float CanonicalToVrmHipsAngle => canonicalToVrmHipsAngle;
        public float CanonicalToVrmChestAngle => canonicalToVrmChestAngle;

        private void LateUpdate()
        {
            Capture(DateTime.UtcNow, Time.frameCount);
            if (logObservation && Time.frameCount %
                Mathf.Max(1, logIntervalFrames) == 0)
            {
                LogCurrentState();
            }
        }

        /// <summary>
        /// Reads the canonical hierarchy once using one explicit frame/time.
        /// This method never changes any Transform.
        /// </summary>
        public void Capture(DateTime capturedUtc, int frame)
        {
            if (capturedUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Capture time must be UTC.", nameof(capturedUtc));
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame));

            capturedFrame = frame;
            capturedAtUtc = capturedUtc.ToString("O");
            valid = HasValidHierarchy();
            if (!valid)
            {
                CurrentBodyOrientation = BodyOrientationState.CreateInvalid(
                    frame, capturedUtc, orientationSource);
                CurrentVirtualHips = BodySegmentOrientationState.CreateInvalid(
                    BodySegment.Pelvis,
                    BodyReferenceFrame.BodyOrientation,
                    frame,
                    capturedUtc);
                ResetRuntimeValues();
                return;
            }

            placementWorldRotation = bodyPlacementFrame.rotation;
            orientationRelativeToPlacement = bodyOrientationFrame.localRotation;
            virtualHipsLocalRotation = virtualHipsFrame.localRotation;

            bool orientationCreated = BodyOrientationState.TryCreate(
                frame,
                capturedUtc,
                placementWorldRotation,
                orientationRelativeToPlacement,
                orientationSource,
                out BodyOrientationState orientation,
                out _);
            bool hipsCreated = BodySegmentOrientationState.TryCreate(
                BodySegment.Pelvis,
                BodyReferenceFrame.BodyOrientation,
                virtualHipsLocalRotation,
                frame,
                capturedUtc,
                out BodySegmentOrientationState hips,
                out _);

            valid = orientationCreated && hipsCreated;
            CurrentBodyOrientation = orientationCreated
                ? orientation
                : BodyOrientationState.CreateInvalid(
                    frame, capturedUtc, orientationSource);
            CurrentVirtualHips = hipsCreated
                ? hips
                : BodySegmentOrientationState.CreateInvalid(
                    BodySegment.Pelvis,
                    BodyReferenceFrame.BodyOrientation,
                    frame,
                    capturedUtc);

            bodyOrientationWorldRotation = CurrentBodyOrientation.WorldRotation;
            forward = CurrentBodyOrientation.Forward;
            up = CurrentBodyOrientation.Up;
            right = CurrentBodyOrientation.Right;
            CaptureComparisons();
        }

        [ContextMenu("Log Canonical Body Frame State")]
        public void LogCurrentState()
        {
            Debug.Log(
                "[CanonicalBodyFrameHost] Version=" + ContractVersion +
                " Valid=" + valid +
                " Frame=" + capturedFrame +
                " Source=" + orientationSource +
                " PlacementPos=" +
                    (bodyPlacementFrame != null
                        ? bodyPlacementFrame.position.ToString()
                        : "unavailable") +
                " PlacementRot=" + placementWorldRotation +
                " RelativeRot=" + orientationRelativeToPlacement +
                " WorldRot=" + bodyOrientationWorldRotation +
                " Forward=" + forward +
                " VirtualHipsDelta=" + virtualHipsLocalRotation +
                " ToRobotBodyDeg=" + canonicalToRobotBodyFrameAngle +
                " ToAnimatorDeg=" + canonicalToAnimatorRootAngle,
                this);
        }

        private bool HasValidHierarchy()
        {
            return bodyPlacementFrame != null &&
                   bodyOrientationFrame != null &&
                   virtualHipsFrame != null &&
                   bodyOrientationFrame.parent == bodyPlacementFrame &&
                   virtualHipsFrame.parent == bodyOrientationFrame &&
                   orientationSource != BodyOrientationSource.Unknown;
        }

        private void CaptureComparisons()
        {
            if (observationProvider == null ||
                CurrentBodyOrientation == null ||
                !CurrentBodyOrientation.Valid)
            {
                ResetComparisonAngles();
                return;
            }

            Quaternion canonical = CurrentBodyOrientation.WorldRotation;
            canonicalToSalieriRootAngle = Angle(
                canonical,
                observationProvider.SalieriRobotBodyRootObservation);
            canonicalToAnimatorRootAngle = Angle(
                canonical,
                observationProvider.AnimatorRootObservation);
            canonicalToRobotBodyFrameAngle = Angle(
                canonical,
                observationProvider.RobotBodyFrameObservation);
            canonicalToVrmHipsAngle = Angle(
                canonical,
                observationProvider.Pelvis);
            canonicalToVrmChestAngle = Angle(
                canonical,
                observationProvider.Chest);
        }

        private void ResetRuntimeValues()
        {
            placementWorldRotation = Quaternion.identity;
            orientationRelativeToPlacement = Quaternion.identity;
            bodyOrientationWorldRotation = Quaternion.identity;
            forward = Vector3.forward;
            up = Vector3.up;
            right = Vector3.right;
            virtualHipsLocalRotation = Quaternion.identity;
            ResetComparisonAngles();
        }

        private void ResetComparisonAngles()
        {
            canonicalToSalieriRootAngle = -1f;
            canonicalToAnimatorRootAngle = -1f;
            canonicalToRobotBodyFrameAngle = -1f;
            canonicalToVrmHipsAngle = -1f;
            canonicalToVrmChestAngle = -1f;
        }

        private static float Angle(
            Quaternion canonical,
            BodyFrameObservationSnapshot observed)
        {
            return observed != null && observed.Valid
                ? Quaternion.Angle(canonical, observed.WorldRotation)
                : -1f;
        }

        private static float Angle(
            Quaternion canonical,
            BodySegmentObservationSnapshot observed)
        {
            return observed != null && observed.BoneAvailable
                ? Quaternion.Angle(canonical, observed.WorldRotation)
                : -1f;
        }
    }
}
