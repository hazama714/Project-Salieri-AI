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
    /// Diagnostic projection of a Humanoid bone and its U0-1 canonical
    /// neutral-relative segment state.
    /// </summary>
    [Serializable]
    public sealed class BodySegmentObservationSnapshot
    {
        [SerializeField] private BodySegment segment;
        [SerializeField] private bool boneAvailable;
        [SerializeField] private bool neutralCaptured;
        [SerializeField] private BodyReferenceFrame referenceFrame;
        [SerializeField] private BodySegment referenceSegment;
        [SerializeField] private string referenceFrameKey = string.Empty;
        [SerializeField] private int capturedFrame = -1;
        [SerializeField] private string capturedAtUtc = string.Empty;
        [SerializeField] private Quaternion neutralLocalRotation =
            Quaternion.identity;
        [SerializeField] private Quaternion currentLocalRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 worldPosition;
        [SerializeField] private Quaternion worldRotation = Quaternion.identity;
        [SerializeField] private Vector3 worldForward = Vector3.forward;
        [SerializeField] private Vector3 worldUp = Vector3.up;
        [SerializeField] private Vector3 worldRight = Vector3.right;
        [SerializeField] private Quaternion neutralRelativeRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 neutralRelativeForward =
            Vector3.forward;

        public BodySegment Segment => segment;
        public bool BoneAvailable => boneAvailable;
        public bool NeutralCaptured => neutralCaptured;
        public BodyReferenceFrame ReferenceFrame => referenceFrame;
        public BodySegment ReferenceSegment => referenceSegment;
        public string ReferenceFrameKey => referenceFrameKey;
        public int CapturedFrame => capturedFrame;
        public string CapturedAtUtc => capturedAtUtc;
        public Quaternion NeutralLocalRotation => neutralLocalRotation;
        public Quaternion CurrentLocalRotation => currentLocalRotation;
        public Vector3 WorldPosition => worldPosition;
        public Quaternion WorldRotation => worldRotation;
        public Vector3 WorldForward => worldForward;
        public Vector3 WorldUp => worldUp;
        public Vector3 WorldRight => worldRight;
        public Quaternion NeutralRelativeRotation => neutralRelativeRotation;
        public Vector3 NeutralRelativeForward => neutralRelativeForward;
        public BodySegmentOrientationState CanonicalState { get; private set; }

        internal void Configure(
            BodySegment bodySegment,
            BodyReferenceFrame frame,
            BodySegment parentSegment,
            string frameKey)
        {
            segment = bodySegment;
            referenceFrame = frame;
            referenceSegment = parentSegment;
            referenceFrameKey = frameKey ?? string.Empty;
        }

        internal bool CaptureNeutral(Transform source)
        {
            boneAvailable = source != null;
            neutralCaptured = source != null;
            neutralLocalRotation = source != null
                ? source.localRotation
                : Quaternion.identity;
            return neutralCaptured;
        }

        internal void Capture(
            Transform source,
            int frame,
            DateTime capturedUtc)
        {
            capturedFrame = frame;
            capturedAtUtc = capturedUtc.ToString("O");
            boneAvailable = source != null;
            currentLocalRotation = source != null
                ? source.localRotation
                : Quaternion.identity;
            worldPosition = source != null ? source.position : Vector3.zero;
            worldRotation = source != null
                ? source.rotation
                : Quaternion.identity;
            worldForward = source != null ? source.forward : Vector3.forward;
            worldUp = source != null ? source.up : Vector3.up;
            worldRight = source != null ? source.right : Vector3.right;

            if (!boneAvailable || !neutralCaptured ||
                !BodyOrientationMath.TryCalculateNeutralRelativeRotation(
                    neutralLocalRotation,
                    currentLocalRotation,
                    out Quaternion delta))
            {
                neutralRelativeRotation = Quaternion.identity;
                neutralRelativeForward = Vector3.forward;
                CanonicalState = BodySegmentOrientationState.CreateInvalid(
                    segment,
                    referenceFrame,
                    frame,
                    capturedUtc,
                    referenceSegment);
                return;
            }

            neutralRelativeRotation = delta;
            neutralRelativeForward = delta * CanonicalBodyAxes.Forward;
            if (!BodySegmentOrientationState.TryCreate(
                    segment,
                    referenceFrame,
                    referenceSegment,
                    delta,
                    frame,
                    capturedUtc,
                    out BodySegmentOrientationState canonical,
                    out _))
            {
                CanonicalState = BodySegmentOrientationState.CreateInvalid(
                    segment,
                    referenceFrame,
                    frame,
                    capturedUtc,
                    referenceSegment);
                return;
            }

            CanonicalState = canonical;
        }
    }
}
