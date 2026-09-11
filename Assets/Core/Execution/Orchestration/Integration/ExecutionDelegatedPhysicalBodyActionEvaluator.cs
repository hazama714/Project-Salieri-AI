// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    public enum ExecutionDelegatedBodyObservationStatus
    {
        Accepted = 0,
        InvalidContract = 1,
        InvalidObservation = 2,
        UnverifiedCompletionNotAllowed = 3,
        VerifiedCompletionFeedbackUnsupported = 4,
        UnsupportedLifecycle = 5
    }

    public sealed class ExecutionDelegatedBodyObservationResult
    {
        public ExecutionDelegatedBodyObservationStatus Status { get; }
        public BodyRuntimeLifecycle Lifecycle { get; }
        public bool RequestAccepted { get; }
        public ExecutionDomainStage DomainStage { get; }
        public ExecutionTerminalOutcome TerminalOutcome { get; }
        public BodyPhysicalVerificationLevel VerificationLevel { get; }
        public string FailureReason { get; }
        public bool IsAccepted =>
            Status == ExecutionDelegatedBodyObservationStatus.Accepted;

        internal ExecutionDelegatedBodyObservationResult(
            ExecutionDelegatedBodyObservationStatus status,
            BodyRuntimeLifecycle lifecycle,
            bool requestAccepted,
            ExecutionDomainStage domainStage,
            ExecutionTerminalOutcome terminalOutcome,
            BodyPhysicalVerificationLevel verificationLevel,
            string failureReason)
        {
            Status = status;
            Lifecycle = lifecycle;
            RequestAccepted = requestAccepted;
            DomainStage = domainStage;
            TerminalOutcome = terminalOutcome;
            VerificationLevel = verificationLevel;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    /// <summary>
    /// Pure gate over the existing body runtime adapter result. It never
    /// creates physical evidence, dispatches body work, or upgrades an
    /// unverified observation to verified completion.
    /// </summary>
    public static class ExecutionDelegatedPhysicalBodyActionEvaluator
    {
        public const string EvaluatorVersion =
            "execution-delegated-physical-body-action-v0.1";

        public static ExecutionDelegatedBodyObservationResult Evaluate(
            ExecutionDelegatedPhysicalBodyActionContract contract,
            BodyRuntimeAdapterResult observation)
        {
            string contractFailure = ValidateContract(contract);
            if (contractFailure.Length > 0)
            {
                return Result(
                    ExecutionDelegatedBodyObservationStatus.InvalidContract,
                    observation,
                    contractFailure);
            }

            if (observation == null ||
                observation.Status != BodyRuntimeAdapterStatus.Observed ||
                observation.LifecycleResult == null)
            {
                return Result(
                    ExecutionDelegatedBodyObservationStatus
                        .InvalidObservation,
                    observation,
                    "DELEGATED_BODY_OBSERVATION_INVALID");
            }

            switch (observation.Lifecycle)
            {
                case BodyRuntimeLifecycle.RequestAccepted:
                case BodyRuntimeLifecycle.PhysicalDispatchRequested:
                case BodyRuntimeLifecycle.PhysicalTransportWritten:
                    return Result(
                        ExecutionDelegatedBodyObservationStatus.Accepted,
                        observation,
                        string.Empty);

                case BodyRuntimeLifecycle.PhysicalCompletionUnverified:
                    return contract.AllowsUnverifiedCompletion
                        ? Result(
                            ExecutionDelegatedBodyObservationStatus.Accepted,
                            observation,
                            string.Empty)
                        : Result(
                            ExecutionDelegatedBodyObservationStatus
                                .UnverifiedCompletionNotAllowed,
                            observation,
                            "DELEGATED_BODY_UNVERIFIED_COMPLETION_NOT_ALLOWED");

                case BodyRuntimeLifecycle.PhysicalCompletionVerified:
                    return contract.SupportsPhysicalCompletionFeedback
                        ? Result(
                            ExecutionDelegatedBodyObservationStatus.Accepted,
                            observation,
                            string.Empty)
                        : Result(
                            ExecutionDelegatedBodyObservationStatus
                                .VerifiedCompletionFeedbackUnsupported,
                            observation,
                            "DELEGATED_BODY_VERIFIED_FEEDBACK_UNSUPPORTED");

                default:
                    return Result(
                        ExecutionDelegatedBodyObservationStatus
                            .UnsupportedLifecycle,
                        observation,
                        "DELEGATED_BODY_COMPLETION_LIFECYCLE_UNSUPPORTED");
            }
        }

        public static string ValidateContract(
            ExecutionDelegatedPhysicalBodyActionContract contract)
        {
            if (contract == null)
                return "DELEGATED_BODY_CONTRACT_NULL";
            if (contract.CancellationCapability == null)
                return "DELEGATED_BODY_CANCELLATION_CAPABILITY_NULL";
            if (contract.CancelEffectConfirmed && !contract.SupportsCancel)
                return "DELEGATED_BODY_CANCEL_EFFECT_WITHOUT_SUPPORT";
            if (contract.PhysicalStopVerified &&
                !contract.SupportsCancel &&
                !contract.SupportsInterrupt)
            {
                return "DELEGATED_BODY_PHYSICAL_STOP_WITHOUT_CONTROL_SUPPORT";
            }
            return string.Empty;
        }

        private static ExecutionDelegatedBodyObservationResult Result(
            ExecutionDelegatedBodyObservationStatus status,
            BodyRuntimeAdapterResult observation,
            string failureReason)
        {
            BodyExecutionLifecycleResult lifecycle = observation != null
                ? observation.LifecycleResult
                : null;
            return new ExecutionDelegatedBodyObservationResult(
                status,
                observation != null
                    ? observation.Lifecycle
                    : BodyRuntimeLifecycle.RequestAccepted,
                lifecycle != null && lifecycle.RequestAccepted,
                lifecycle != null ? lifecycle.DomainStage : null,
                lifecycle != null
                    ? lifecycle.TerminalOutcome
                    : ExecutionTerminalOutcome.None,
                observation != null
                    ? observation.VerificationLevel
                    : BodyPhysicalVerificationLevel.None,
                failureReason);
        }
    }
}
