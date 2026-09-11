// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Skills
{
    [Serializable]
    public sealed class SkillPlanStepData
    {
        public string stepId = string.Empty;
        public string skillId = string.Empty;
        public int skillVersion = 1;
        public string inputFrom = string.Empty;
        public string onSuccess = string.Empty;
        public string onFailure = string.Empty;
    }
}
