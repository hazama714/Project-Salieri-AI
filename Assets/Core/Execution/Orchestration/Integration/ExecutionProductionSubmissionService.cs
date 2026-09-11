// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Validation;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    public interface IExecutionProductionSubmissionIdSource
    {
        string CreatePlanId();
        string CreateStepId();
    }

    /// <summary>
    /// Production-capable outer-boundary ID source. Tests may inject a
    /// deterministic implementation. Guid generation never enters the
    /// builder, admission, host, or reducer.
    /// </summary>
    public sealed class GuidExecutionProductionSubmissionIdSource :
        IExecutionProductionSubmissionIdSource
    {
        public string CreatePlanId()
        {
            return "execution-plan-" + Guid.NewGuid().ToString("N");
        }

        public string CreateStepId()
        {
            return "execution-step-" + Guid.NewGuid().ToString("N");
        }
    }

    public enum ExecutionProductionSubmissionStatus
    {
        Started = 0,
        InvalidRequest = 1,
        NotRegistered = 2,
        MissingContext = 3,
        InvalidCompositionPolicy = 4,
        InvalidCreatedAtUtc = 5,
        LifecycleAllocationFailed = 6,
        HandoffRejected = 7,
        SubmissionFailed = 8
    }

    public sealed class ExecutionSubmissionResult
    {
        public ExecutionProductionSubmissionStatus Status { get; }
        public ExecutionActionDescriptorLookupResult DescriptorLookup
        {
            get;
        }
        public ExecutionPlanCompositionPolicyValidationResult
            CompositionValidation { get; }
        public ExecutionRequestPlanConstructionSpec ConstructionSpec
        {
            get;
        }
        public ExecutionRequestProductionHandoffResult Handoff { get; }
        public string Diagnostic { get; }
        public bool IsStarted =>
            Status == ExecutionProductionSubmissionStatus.Started &&
            Handoff != null && Handoff.IsStarted;

        internal ExecutionSubmissionResult(
            ExecutionProductionSubmissionStatus status,
            ExecutionActionDescriptorLookupResult descriptorLookup,
            ExecutionPlanCompositionPolicyValidationResult
                compositionValidation,
            ExecutionRequestPlanConstructionSpec constructionSpec,
            ExecutionRequestProductionHandoffResult handoff,
            string diagnostic)
        {
            Status = status;
            DescriptorLookup = descriptorLookup;
            CompositionValidation = compositionValidation;
            ConstructionSpec = constructionSpec;
            Handoff = handoff;
            Diagnostic = diagnostic ?? string.Empty;
        }
    }

    /// <summary>
    /// Production Submission boundary for an already-created
    /// ExecutionRequest. It validates explicit prerequisites before issuing
    /// lifecycle identity, builds an explicit construction spec, and then
    /// delegates exclusively to the existing handoff/admission/runtime path.
    /// </summary>
    public sealed class ExecutionProductionSubmissionService
    {
        public const string ServiceVersion =
            "execution-production-submission-3d4b.1";

        private readonly ExecutionActionDescriptorRegistry registry;
        private readonly ExecutionRequestProductionHandoff handoff;
        private readonly IExecutionProductionSubmissionIdSource idSource;
        private readonly HashSet<string> issuedPlanIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> issuedStepIds =
            new HashSet<string>(StringComparer.Ordinal);

        public int SubmissionAttemptCount { get; private set; }
        public int HandoffInvocationCount { get; private set; }
        public int StartedSubmissionCount { get; private set; }
        public int LastIssuedGeneration { get; private set; }
        public ExecutionSubmissionResult LastResult { get; private set; }

        public ExecutionProductionSubmissionService(
            ExecutionActionDescriptorRegistry registry,
            ExecutionRequestProductionHandoff handoff,
            IExecutionProductionSubmissionIdSource idSource)
        {
            this.registry = registry ??
                throw new ArgumentNullException("registry");
            this.handoff = handoff ??
                throw new ArgumentNullException("handoff");
            this.idSource = idSource ??
                throw new ArgumentNullException("idSource");
        }

        public ExecutionSubmissionResult Submit(
            global::ExecutionRequest request,
            ExecutionRequestProvenance provenance,
            ExecutionPlanCompositionPolicy compositionPolicy,
            DateTime createdAtUtc)
        {
            SubmissionAttemptCount++;
            ExecutionActionDescriptorLookupResult descriptorLookup = null;
            ExecutionPlanCompositionPolicyValidationResult
                compositionValidation = null;

            if (request == null ||
                string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.ActionId))
            {
                return Store(Rejected(
                    ExecutionProductionSubmissionStatus.InvalidRequest,
                    null,
                    null,
                    "EXECUTION_REQUEST_INVALID"));
            }

            descriptorLookup = registry.Lookup(request.ActionId);
            if (descriptorLookup == null || !descriptorLookup.IsFound)
            {
                return Store(Rejected(
                    ExecutionProductionSubmissionStatus.NotRegistered,
                    descriptorLookup,
                    null,
                    "EXECUTION_ACTION_NOT_REGISTERED"));
            }

            ExecutionRequestProvenanceValidationResult provenanceValidation =
                ExecutionRequestProvenanceValidator.Validate(provenance);
            if (!provenanceValidation.IsValid)
            {
                return Store(Rejected(
                    ExecutionProductionSubmissionStatus.MissingContext,
                    descriptorLookup,
                    null,
                    provenanceValidation.FailureCode));
            }

            compositionValidation =
                ExecutionPlanCompositionPolicyValidator.Validate(
                    compositionPolicy);
            if (compositionValidation == null ||
                !compositionValidation.IsValid)
            {
                return Store(Rejected(
                    ExecutionProductionSubmissionStatus
                        .InvalidCompositionPolicy,
                    descriptorLookup,
                    compositionValidation,
                    "EXECUTION_PLAN_COMPOSITION_POLICY_INVALID"));
            }

            if (createdAtUtc == default(DateTime) ||
                createdAtUtc.Kind != DateTimeKind.Utc)
            {
                return Store(Rejected(
                    ExecutionProductionSubmissionStatus.InvalidCreatedAtUtc,
                    descriptorLookup,
                    compositionValidation,
                    "CREATED_AT_UTC_MUST_BE_EXPLICIT_UTC"));
            }

            int generation;
            string planId;
            string stepId;
            try
            {
                generation = checked(LastIssuedGeneration + 1);
                planId = idSource.CreatePlanId() ?? string.Empty;
                stepId = idSource.CreateStepId() ?? string.Empty;
            }
            catch (Exception exception)
            {
                return Store(Rejected(
                    ExecutionProductionSubmissionStatus
                        .LifecycleAllocationFailed,
                    descriptorLookup,
                    compositionValidation,
                    exception.GetType().Name + ": " + exception.Message));
            }

            if (generation <= 0 || string.IsNullOrWhiteSpace(planId) ||
                string.IsNullOrWhiteSpace(stepId) ||
                issuedPlanIds.Contains(planId) ||
                issuedStepIds.Contains(stepId))
            {
                return Store(Rejected(
                    ExecutionProductionSubmissionStatus
                        .LifecycleAllocationFailed,
                    descriptorLookup,
                    compositionValidation,
                    "LIFECYCLE_ID_OR_GENERATION_INVALID"));
            }

            issuedPlanIds.Add(planId);
            issuedStepIds.Add(stepId);
            LastIssuedGeneration = generation;

            ExecutionActionDescriptor descriptor =
                descriptorLookup.Descriptor;
            var spec = new ExecutionRequestPlanConstructionSpec(
                planId,
                stepId,
                provenance.SourceKind,
                provenance.SourceEventId,
                provenance.InteractionId,
                provenance.InputId,
                provenance.AnalysisId,
                provenance.ReactionId,
                generation,
                createdAtUtc,
                descriptor.PolicyVersion,
                descriptor.PlanType,
                descriptor.PolicyKind,
                descriptor.StepType,
                descriptor.StepRole,
                descriptor.Domain,
                compositionPolicy.Requiredness,
                descriptor.ResourceRequirements,
                descriptor.CompletionPolicy,
                descriptor.PermissionWaitPolicy,
                descriptor.TimeoutPolicy,
                descriptor.ResourceWaitTimeoutPolicy,
                descriptor.RetryPolicy,
                descriptor.FailurePolicy,
                descriptor.CancellationPolicy,
                compositionPolicy.PlanCompletionPolicy,
                compositionPolicy.PlanFailurePolicy,
                compositionPolicy.PlanCancellationPolicy,
                descriptor.SafetyPolicy,
                descriptor.PayloadSchemaVersion);

            try
            {
                HandoffInvocationCount++;
                ExecutionRequestProductionHandoffResult handoffResult =
                    handoff.BuildAndAdmit(
                        request,
                        spec,
                        "submission-structure-" + planId,
                        "submission-policy-" + planId,
                        createdAtUtc);
                if (handoffResult == null || !handoffResult.IsStarted)
                {
                    return Store(new ExecutionSubmissionResult(
                        ExecutionProductionSubmissionStatus.HandoffRejected,
                        descriptorLookup,
                        compositionValidation,
                        spec,
                        handoffResult,
                        "EXECUTION_SUBMISSION_HANDOFF_REJECTED"));
                }

                StartedSubmissionCount++;
                return Store(new ExecutionSubmissionResult(
                    ExecutionProductionSubmissionStatus.Started,
                    descriptorLookup,
                    compositionValidation,
                    spec,
                    handoffResult,
                    "EXECUTION_SUBMISSION_STARTED"));
            }
            catch (Exception exception)
            {
                return Store(new ExecutionSubmissionResult(
                    ExecutionProductionSubmissionStatus.SubmissionFailed,
                    descriptorLookup,
                    compositionValidation,
                    spec,
                    null,
                    exception.GetType().Name + ": " + exception.Message));
            }
        }

        private ExecutionSubmissionResult Store(
            ExecutionSubmissionResult result)
        {
            LastResult = result;
            return result;
        }

        private static ExecutionSubmissionResult Rejected(
            ExecutionProductionSubmissionStatus status,
            ExecutionActionDescriptorLookupResult descriptorLookup,
            ExecutionPlanCompositionPolicyValidationResult
                compositionValidation,
            string diagnostic)
        {
            return new ExecutionSubmissionResult(
                status,
                descriptorLookup,
                compositionValidation,
                null,
                null,
                diagnostic);
        }

    }
}
