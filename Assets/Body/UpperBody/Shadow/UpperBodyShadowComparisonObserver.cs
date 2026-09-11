// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Body.Frames;
using SalieriAI.Body.Frames.Observation;

using UnityEngine;

namespace SalieriAI.Body.UpperBody.Shadow
{
    [Serializable]
    public sealed class UpperBodyShadowSegmentComparison
    {
        [SerializeField] private BodySegment segment;
        [SerializeField] private bool actualAvailable;
        [SerializeField] private bool shadowAvailable;
        [SerializeField] private Quaternion actualNeutralRelativeRotation =
            Quaternion.identity;
        [SerializeField] private Quaternion shadowNeutralRelativeRotation =
            Quaternion.identity;
        [SerializeField] private float angularDifferenceDegrees = -1f;
        [SerializeField] private Vector3 actualForward = Vector3.forward;
        [SerializeField] private Vector3 shadowForward = Vector3.forward;

        public BodySegment Segment => segment;
        public bool ActualAvailable => actualAvailable;
        public bool ShadowAvailable => shadowAvailable;
        public Quaternion ActualNeutralRelativeRotation =>
            actualNeutralRelativeRotation;
        public Quaternion ShadowNeutralRelativeRotation =>
            shadowNeutralRelativeRotation;
        public float AngularDifferenceDegrees => angularDifferenceDegrees;
        public Vector3 ActualForward => actualForward;
        public Vector3 ShadowForward => shadowForward;

        internal UpperBodyShadowSegmentComparison(BodySegment bodySegment)
        {
            segment = bodySegment;
        }

        internal void Capture(
            BodySegmentObservationSnapshot actual,
            UpperBodyShadowSegmentSolution shadow)
        {
            actualAvailable = actual != null && actual.CanonicalState != null &&
                              actual.CanonicalState.Valid;
            shadowAvailable = shadow != null && shadow.Available;
            actualNeutralRelativeRotation = actualAvailable
                ? actual.CanonicalState.NeutralRelativeRotation
                : Quaternion.identity;
            shadowNeutralRelativeRotation = shadowAvailable
                ? shadow.CandidateNeutralRelativeRotation
                : Quaternion.identity;
            actualForward = actualAvailable
                ? actual.CanonicalState.Forward
                : Vector3.forward;
            shadowForward = shadowAvailable
                ? shadow.CandidateForward
                : Vector3.forward;
            angularDifferenceDegrees = actualAvailable && shadowAvailable
                ? Quaternion.Angle(
                    actualNeutralRelativeRotation,
                    shadowNeutralRelativeRotation)
                : -1f;
        }
    }

    /// <summary>
    /// Read-only comparison of U0-2 actual solved pose and U0-4 candidate.
    /// It neither changes the candidate nor writes an observed bone.
    /// </summary>
    [DefaultExecutionOrder(510)]
    [DisallowMultipleComponent]
    public sealed class UpperBodyShadowComparisonObserver : MonoBehaviour
    {
        [SerializeField] private UpperBodyShadowRuntimeHost shadowHost;
        [SerializeField] private BodyFrameObservationProvider observationProvider;

        [Header("Actual vs Shadow Angular Difference (Read Only)")]
        [SerializeField] private bool valid;
        [SerializeField] private float pelvisDegrees = -1f;
        [SerializeField] private float spineDegrees = -1f;
        [SerializeField] private float chestDegrees = -1f;
        [SerializeField] private float upperChestDegrees = -1f;
        [SerializeField] private float neckDegrees = -1f;
        [SerializeField] private float headDegrees = -1f;
        [SerializeField] private Vector3 actualHeadForward = Vector3.forward;
        [SerializeField] private Vector3 shadowHeadForward = Vector3.forward;
        [SerializeField] private UpperBodyShadowSegmentComparison[]
            segmentComparisons = CreateComparisons();

        [Header("Optional Diagnostics")]
        [SerializeField] private bool logComparison;
        [Min(1)]
        [SerializeField] private int logIntervalFrames = 120;

        public bool Valid => valid;
        public float PelvisDegrees => pelvisDegrees;
        public float SpineDegrees => spineDegrees;
        public float ChestDegrees => chestDegrees;
        public float UpperChestDegrees => upperChestDegrees;
        public float NeckDegrees => neckDegrees;
        public float HeadDegrees => headDegrees;
        public IReadOnlyList<UpperBodyShadowSegmentComparison>
            SegmentComparisons => segmentComparisons;

        private void LateUpdate()
        {
            CaptureComparison();
            if (logComparison && Time.frameCount % Mathf.Max(1, logIntervalFrames) == 0)
                LogCurrentComparison();
        }

        public void CaptureComparison()
        {
            UpperBodyShadowSolution solution = shadowHost != null
                ? shadowHost.CurrentSolution
                : null;
            valid = solution != null && solution.Valid && observationProvider != null;
            if (!valid)
            {
                ResetComparison();
                return;
            }

            EnsureComparisons();
            for (int i = 0; i < segmentComparisons.Length; i++)
            {
                BodySegment segment = segmentComparisons[i].Segment;
                segmentComparisons[i].Capture(
                    Observation(segment),
                    solution.Find(segment));
            }

            pelvisDegrees = segmentComparisons[0].AngularDifferenceDegrees;
            spineDegrees = segmentComparisons[1].AngularDifferenceDegrees;
            chestDegrees = segmentComparisons[2].AngularDifferenceDegrees;
            upperChestDegrees = segmentComparisons[3].AngularDifferenceDegrees;
            neckDegrees = segmentComparisons[4].AngularDifferenceDegrees;
            headDegrees = segmentComparisons[5].AngularDifferenceDegrees;
            actualHeadForward = observationProvider.Head.CanonicalState != null &&
                                observationProvider.Head.CanonicalState.Valid
                ? observationProvider.Head.CanonicalState.Forward
                : Vector3.forward;
            shadowHeadForward = solution.ReconstructedHeadForward;
        }

        private BodySegmentObservationSnapshot Observation(BodySegment segment)
        {
            switch (segment)
            {
                case BodySegment.Pelvis: return observationProvider.Pelvis;
                case BodySegment.Spine: return observationProvider.Spine;
                case BodySegment.Chest: return observationProvider.Chest;
                case BodySegment.UpperChest: return observationProvider.UpperChest;
                case BodySegment.Neck: return observationProvider.Neck;
                case BodySegment.Head: return observationProvider.Head;
                default: return null;
            }
        }

        private void ResetComparison()
        {
            pelvisDegrees = spineDegrees = chestDegrees = upperChestDegrees =
                neckDegrees = headDegrees = -1f;
            actualHeadForward = shadowHeadForward = Vector3.forward;
            EnsureComparisons();
            for (int i = 0; i < segmentComparisons.Length; i++)
                segmentComparisons[i].Capture(null, null);
        }

        private void EnsureComparisons()
        {
            if (segmentComparisons == null || segmentComparisons.Length != 6)
                segmentComparisons = CreateComparisons();
        }

        private static UpperBodyShadowSegmentComparison[] CreateComparisons() =>
            new[]
            {
                new UpperBodyShadowSegmentComparison(BodySegment.Pelvis),
                new UpperBodyShadowSegmentComparison(BodySegment.Spine),
                new UpperBodyShadowSegmentComparison(BodySegment.Chest),
                new UpperBodyShadowSegmentComparison(BodySegment.UpperChest),
                new UpperBodyShadowSegmentComparison(BodySegment.Neck),
                new UpperBodyShadowSegmentComparison(BodySegment.Head)
            };

        private void LogCurrentComparison()
        {
            Debug.Log(
                "[UpperBodyShadowComparison] Valid=" + valid +
                " Target=" + (shadowHost != null ? shadowHost.TargetKey : "none") +
                " PelvisDeg=" + pelvisDegrees.ToString("F3") +
                " SpineDeg=" + spineDegrees.ToString("F3") +
                " ChestDeg=" + chestDegrees.ToString("F3") +
                " UpperChestDeg=" + upperChestDegrees.ToString("F3") +
                " NeckDeg=" + neckDegrees.ToString("F3") +
                " HeadDeg=" + headDegrees.ToString("F3") +
                " ActualHeadForward=" + actualHeadForward +
                " ShadowHeadForward=" + shadowHeadForward,
                this);
        }
    }
}
