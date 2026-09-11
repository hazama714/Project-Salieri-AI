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
    /// Declares which pose source owns a sensor frame.
    ///
    /// EstimatedFromNeck remains zero for backward-compatible scene
    /// deserialization. Fixed is used when the physical sensor does not move
    /// with the neck, while EstimatedFromNeck is used for a neck-mounted
    /// sensor whose pose is estimated from the current software command.
    /// Neither value represents measured physical feedback.
    /// </summary>
    public enum SensorPoseAuthority
    {
        EstimatedFromNeck = 0,

        // Compatibility alias for scenes and code created before the
        // moving-webcam authority name was made explicit.
        CommandedPhysicalBody = EstimatedFromNeck,

        Fixed = 1
    }
}
