// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Reduction
{
    public enum ExecutionCoordinatorPlanLifecycle
    {
        Accepted = 0,
        Running = 1,
        Waiting = 2,
        Cancelling = 3,
        SafetyPreempting = 4,
        Terminal = 5
    }

    public enum ExecutionCoordinatorStepLifecycle
    {
        BlockedByDependency = 0,
        Ready = 1,
        WaitingForPermissionBeforeResources = 2,
        WaitingForResource = 3,
        WaitingForPermissionBeforeStart = 4,
        Starting = 5,
        Running = 6,
        WaitingForCompletion = 7,
        Cancelling = 8,
        CleaningUp = 9,
        Terminal = 10
    }

    public enum ExecutionCoordinatorEventDisposition
    {
        Applied = 0,
        Duplicate = 1,
        Stale = 2,
        Rejected = 3
    }

    public static class ExecutionCoordinatorDiagnosticCodes
    {
        public const string DuplicateEvent = "DUPLICATE_EVENT";
        public const string StaleEvent = "STALE_EVENT";
        public const string InvalidEvent = "INVALID_EVENT";
        public const string MissingContextId = "MISSING_CONTEXT_ID";
        public const string OptionalStepFailed = "OPTIONAL_STEP_FAILED";
        public const string FallbackUsed = "FALLBACK_USED";
        public const string CompletionUnverified = "COMPLETION_UNVERIFIED";
        public const string CleanupFailed = "CLEANUP_FAILED";
        public const string ControlEffectFailed = "CONTROL_EFFECT_FAILED";
        public const string ResourceReleaseFailed =
            "RESOURCE_RELEASE_FAILED";
        public const string DuplicateLease = "DUPLICATE_LEASE";
        public const string StaleLease = "STALE_LEASE";
    }
}
