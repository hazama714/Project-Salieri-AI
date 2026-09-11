// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Body.Retargeting.Neck
{
    /// <summary>
    /// Selects which canonical neck state drives the mechanical Virtual Body.
    /// Actual values are optional measured inputs; they are never inferred from
    /// commanded values.
    /// </summary>
    public enum VBodyNeckPoseAuthority
    {
        Commanded = 0,
        ActualPrefer = 1,
        ActualOnly = 2
    }

    /// <summary>
    /// Read-only canonical neck joint values in signed degrees.
    /// Commanded values do not prove that the physical neck reached the pose.
    /// </summary>
    public readonly struct NeckJointStateSnapshot
    {
        public float CommandedYawDegrees { get; }
        public float CommandedPitchDegrees { get; }
        public bool HasActual { get; }
        public float ActualYawDegrees { get; }
        public float ActualPitchDegrees { get; }

        public NeckJointStateSnapshot(
            float commandedYawDegrees,
            float commandedPitchDegrees,
            bool hasActual,
            float actualYawDegrees,
            float actualPitchDegrees)
        {
            CommandedYawDegrees = commandedYawDegrees;
            CommandedPitchDegrees = commandedPitchDegrees;
            HasActual = hasActual;
            ActualYawDegrees = actualYawDegrees;
            ActualPitchDegrees = actualPitchDegrees;
        }

        public static NeckJointStateSnapshot CommandedOnly(
            float yawDegrees,
            float pitchDegrees)
        {
            return new NeckJointStateSnapshot(
                yawDegrees,
                pitchDegrees,
                false,
                0f,
                0f);
        }
    }

    public interface INeckJointStateSource
    {
        bool TryGetSnapshot(out NeckJointStateSnapshot snapshot);
    }

    internal static class VBodyNeckPoseAuthoritySelector
    {
        internal static bool TrySelect(
            VBodyNeckPoseAuthority authority,
            NeckJointStateSnapshot snapshot,
            out float yawDegrees,
            out float pitchDegrees,
            out bool usedActual)
        {
            switch (authority)
            {
                case VBodyNeckPoseAuthority.Commanded:
                    yawDegrees = snapshot.CommandedYawDegrees;
                    pitchDegrees = snapshot.CommandedPitchDegrees;
                    usedActual = false;
                    return true;

                case VBodyNeckPoseAuthority.ActualPrefer:
                    if (snapshot.HasActual)
                    {
                        yawDegrees = snapshot.ActualYawDegrees;
                        pitchDegrees = snapshot.ActualPitchDegrees;
                        usedActual = true;
                    }
                    else
                    {
                        yawDegrees = snapshot.CommandedYawDegrees;
                        pitchDegrees = snapshot.CommandedPitchDegrees;
                        usedActual = false;
                    }
                    return true;

                case VBodyNeckPoseAuthority.ActualOnly:
                    if (snapshot.HasActual)
                    {
                        yawDegrees = snapshot.ActualYawDegrees;
                        pitchDegrees = snapshot.ActualPitchDegrees;
                        usedActual = true;
                        return true;
                    }
                    break;
            }

            yawDegrees = 0f;
            pitchDegrees = 0f;
            usedActual = false;
            return false;
        }
    }
}
