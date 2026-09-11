// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Body.Joint;

using UnityEngine;

namespace SalieriAI.Body.Retargeting.Neck
{
    /// <summary>
    /// The single runtime writer for the mechanical VBody neck constraints.
    /// It mirrors canonical state only and never writes to a physical servo.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class VBodyNeckPoseDriver : MonoBehaviour
    {
        public enum CarrierAxisSign
        {
            Positive = 0,
            Negative = 1
        }

        [Header("State Source")]
        [SerializeField] private MonoBehaviour stateSourceBehaviour = null;

        [Header("VBody Constraints")]
        [SerializeField] private BodyJointConstraint yawConstraint = null;
        [SerializeField] private BodyJointConstraint pitchConstraint = null;

        [Header("Carrier Axis Calibration")]
        [Tooltip(
            "Maps CommandedYawRelativeAngle to the calibrated VBody yaw " +
            "carrier direction. This is independent of physical servo invert.")]
        [SerializeField]
        private CarrierAxisSign yawCarrierSign = CarrierAxisSign.Negative;

        [Tooltip(
            "Maps CommandedPitchRelativeAngle to the calibrated VBody pitch " +
            "carrier direction. This is independent of physical servo invert.")]
        [SerializeField]
        private CarrierAxisSign pitchCarrierSign = CarrierAxisSign.Positive;

        [Header("Pose Authority")]
        [SerializeField]
        private VBodyNeckPoseAuthority authority =
            VBodyNeckPoseAuthority.Commanded;

        private INeckJointStateSource stateSource;

        public VBodyNeckPoseAuthority Authority => authority;
        public BodyJointConstraint YawConstraint => yawConstraint;
        public BodyJointConstraint PitchConstraint => pitchConstraint;
        public float YawCarrierSign => ToMultiplier(yawCarrierSign);
        public float PitchCarrierSign => ToMultiplier(pitchCarrierSign);
        public bool LastSnapshotAvailable { get; private set; }
        public bool LastPoseApplied { get; private set; }
        public bool LastPoseUsedActual { get; private set; }
        public float LastAppliedYawDegrees { get; private set; }
        public float LastAppliedPitchDegrees { get; private set; }

        private void Awake()
        {
            ResolveSerializedStateSource();
        }

        private void OnEnable()
        {
            ResolveSerializedStateSource();
        }

        private void LateUpdate()
        {
            ApplyLatestPose();
        }

        public bool ApplyLatestPose()
        {
            if (stateSource == null)
                ResolveSerializedStateSource();

            if (stateSource == null ||
                yawConstraint == null ||
                pitchConstraint == null ||
                !stateSource.TryGetSnapshot(out NeckJointStateSnapshot snapshot))
            {
                LastSnapshotAvailable = false;
                LastPoseApplied = false;
                LastPoseUsedActual = false;
                return false;
            }

            LastSnapshotAvailable = true;
            if (!VBodyNeckPoseAuthoritySelector.TrySelect(
                    authority,
                    snapshot,
                    out float yawDegrees,
                    out float pitchDegrees,
                    out bool usedActual))
            {
                // ActualOnly with no measured state deliberately leaves the
                // existing VBody pose untouched.
                LastPoseApplied = false;
                LastPoseUsedActual = false;
                return false;
            }

            float appliedYaw = yawDegrees * YawCarrierSign;
            float appliedPitch = pitchDegrees * PitchCarrierSign;
            yawConstraint.SetTargetAngle(appliedYaw);
            pitchConstraint.SetTargetAngle(appliedPitch);

            LastPoseApplied = true;
            LastPoseUsedActual = usedActual;
            LastAppliedYawDegrees = yawConstraint.AppliedAngle;
            LastAppliedPitchDegrees = pitchConstraint.AppliedAngle;
            return true;
        }

        private void ResolveSerializedStateSource()
        {
            stateSource = stateSourceBehaviour as INeckJointStateSource;
        }

        internal void SetStateSourceForTesting(INeckJointStateSource source)
        {
            stateSource = source;
        }

        internal void SetAuthorityForTesting(VBodyNeckPoseAuthority value)
        {
            authority = value;
        }

        internal void SetCarrierSignsForTesting(
            CarrierAxisSign yaw,
            CarrierAxisSign pitch)
        {
            yawCarrierSign = yaw;
            pitchCarrierSign = pitch;
        }

        private static float ToMultiplier(CarrierAxisSign value)
        {
            return value == CarrierAxisSign.Negative ? -1f : 1f;
        }
    }
}
