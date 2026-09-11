// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

namespace SalieriAI.Body.Retargeting.Neck
{
    /// <summary>
    /// Explicit calibration contract from the virtual solved neck pose to a
    /// candidate physical neck pose. It contains no servo or output identity.
    /// </summary>
    [Serializable]
    public sealed class VirtualNeckRetargetProfile
    {
        public const string DefaultProfileId =
            "salieri.virtual-neck-retarget";
        public const string DefaultProfileVersion = "0.1";

        [SerializeField] private string profileId = DefaultProfileId;
        [SerializeField] private string profileVersion = DefaultProfileVersion;

        [SerializeField] private float yawSign = -1f;
        [SerializeField] private float pitchSign = 1f;
        [SerializeField] private float yawGain = 1f;
        [SerializeField] private float pitchGain = 1f;
        [SerializeField] private float yawOffsetDegrees;
        [SerializeField] private float pitchOffsetDegrees;

        // These defaults mirror the effective MainScene physical range:
        // NeckController relative limits intersected with servo ranges.
        [SerializeField] private float yawMinDegrees = -60f;
        [SerializeField] private float yawMaxDegrees = 60f;
        [SerializeField] private float pitchMinDegrees = -30f;
        [SerializeField] private float pitchMaxDegrees = 30f;

        // Zero means disabled. The current physical controller has no formal
        // degrees-per-second limit that can be reused without invention.
        [SerializeField] private float yawMaxSpeedDegreesPerSecond;
        [SerializeField] private float pitchMaxSpeedDegreesPerSecond;

        public string ProfileId => profileId;
        public string ProfileVersion => profileVersion;
        public float YawSign => yawSign;
        public float PitchSign => pitchSign;
        public float YawGain => yawGain;
        public float PitchGain => pitchGain;
        public float YawOffsetDegrees => yawOffsetDegrees;
        public float PitchOffsetDegrees => pitchOffsetDegrees;
        public float YawMinDegrees => yawMinDegrees;
        public float YawMaxDegrees => yawMaxDegrees;
        public float PitchMinDegrees => pitchMinDegrees;
        public float PitchMaxDegrees => pitchMaxDegrees;
        public float YawMaxSpeedDegreesPerSecond =>
            yawMaxSpeedDegreesPerSecond;
        public float PitchMaxSpeedDegreesPerSecond =>
            pitchMaxSpeedDegreesPerSecond;

        public VirtualNeckRetargetProfile()
        {
        }

        public VirtualNeckRetargetProfile(
            float configuredYawSign,
            float configuredPitchSign,
            float configuredYawGain,
            float configuredPitchGain,
            float configuredYawOffset,
            float configuredPitchOffset,
            float configuredYawMin,
            float configuredYawMax,
            float configuredPitchMin,
            float configuredPitchMax,
            float configuredYawMaxSpeed,
            float configuredPitchMaxSpeed,
            string configuredProfileId = DefaultProfileId,
            string configuredProfileVersion = DefaultProfileVersion)
        {
            profileId = configuredProfileId;
            profileVersion = configuredProfileVersion;
            yawSign = configuredYawSign;
            pitchSign = configuredPitchSign;
            yawGain = configuredYawGain;
            pitchGain = configuredPitchGain;
            yawOffsetDegrees = configuredYawOffset;
            pitchOffsetDegrees = configuredPitchOffset;
            yawMinDegrees = configuredYawMin;
            yawMaxDegrees = configuredYawMax;
            pitchMinDegrees = configuredPitchMin;
            pitchMaxDegrees = configuredPitchMax;
            yawMaxSpeedDegreesPerSecond = configuredYawMaxSpeed;
            pitchMaxSpeedDegreesPerSecond = configuredPitchMaxSpeed;
        }

        public void ConfigurePhysicalRange(
            float yawMin,
            float yawMax,
            float pitchMin,
            float pitchMax)
        {
            yawMinDegrees = yawMin;
            yawMaxDegrees = yawMax;
            pitchMinDegrees = pitchMin;
            pitchMaxDegrees = pitchMax;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(profileId) ||
                string.IsNullOrWhiteSpace(profileVersion))
            {
                error = "Profile identity/version is missing.";
                return false;
            }
            if (!IsFinite(yawSign) || !IsFinite(pitchSign) ||
                !Mathf.Approximately(Mathf.Abs(yawSign), 1f) ||
                !Mathf.Approximately(Mathf.Abs(pitchSign), 1f))
            {
                error = "Axis signs must be exactly -1 or +1.";
                return false;
            }
            if (!IsFinite(yawGain) || !IsFinite(pitchGain) ||
                yawGain < 0f || pitchGain < 0f)
            {
                error = "Gains must be finite and non-negative.";
                return false;
            }
            if (!IsFinite(yawOffsetDegrees) ||
                !IsFinite(pitchOffsetDegrees) ||
                !IsFinite(yawMinDegrees) ||
                !IsFinite(yawMaxDegrees) ||
                !IsFinite(pitchMinDegrees) ||
                !IsFinite(pitchMaxDegrees) ||
                yawMinDegrees > yawMaxDegrees ||
                pitchMinDegrees > pitchMaxDegrees)
            {
                error = "Offsets or physical ranges are invalid.";
                return false;
            }
            if (!IsFinite(yawMaxSpeedDegreesPerSecond) ||
                !IsFinite(pitchMaxSpeedDegreesPerSecond) ||
                yawMaxSpeedDegreesPerSecond < 0f ||
                pitchMaxSpeedDegreesPerSecond < 0f)
            {
                error = "Maximum speeds must be finite and non-negative.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
