// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Runtime
{
    public enum ExecutionCoordinatorEventType
    {
        None = 0,
        PlanReceived = 1,
        PlanStartRequested = 2,
        PermissionGranted = 3,
        PermissionDenied = 4,
        PermissionWaitTimedOut = 5,
        LeaseAcquired = 6,
        LeaseRejected = 7,
        LeaseReleased = 8,
        LeaseReleaseFailed = 9,
        ExecutorStartAccepted = 10,
        ExecutorStartRejected = 11,
        DomainStageChanged = 12,
        CompletionPolicyReached = 13,
        ExecutorTerminal = 14,
        CancelRequested = 15,
        InterruptRequested = 16,
        SafetyStopRequested = 17,
        EmergencyPreemptRequested = 18,
        ControlDispatched = 19,
        ControlEffectConfirmed = 20,
        ControlEffectFailed = 21,
        StepTimedOut = 22,
        AttemptTimedOut = 23,
        CleanupCompleted = 24,
        CleanupFailed = 25,
        ResourceAvailabilityEvaluated = 26,
        ResourceWaitTimedOut = 27
    }

    public enum ExecutionCoordinatorEffectIntentType
    {
        None = 0,
        CheckPermission = 1,
        AcquireResourceLease = 2,
        ReleaseResourceLease = 3,
        StartExecutor = 4,
        RequestExecutorCancel = 5,
        RequestExecutorInterrupt = 6,
        RequestSafetyPreempt = 7,
        StartTimeoutWatch = 8,
        CancelTimeoutWatch = 9,
        BeginCleanup = 10,
        PublishLifecycleTrace = 11,
        PublishTerminalResult = 12
    }
}
