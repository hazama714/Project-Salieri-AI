// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Validation
{
    public static class ExecutionPlanValidationFailureCodes
    {
        public const string NullPlan = "NullPlan";
        public const string MissingPlanId = "MissingPlanId";
        public const string MissingPolicyVersion =
            "MissingPolicyVersion";
        public const string UnsupportedPolicyVersion =
            "UnsupportedPolicyVersion";
        public const string MissingInteractionId =
            "MissingInteractionId";
        public const string MissingInputId = "MissingInputId";
        public const string MissingAnalysisId = "MissingAnalysisId";
        public const string MissingReactionId = "MissingReactionId";
        public const string InvalidSubmissionProvenance =
            "InvalidSubmissionProvenance";
        public const string InvalidPlanGeneration =
            "InvalidPlanGeneration";
        public const string InvalidPlanType = "InvalidPlanType";
        public const string EmptyStepCollection =
            "EmptyStepCollection";
        public const string NullStep = "NullStep";
        public const string MissingStepId = "MissingStepId";
        public const string InvalidStepType = "InvalidStepType";
        public const string InvalidDomain = "InvalidDomain";
        public const string InvalidNoOpSemantics =
            "InvalidNoOpSemantics";
        public const string InvalidLookAroundSemantics =
            "InvalidLookAroundSemantics";
        public const string DuplicateStepId = "DuplicateStepId";
        public const string MissingRequestId = "MissingRequestId";
        public const string DuplicateRequestId =
            "DuplicateRequestId";
        public const string MissingRequestReference =
            "MissingRequestReference";
        public const string RequestReferenceIdMismatch =
            "RequestReferenceIdMismatch";
        public const string StepPlanIdMismatch =
            "StepPlanIdMismatch";
        public const string MissingDependencyId =
            "MissingDependencyId";
        public const string DependencyPlanIdMismatch =
            "DependencyPlanIdMismatch";
        public const string MissingDependencyTarget =
            "MissingDependencyTarget";
        public const string SelfDependency = "SelfDependency";
        public const string DuplicateDependency =
            "DuplicateDependency";
        public const string DependencyCycle = "DependencyCycle";
        public const string FallbackCycle = "FallbackCycle";
        public const string InvalidCompletionPolicy =
            "InvalidCompletionPolicy";
        public const string DomainCompletionMismatch =
            "DomainCompletionMismatch";
        public const string InvalidRetryPolicy =
            "InvalidRetryPolicy";
        public const string RetryUsesForbiddenFailure =
            "RetryUsesForbiddenFailure";
        public const string InvalidPermissionWaitPolicy =
            "InvalidPermissionWaitPolicy";
        public const string MissingPermissionTimeout =
            "MissingPermissionTimeout";
        public const string MissingTimeoutPolicy =
            "MissingTimeoutPolicy";
        public const string InvalidResourceWaitTimeoutPolicy =
            "InvalidResourceWaitTimeoutPolicy";
        public const string MissingFailurePolicy =
            "MissingFailurePolicy";
        public const string MissingCancellationPolicy =
            "MissingCancellationPolicy";
        public const string MissingPlanPolicy =
            "MissingPlanPolicy";
        public const string AckMustBeOptional = "AckMustBeOptional";
        public const string AckStartsBeforePrimaryAccepted =
            "AckStartsBeforePrimaryAccepted";
        public const string PrimaryBodyMustBeRequired =
            "PrimaryBodyMustBeRequired";
        public const string MissingPrimaryBodyStep =
            "MissingPrimaryBodyStep";
        public const string PhysicalUnverifiedNotDeclared =
            "PhysicalUnverifiedNotDeclared";
        public const string PhysicalCompletionFalselyVerified =
            "PhysicalCompletionFalselyVerified";
        public const string OptionalAckFailureEscalatesPlan =
            "OptionalAckFailureEscalatesPlan";
        public const string EmergencyUsesNormalPermissionWait =
            "EmergencyUsesNormalPermissionWait";
        public const string EmergencyUsesNormalDependencyWait =
            "EmergencyUsesNormalDependencyWait";
        public const string EmergencyUsesNormalResourceWait =
            "EmergencyUsesNormalResourceWait";
        public const string EmergencyRequiresAck =
            "EmergencyRequiresAck";
        public const string InvalidControlPriority =
            "InvalidControlPriority";
        public const string DuplicateResourceRequirement =
            "DuplicateResourceRequirement";
        public const string ConflictingResourceAccess =
            "ConflictingResourceAccess";
        public const string MissingResourceId = "MissingResourceId";
        public const string RuntimeSnapshotIdMismatch =
            "RuntimeSnapshotIdMismatch";
        public const string InvalidLifecycleOutcomeCombination =
            "InvalidLifecycleOutcomeCombination";
        public const string InternalValidationError =
            "InternalValidationError";
    }

    public static class ExecutionFailureCodes
    {
        public const string InvalidRequest = "InvalidRequest";
        public const string PermissionDenied = "PermissionDenied";
        public const string ResourceUnavailable =
            "ResourceUnavailable";
        public const string CapabilityUnavailable =
            "CapabilityUnavailable";
        public const string SafetyRejected = "SafetyRejected";
        public const string Emergency = "Emergency";
        public const string Cancellation = "Cancellation";
        public const string TargetUnavailable = "TargetUnavailable";
        public const string TargetLost = "TargetLost";
        public const string SynthesisFailed = "SynthesisFailed";
        public const string PlaybackFailed = "PlaybackFailed";
        public const string DispatchRejected = "DispatchRejected";
        public const string DispatchFailed = "DispatchFailed";
        public const string HardwareDisconnected =
            "HardwareDisconnected";
        public const string CompletionEvidenceUnavailable =
            "CompletionEvidenceUnavailable";
        public const string ContinueImpossible =
            "ContinueImpossible";
        public const string Superseded = "Superseded";
        public const string ComponentDisabled = "ComponentDisabled";
        public const string CancelledByOwner = "CancelledByOwner";
        public const string InterruptedByPriority =
            "InterruptedByPriority";
        public const string SafetyPreempted = "SafetyPreempted";
        public const string TimedOut = "TimedOut";
        public const string CleanupFailed = "CleanupFailed";
        public const string InternalError = "InternalError";
    }
}
