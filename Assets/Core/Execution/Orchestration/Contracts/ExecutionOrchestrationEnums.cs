// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    public enum ExecutionPlanType
    {
        None = 0,
        Reaction = 1,
        Control = 2,
        Stop = 3,
        Emergency = 4,
        Recovery = 5,
        NoOp = 6
    }

    public enum ExecutionStepType
    {
        None = 0,
        Speech = 1,
        Skill = 2,
        Expression = 3,
        VrmTarget = 4,
        VirtualBody = 5,
        PhysicalBody = 6,
        WaitForCondition = 7,
        FeedbackCheck = 8,
        Clarification = 9,
        Cleanup = 10,
        Control = 11,
        NoOp = 12
    }

    public enum ExecutionStepRole
    {
        Other = 0,
        PrimaryAction = 1,
        Acknowledgement = 2,
        Fallback = 3,
        Cleanup = 4
    }

    public enum ExecutionDomain
    {
        None = 0,
        Speech = 1,
        Skill = 2,
        Expression = 3,
        Vrm = 4,
        VirtualBody = 5,
        PhysicalBody = 6,
        Feedback = 7,
        Control = 8,
        Resource = 9,
        NoOp = 10
    }

    public enum ExecutionRequiredness
    {
        Required = 0,
        Optional = 1
    }

    public enum ExecutionDependencyGate
    {
        None = 0,
        AfterAccepted = 1,
        AfterStarted = 2,
        AfterCompletionPolicy = 3,
        AfterSuccess = 4,
        AfterFailure = 5,
        AfterTerminal = 6,
        AfterSpecificOutcome = 7,
        Fallback = 8
    }

    public enum ExecutionPolicyKind
    {
        DependencyGraph = 0,
        ActionThenSpeech = 1,
        SpeechThenAction = 2,
        Parallel = 3,
        NoAcknowledgement = 4,
        NoOp = 5
    }

    public enum ExecutionCompletionKind
    {
        None = 0,
        RequestAccepted = 1,
        ExecutionStarted = 2,
        PlaybackCompleted = 3,
        VrmTargetApplied = 4,
        VirtualBodyResolved = 5,
        PhysicalDispatched = 6,
        PhysicalCompletionVerified = 7,
        PhysicalCompletionUnverifiedAllowed = 8,
        GoalConditionSatisfied = 9,
        ContactFeedbackReceived = 10,
        LogicalNoOpCompleted = 11
    }

    public enum ExecutionPlanLifecycle
    {
        Created = 0,
        Validated = 1,
        Queued = 2,
        Running = 3,
        Waiting = 4,
        Paused = 5,
        Cancelling = 6,
        Terminal = 7
    }

    public enum ExecutionStepLifecycle
    {
        Pending = 0,
        Ready = 1,
        WaitingForPermission = 2,
        WaitingForResource = 3,
        Running = 4,
        Paused = 5,
        Cancelling = 6,
        Terminal = 7
    }

    public enum ExecutionAttemptLifecycle
    {
        Created = 0,
        Running = 1,
        Cancelling = 2,
        Terminal = 3
    }

    public enum ExecutionTerminalOutcome
    {
        None = 0,
        Succeeded = 1,
        SucceededUnverified = 2,
        Failed = 3,
        Rejected = 4,
        Cancelled = 5,
        Interrupted = 6,
        SafetyPreempted = 7,
        TimedOut = 8,
        ContinueImpossible = 9,
        PartiallyCompleted = 10
    }

    public enum ExecutionControlType
    {
        None = 0,
        Cancel = 1,
        Interrupt = 2,
        Stop = 3,
        SafetyStop = 4,
        EmergencyPreempt = 5,
        Pause = 6,
        Resume = 7
    }

    public enum ExecutionControlScope
    {
        None = 0,
        Plan = 1,
        Step = 2,
        Domain = 3,
        Resource = 4,
        SafetyGroup = 5,
        AllActive = 6
    }

    public enum ExecutionControlAcceptanceStatus
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2
    }

    public enum ExecutionControlEffectStatus
    {
        NotObserved = 0,
        Applied = 1,
        PartiallyApplied = 2,
        Failed = 3,
        TimedOut = 4
    }

    public enum ExecutionResourceAccessMode
    {
        Shared = 0,
        Exclusive = 1
    }

    public enum ExecutionResourceLeaseScope
    {
        Step = 0,
        Plan = 1
    }

    public enum ExecutionResourceReleasePolicy
    {
        OnStepTerminal = 0,
        OnPlanTerminal = 1,
        Explicit = 2,
        SafetyPreempt = 3
    }

    public enum ExecutionPermissionWaitBehavior
    {
        None = 0,
        WaitWithTimeout = 1,
        RejectImmediately = 2,
        UseFallback = 3,
        SkipOptional = 4,
        SafetyBypass = 5
    }

    public enum ExecutionTimeoutAction
    {
        Fail = 0,
        Cancel = 1,
        UseFallback = 2,
        SkipOptional = 3
    }

    public enum ExecutionFailureAction
    {
        FailPlan = 0,
        ContinuePlan = 1,
        UseFallback = 2,
        MarkPartiallyCompleted = 3
    }

    public enum ExecutionCancellationScope
    {
        Step = 0,
        Plan = 1,
        Domain = 2,
        AllActive = 3
    }

    public enum ExecutionOptionalFailureBehavior
    {
        DiagnosticOnly = 0,
        PartiallyCompleted = 1,
        FailPlan = 2
    }
}
