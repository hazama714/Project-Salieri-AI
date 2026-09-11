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

namespace SalieriAI.Body.UpperBody.Shadow
{
    /// <summary>
    /// Pure virtual-body distribution settings for one segment. Limits are
    /// virtual shadow constraints, never physical servo limits.
    /// </summary>
    public sealed class UpperBodySegmentDistribution
    {
        public BodySegment Segment { get; }
        public bool Enabled { get; }
        public float YawWeight { get; }
        public float PitchWeight { get; }
        public float MaxYawDegrees { get; }
        public float MaxPitchDegrees { get; }

        public UpperBodySegmentDistribution(
            BodySegment segment,
            bool enabled,
            float yawWeight,
            float pitchWeight,
            float maxYawDegrees,
            float maxPitchDegrees)
        {
            if (segment == BodySegment.Unknown)
                throw new ArgumentException("Segment must be explicit.", nameof(segment));
            if (!IsFiniteNonNegative(yawWeight) ||
                !IsFiniteNonNegative(pitchWeight) ||
                !IsFiniteNonNegative(maxYawDegrees) ||
                !IsFiniteNonNegative(maxPitchDegrees))
                throw new ArgumentException("Weights and limits must be finite and non-negative.");

            Segment = segment;
            Enabled = enabled;
            YawWeight = yawWeight;
            PitchWeight = pitchWeight;
            MaxYawDegrees = maxYawDegrees;
            MaxPitchDegrees = maxPitchDegrees;
        }

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    /// <summary>
    /// Immutable, replaceable distribution profile for the pure shadow
    /// solver. U0-4 values are conservative diagnostics, not final motion.
    /// </summary>
    public sealed class UpperBodyOrientationDistributionProfile
    {
        public const string U04ProfileId =
            "upper-body-shadow-u0.4-conservative";
        public const string U04ProfileVersion = "0.1";

        private readonly UpperBodySegmentDistribution[] segments;

        public string ProfileId { get; }
        public string Version { get; }
        public IReadOnlyList<UpperBodySegmentDistribution> Segments => segments;

        private UpperBodyOrientationDistributionProfile(
            string profileId,
            string version,
            UpperBodySegmentDistribution[] segments)
        {
            ProfileId = profileId;
            Version = version;
            this.segments = segments;
        }

        public static bool TryCreate(
            string profileId,
            string version,
            IReadOnlyList<UpperBodySegmentDistribution> definitions,
            out UpperBodyOrientationDistributionProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(profileId) ||
                string.IsNullOrWhiteSpace(version))
            {
                error = "Profile ID and version are required.";
                return false;
            }
            if (definitions == null || definitions.Count == 0)
            {
                error = "At least one segment definition is required.";
                return false;
            }

            var seen = new HashSet<BodySegment>();
            var copy = new UpperBodySegmentDistribution[definitions.Count];
            float yawWeight = 0f;
            float pitchWeight = 0f;
            for (int i = 0; i < definitions.Count; i++)
            {
                UpperBodySegmentDistribution definition = definitions[i];
                if (definition == null || !seen.Add(definition.Segment))
                {
                    error = "Segment definitions must be non-null and unique.";
                    return false;
                }
                copy[i] = definition;
                if (definition.Enabled)
                {
                    yawWeight += definition.YawWeight;
                    pitchWeight += definition.PitchWeight;
                }
            }
            if (yawWeight <= 0f || pitchWeight <= 0f)
            {
                error = "Enabled segment weights must support yaw and pitch.";
                return false;
            }

            profile = new UpperBodyOrientationDistributionProfile(
                profileId.Trim(), version.Trim(), copy);
            return true;
        }

        public static UpperBodyOrientationDistributionProfile
            CreateU04Conservative()
        {
            // Head + Neck own 65% of small motion. Trunk participation is
            // present but conservative. These are replaceable shadow values.
            var values = new[]
            {
                new UpperBodySegmentDistribution(
                    BodySegment.Pelvis, true, 0.05f, 0.05f, 10f, 5f),
                new UpperBodySegmentDistribution(
                    BodySegment.Spine, true, 0.10f, 0.10f, 15f, 10f),
                new UpperBodySegmentDistribution(
                    BodySegment.Chest, true, 0.20f, 0.20f, 20f, 15f),
                new UpperBodySegmentDistribution(
                    BodySegment.UpperChest, false, 0f, 0f, 0f, 0f),
                new UpperBodySegmentDistribution(
                    BodySegment.Neck, true, 0.30f, 0.30f, 30f, 20f),
                new UpperBodySegmentDistribution(
                    BodySegment.Head, true, 0.35f, 0.35f, 35f, 25f)
            };
            if (!TryCreate(
                    U04ProfileId,
                    U04ProfileVersion,
                    values,
                    out UpperBodyOrientationDistributionProfile profile,
                    out string error))
                throw new InvalidOperationException(error);
            return profile;
        }
    }
}
