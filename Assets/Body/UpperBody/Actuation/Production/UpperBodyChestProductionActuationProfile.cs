// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Body.Frames;
using SalieriAI.Body.UpperBody.Shadow;

namespace SalieriAI.Body.UpperBody.Actuation.Production
{
    /// <summary>
    /// Explicit U1 virtual-body profile. Values limit the first visual Chest
    /// proof only; they are not physical calibration or servo constraints.
    /// </summary>
    public sealed class UpperBodyChestProductionActuationProfile
    {
        public const string U1ProfileId =
            "upper-body-chest-production-u1";
        public const string U1ProfileVersion = "0.1";
        public const float U1MaxYawDegrees = 10f;
        public const float U1MaxPitchDegrees = 7.5f;
        public const float U1BlendDegreesPerSecond = 30f;

        public UpperBodyOrientationDistributionProfile SolverProfile { get; }
        public UpperBodyActuationProfileSelection Selection { get; }
        public float BlendDegreesPerSecond { get; }

        private UpperBodyChestProductionActuationProfile(
            UpperBodyOrientationDistributionProfile solverProfile,
            UpperBodyActuationProfileSelection selection,
            float blendDegreesPerSecond)
        {
            SolverProfile = solverProfile;
            Selection = selection;
            BlendDegreesPerSecond = blendDegreesPerSecond;
        }

        public static UpperBodyChestProductionActuationProfile CreateU1Initial()
        {
            var definitions = new[]
            {
                new UpperBodySegmentDistribution(
                    BodySegment.Chest,
                    true,
                    1f,
                    1f,
                    U1MaxYawDegrees,
                    U1MaxPitchDegrees)
            };
            if (!UpperBodyOrientationDistributionProfile.TryCreate(
                    U1ProfileId,
                    U1ProfileVersion,
                    definitions,
                    out UpperBodyOrientationDistributionProfile solverProfile,
                    out string profileError))
                throw new InvalidOperationException(profileError);
            if (!UpperBodyActuationProfileSelection.TryCreate(
                    U1ProfileId,
                    U1ProfileVersion,
                    out UpperBodyActuationProfileSelection selection,
                    out string selectionError))
                throw new InvalidOperationException(selectionError);
            return new UpperBodyChestProductionActuationProfile(
                solverProfile,
                selection,
                U1BlendDegreesPerSecond);
        }
    }
}
