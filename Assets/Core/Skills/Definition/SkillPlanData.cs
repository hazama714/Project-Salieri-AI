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
    public sealed class SkillPlanData
    {
        public int schemaVersion = 1;
        public string planId = string.Empty;
        public int version = 1;
        public string modeId = string.Empty;
        public string displayName = string.Empty;
        public string goal = string.Empty;
        public string source = string.Empty;
        public string entryStepId = string.Empty;
        public int maximumSteps;
        public float maximumExecutionSeconds;
        public SkillPlanStepData[] steps = Array.Empty<SkillPlanStepData>();
    }
}
