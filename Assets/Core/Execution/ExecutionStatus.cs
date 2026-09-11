// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

public enum ExecutionStatus
{
    None = 0,

    Queued = 10,
    Running = 20,

    Completed = 30,
    Failed = 40,
    Cancelled = 50,

    Rejected = 60
}