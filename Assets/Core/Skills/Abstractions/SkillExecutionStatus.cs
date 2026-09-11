// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Skills
{
    public enum SkillExecutionStatus
    {
        Ready = 0,
        Running = 10,
        Succeeded = 20,
        Failed = 30,
        Cancelled = 40,
        Deferred = 50,
        TimedOut = 60,
        PermissionDenied = 70,
        InvalidInput = 80,
        ResourceUnavailable = 90,
        TargetLost = 100,
        SafetyRejected = 110
    }
}
