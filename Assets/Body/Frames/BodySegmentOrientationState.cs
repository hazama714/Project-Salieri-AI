// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

namespace SalieriAI.Body.Frames
{
    /// <summary>
    /// Immutable orientation delta for a body segment. The value is always a
    /// neutral-relative quaternion and its reference frame is explicit.
    /// </summary>
    public sealed class BodySegmentOrientationState
    {
        public BodySegment Segment { get; }
        public bool Valid { get; }
        public BodyReferenceFrame ReferenceFrame { get; }
        public BodySegment ReferenceSegment { get; }
        public Quaternion NeutralRelativeRotation { get; }
        public Vector3 Forward { get; }
        public int CapturedFrame { get; }
        public DateTime CapturedAtUtc { get; }
        public string Version => CanonicalBodyAxes.ContractVersion;

        private BodySegmentOrientationState(
            BodySegment segment,
            bool valid,
            BodyReferenceFrame referenceFrame,
            BodySegment referenceSegment,
            Quaternion neutralRelativeRotation,
            int capturedFrame,
            DateTime capturedAtUtc)
        {
            Segment = segment;
            Valid = valid;
            ReferenceFrame = referenceFrame;
            ReferenceSegment = referenceSegment;
            NeutralRelativeRotation = neutralRelativeRotation;
            Forward = neutralRelativeRotation * CanonicalBodyAxes.Forward;
            CapturedFrame = capturedFrame;
            CapturedAtUtc = capturedAtUtc;
        }

        public static bool TryCreate(
            BodySegment segment,
            BodyReferenceFrame referenceFrame,
            Quaternion neutralRelativeRotation,
            int capturedFrame,
            DateTime capturedAtUtc,
            out BodySegmentOrientationState state,
            out string error)
        {
            return TryCreate(
                segment,
                referenceFrame,
                BodySegment.Unknown,
                neutralRelativeRotation,
                capturedFrame,
                capturedAtUtc,
                out state,
                out error);
        }

        public static bool TryCreate(
            BodySegment segment,
            BodyReferenceFrame referenceFrame,
            BodySegment referenceSegment,
            Quaternion neutralRelativeRotation,
            int capturedFrame,
            DateTime capturedAtUtc,
            out BodySegmentOrientationState state,
            out string error)
        {
            state = null;
            error = string.Empty;
            if (segment == BodySegment.Unknown)
            {
                error = "Body segment must be explicit.";
                return false;
            }

            if (referenceFrame == BodyReferenceFrame.Unknown)
            {
                error = "Segment reference frame must be explicit.";
                return false;
            }

            if (referenceFrame == BodyReferenceFrame.ParentSegment)
            {
                if (referenceSegment == BodySegment.Unknown ||
                    referenceSegment == segment)
                {
                    error = "Parent segment reference must identify a different segment.";
                    return false;
                }
            }
            else if (referenceSegment != BodySegment.Unknown)
            {
                error = "Reference segment is only valid for ParentSegment.";
                return false;
            }

            if (capturedFrame < 0)
            {
                error = "Captured frame must be zero or greater.";
                return false;
            }

            if (capturedAtUtc.Kind != DateTimeKind.Utc)
            {
                error = "Captured time must be explicit UTC.";
                return false;
            }

            if (!BodyOrientationMath.TryNormalize(
                    neutralRelativeRotation,
                    out Quaternion normalized))
            {
                error = "Neutral-relative rotation is invalid.";
                return false;
            }

            state = new BodySegmentOrientationState(
                segment,
                true,
                referenceFrame,
                referenceSegment,
                normalized,
                capturedFrame,
                capturedAtUtc);
            return true;
        }

        public static BodySegmentOrientationState CreateInvalid(
            BodySegment segment,
            BodyReferenceFrame referenceFrame,
            int capturedFrame,
            DateTime capturedAtUtc,
            BodySegment referenceSegment = BodySegment.Unknown)
        {
            if (capturedAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Captured time must be explicit UTC.",
                    nameof(capturedAtUtc));
            }

            return new BodySegmentOrientationState(
                segment,
                false,
                referenceFrame,
                referenceSegment,
                Quaternion.identity,
                capturedFrame,
                capturedAtUtc);
        }
    }
}
