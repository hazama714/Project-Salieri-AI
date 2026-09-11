// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Affordance
{
    public sealed class BodyCapability
    {
        public bool HasNeck { get; set; } = true;
        public bool HasTorso { get; set; }
        public bool HasArm { get; set; }
        public bool CanMoveBase { get; set; }

        public static BodyCapability CurrentMinimum()
        {
            return new BodyCapability
            {
                HasNeck = true,
                HasTorso = false,
                HasArm = false,
                CanMoveBase = false
            };
        }
    }
}