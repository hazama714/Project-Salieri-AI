// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Perception.Attention
{
    /// <summary>
    /// Canonical three-axis neck orientation value used at the sensor-pose
    /// estimation boundary. It is a software value, not measured physical
    /// feedback and not evidence that the physical neck reached the pose.
    /// </summary>
    public readonly struct NeckOrientation
    {
        public static NeckOrientation Zero =>
            new NeckOrientation(0f, 0f, 0f);

        public float YawDegrees { get; }
        public float PitchDegrees { get; }
        public float RollDegrees { get; }

        public NeckOrientation(
            float yawDegrees,
            float pitchDegrees,
            float rollDegrees)
        {
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
            RollDegrees = rollDegrees;
        }
    }
}
