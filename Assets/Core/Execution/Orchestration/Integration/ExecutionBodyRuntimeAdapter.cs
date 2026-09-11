// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>Pure observation adapter; it never executes body work.</summary>
    public sealed class ExecutionBodyRuntimeAdapter
    {
        public const string AdapterVersion =
            "execution-body-runtime-adapter-3c5.1";

        public BodyRuntimeAdapterResult Observe(
            BodyExecutionRequest request,
            BodyExecutionRuntimeFact fact)
        {
            string invalid = Validate(request, fact);
            if (invalid.Length > 0)
                return Result(request, fact,
                    BodyRuntimeAdapterStatus.RejectedInvalid,
                    null, invalid);

            bool accepted = false;
            ExecutionDomainStage stage = null;
            ExecutionTerminalOutcome terminal =
                ExecutionTerminalOutcome.None;

            switch (fact.Lifecycle)
            {
                case BodyRuntimeLifecycle.RequestAccepted:
                    accepted = true;
                    break;
                case BodyRuntimeLifecycle.RequestRejected:
                    terminal = ExecutionTerminalOutcome.Rejected;
                    break;
                case BodyRuntimeLifecycle.VirtualTargetApplied:
                    stage = new ExecutionDomainStage(
                        ExecutionDomainStageIds.VrmTargetApplied);
                    break;
                case BodyRuntimeLifecycle.VirtualBodyResolved:
                    stage = new ExecutionDomainStage(
                        ExecutionDomainStageIds.VirtualBodyResolved);
                    break;
                case BodyRuntimeLifecycle.PhysicalDispatchRequested:
                case BodyRuntimeLifecycle.PhysicalTransportWritten:
                    stage = new ExecutionDomainStage(
                        ExecutionDomainStageIds.PhysicalDispatched);
                    break;
                case BodyRuntimeLifecycle.PhysicalCompletionUnverified:
                    terminal =
                        ExecutionTerminalOutcome.SucceededUnverified;
                    break;
                case BodyRuntimeLifecycle.PhysicalCompletionVerified:
                    stage = new ExecutionDomainStage(
                        ExecutionDomainStageIds
                            .PhysicalCompletionVerified);
                    break;
                case BodyRuntimeLifecycle.Failed:
                    terminal = ExecutionTerminalOutcome.Failed;
                    break;
                case BodyRuntimeLifecycle.Interrupted:
                    terminal = ExecutionTerminalOutcome.Interrupted;
                    break;
                case BodyRuntimeLifecycle.SafetyPreempted:
                    terminal = ExecutionTerminalOutcome.SafetyPreempted;
                    break;
                default:
                    return Result(request, fact,
                        BodyRuntimeAdapterStatus.RejectedInvalid,
                        null, "BODY_RUNTIME_LIFECYCLE_UNSUPPORTED");
            }

            var lifecycle = new BodyExecutionLifecycleResult(
                request.RequestId, request.PlanId, request.StepId,
                request.ExecutionAttemptId, accepted, stage, terminal,
                fact.OccurredAtUtc, fact.FailureReason);
            return Result(request, fact,
                BodyRuntimeAdapterStatus.Observed, lifecycle,
                string.Empty);
        }

        private static string Validate(
            BodyExecutionRequest request,
            BodyExecutionRuntimeFact fact)
        {
            if (request == null || fact == null)
                return "BODY_RUNTIME_INPUT_NULL";
            if (string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.PlanId) ||
                string.IsNullOrWhiteSpace(request.StepId) ||
                string.IsNullOrWhiteSpace(request.ExecutionAttemptId) ||
                string.IsNullOrWhiteSpace(request.ActionId) ||
                string.IsNullOrWhiteSpace(fact.RuntimeExecutionToken) ||
                string.IsNullOrWhiteSpace(fact.ActionId) ||
                fact.RuntimeGeneration <= 0 ||
                fact.OccurredAtUtc == default(DateTime) ||
                !Enum.IsDefined(typeof(BodyRuntimeLifecycle),
                    fact.Lifecycle) ||
                !Enum.IsDefined(typeof(BodyPhysicalVerificationLevel),
                    fact.VerificationLevel))
                return "BODY_RUNTIME_INPUT_INVALID";
            if (!string.Equals(request.ActionId, fact.ActionId,
                    StringComparison.Ordinal))
                return "BODY_ACTION_ID_MISMATCH";

            string stageFailure = ValidateStage(fact);
            if (stageFailure.Length > 0)
                return stageFailure;

            switch (fact.Lifecycle)
            {
                case BodyRuntimeLifecycle.RequestAccepted:
                    return RequireShape(fact,
                        ExecutionTerminalOutcome.None,
                        BodyPhysicalVerificationLevel.None);
                case BodyRuntimeLifecycle.RequestRejected:
                    return RequireShape(fact,
                        ExecutionTerminalOutcome.Rejected,
                        BodyPhysicalVerificationLevel.None);
                case BodyRuntimeLifecycle.VirtualTargetApplied:
                case BodyRuntimeLifecycle.VirtualBodyResolved:
                case BodyRuntimeLifecycle.PhysicalDispatchRequested:
                    return RequireShape(fact,
                        ExecutionTerminalOutcome.None,
                        BodyPhysicalVerificationLevel.None);
                case BodyRuntimeLifecycle.PhysicalTransportWritten:
                    return RequireShape(fact,
                        ExecutionTerminalOutcome.None,
                        BodyPhysicalVerificationLevel.Unverified);
                case BodyRuntimeLifecycle.PhysicalCompletionUnverified:
                    return RequireShape(fact,
                        ExecutionTerminalOutcome.SucceededUnverified,
                        BodyPhysicalVerificationLevel.Unverified);
                case BodyRuntimeLifecycle.PhysicalCompletionVerified:
                    return RequireShape(fact,
                        ExecutionTerminalOutcome.Succeeded,
                        BodyPhysicalVerificationLevel.Verified);
                case BodyRuntimeLifecycle.Failed:
                    return RequireShape(fact,
                        ExecutionTerminalOutcome.Failed,
                        BodyPhysicalVerificationLevel.None);
                case BodyRuntimeLifecycle.Interrupted:
                    return RequireShape(fact,
                        ExecutionTerminalOutcome.Interrupted,
                        BodyPhysicalVerificationLevel.None);
                case BodyRuntimeLifecycle.SafetyPreempted:
                    return RequireShape(fact,
                        ExecutionTerminalOutcome.SafetyPreempted,
                        BodyPhysicalVerificationLevel.None);
                default:
                    return "BODY_RUNTIME_LIFECYCLE_UNSUPPORTED";
            }
        }

        private static string ValidateStage(BodyExecutionRuntimeFact fact)
        {
            string expected = null;
            switch (fact.Lifecycle)
            {
                case BodyRuntimeLifecycle.RequestAccepted:
                    expected = ExecutionDomainStageIds.RequestAccepted;
                    break;
                case BodyRuntimeLifecycle.VirtualTargetApplied:
                    expected = ExecutionDomainStageIds.VrmTargetApplied;
                    break;
                case BodyRuntimeLifecycle.VirtualBodyResolved:
                    expected = ExecutionDomainStageIds.VirtualBodyResolved;
                    break;
                case BodyRuntimeLifecycle.PhysicalDispatchRequested:
                case BodyRuntimeLifecycle.PhysicalTransportWritten:
                    expected = ExecutionDomainStageIds.PhysicalDispatched;
                    break;
                case BodyRuntimeLifecycle.PhysicalCompletionVerified:
                    expected = ExecutionDomainStageIds
                        .PhysicalCompletionVerified;
                    break;
            }

            string actual = fact.DomainStage != null
                ? fact.DomainStage.StageId : string.Empty;
            if (expected == null)
                return actual.Length == 0
                    ? string.Empty
                    : "BODY_RUNTIME_STAGE_UNEXPECTED";
            return string.Equals(expected, actual,
                    StringComparison.Ordinal)
                ? string.Empty
                : "BODY_RUNTIME_STAGE_MISMATCH";
        }

        private static string RequireShape(
            BodyExecutionRuntimeFact fact,
            ExecutionTerminalOutcome outcome,
            BodyPhysicalVerificationLevel verification)
        {
            return fact.TerminalOutcome == outcome &&
                fact.VerificationLevel == verification
                    ? string.Empty
                    : "BODY_RUNTIME_FACT_SHAPE_INVALID";
        }

        private static BodyRuntimeAdapterResult Result(
            BodyExecutionRequest request,
            BodyExecutionRuntimeFact fact,
            BodyRuntimeAdapterStatus status,
            BodyExecutionLifecycleResult lifecycle,
            string failure)
        {
            return new BodyRuntimeAdapterResult(
                fact != null ? fact.RuntimeExecutionToken : string.Empty,
                fact != null ? fact.RuntimeGeneration : 0,
                fact != null ? fact.ActionId :
                    (request != null ? request.ActionId : string.Empty),
                fact != null ? fact.Lifecycle :
                    BodyRuntimeLifecycle.RequestAccepted,
                fact != null ? fact.VerificationLevel :
                    BodyPhysicalVerificationLevel.None,
                status, lifecycle,
                fact != null ? fact.Diagnostic : string.Empty,
                failure);
        }
    }
}
