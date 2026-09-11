// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using SalieriAI.Core.Execution.Orchestration.Contracts;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>
    /// Explicit orchestration metadata supplied by the Production caller.
    /// ExecutionRequest does not contain domain, resource, policy, or
    /// correlation identities, so this contract prevents the builder from
    /// guessing them.
    /// </summary>
    public sealed class ExecutionRequestPlanConstructionSpec
    {
        public string PlanId { get; }
        public string StepId { get; }
        public ExecutionSubmissionSourceKind SourceKind { get; }
        public string SourceEventId { get; }
        public string InteractionId { get; }
        public string InputId { get; }
        public string AnalysisId { get; }
        public string ReactionId { get; }
        public int PlanGeneration { get; }
        public DateTime CreatedAtUtc { get; }
        public ExecutionOrchestrationPolicyVersion PolicyVersion { get; }
        public ExecutionPlanType PlanType { get; }
        public ExecutionPolicyKind PolicyKind { get; }
        public ExecutionStepType StepType { get; }
        public ExecutionStepRole StepRole { get; }
        public ExecutionDomain Domain { get; }
        public ExecutionRequiredness Requiredness { get; }
        public IReadOnlyList<ExecutionResourceRequirement>
            ResourceRequirements { get; }
        public ExecutionCompletionPolicy CompletionPolicy { get; }
        public ExecutionPermissionWaitPolicy PermissionWaitPolicy { get; }
        public ExecutionTimeoutPolicy TimeoutPolicy { get; }
        public ExecutionResourceWaitTimeoutPolicy ResourceWaitTimeoutPolicy
        {
            get;
        }
        public ExecutionRetryPolicy RetryPolicy { get; }
        public ExecutionFailurePolicy StepFailurePolicy { get; }
        public ExecutionCancellationPolicy StepCancellationPolicy { get; }
        public ExecutionPlanCompletionPolicy PlanCompletionPolicy { get; }
        public ExecutionFailurePolicy PlanFailurePolicy { get; }
        public ExecutionCancellationPolicy PlanCancellationPolicy { get; }
        public ExecutionSafetyPolicy SafetyPolicy { get; }
        public string PayloadSchemaVersion { get; }

        public ExecutionRequestPlanConstructionSpec(
            string planId,
            string stepId,
            string interactionId,
            string inputId,
            string analysisId,
            string reactionId,
            int planGeneration,
            DateTime createdAtUtc,
            ExecutionOrchestrationPolicyVersion policyVersion,
            ExecutionPlanType planType,
            ExecutionPolicyKind policyKind,
            ExecutionStepType stepType,
            ExecutionStepRole stepRole,
            ExecutionDomain domain,
            ExecutionRequiredness requiredness,
            IEnumerable<ExecutionResourceRequirement>
                resourceRequirements,
            ExecutionCompletionPolicy completionPolicy,
            ExecutionPermissionWaitPolicy permissionWaitPolicy,
            ExecutionTimeoutPolicy timeoutPolicy,
            ExecutionResourceWaitTimeoutPolicy resourceWaitTimeoutPolicy,
            ExecutionRetryPolicy retryPolicy,
            ExecutionFailurePolicy stepFailurePolicy,
            ExecutionCancellationPolicy stepCancellationPolicy,
            ExecutionPlanCompletionPolicy planCompletionPolicy,
            ExecutionFailurePolicy planFailurePolicy,
            ExecutionCancellationPolicy planCancellationPolicy,
            ExecutionSafetyPolicy safetyPolicy,
            string payloadSchemaVersion)
            : this(
                planId,
                stepId,
                ExecutionSubmissionSourceKind.Unknown,
                string.Empty,
                interactionId,
                inputId,
                analysisId,
                reactionId,
                planGeneration,
                createdAtUtc,
                policyVersion,
                planType,
                policyKind,
                stepType,
                stepRole,
                domain,
                requiredness,
                resourceRequirements,
                completionPolicy,
                permissionWaitPolicy,
                timeoutPolicy,
                resourceWaitTimeoutPolicy,
                retryPolicy,
                stepFailurePolicy,
                stepCancellationPolicy,
                planCompletionPolicy,
                planFailurePolicy,
                planCancellationPolicy,
                safetyPolicy,
                payloadSchemaVersion)
        {
        }

        public ExecutionRequestPlanConstructionSpec(
            string planId,
            string stepId,
            ExecutionSubmissionSourceKind sourceKind,
            string sourceEventId,
            string interactionId,
            string inputId,
            string analysisId,
            string reactionId,
            int planGeneration,
            DateTime createdAtUtc,
            ExecutionOrchestrationPolicyVersion policyVersion,
            ExecutionPlanType planType,
            ExecutionPolicyKind policyKind,
            ExecutionStepType stepType,
            ExecutionStepRole stepRole,
            ExecutionDomain domain,
            ExecutionRequiredness requiredness,
            IEnumerable<ExecutionResourceRequirement>
                resourceRequirements,
            ExecutionCompletionPolicy completionPolicy,
            ExecutionPermissionWaitPolicy permissionWaitPolicy,
            ExecutionTimeoutPolicy timeoutPolicy,
            ExecutionResourceWaitTimeoutPolicy resourceWaitTimeoutPolicy,
            ExecutionRetryPolicy retryPolicy,
            ExecutionFailurePolicy stepFailurePolicy,
            ExecutionCancellationPolicy stepCancellationPolicy,
            ExecutionPlanCompletionPolicy planCompletionPolicy,
            ExecutionFailurePolicy planFailurePolicy,
            ExecutionCancellationPolicy planCancellationPolicy,
            ExecutionSafetyPolicy safetyPolicy,
            string payloadSchemaVersion)
        {
            PlanId = planId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            SourceKind = sourceKind;
            SourceEventId = sourceEventId ?? string.Empty;
            InteractionId = interactionId ?? string.Empty;
            InputId = inputId ?? string.Empty;
            AnalysisId = analysisId ?? string.Empty;
            ReactionId = reactionId ?? string.Empty;
            PlanGeneration = planGeneration;
            CreatedAtUtc = NormalizeUtc(createdAtUtc);
            PolicyVersion = policyVersion;
            PlanType = planType;
            PolicyKind = policyKind;
            StepType = stepType;
            StepRole = stepRole;
            Domain = domain;
            Requiredness = requiredness;
            ResourceRequirements = new ReadOnlyCollection<
                ExecutionResourceRequirement>(
                    resourceRequirements != null
                        ? new List<ExecutionResourceRequirement>(
                            resourceRequirements)
                        : new List<ExecutionResourceRequirement>());
            CompletionPolicy = completionPolicy;
            PermissionWaitPolicy = permissionWaitPolicy;
            TimeoutPolicy = timeoutPolicy;
            ResourceWaitTimeoutPolicy = resourceWaitTimeoutPolicy;
            RetryPolicy = retryPolicy;
            StepFailurePolicy = stepFailurePolicy;
            StepCancellationPolicy = stepCancellationPolicy;
            PlanCompletionPolicy = planCompletionPolicy;
            PlanFailurePolicy = planFailurePolicy;
            PlanCancellationPolicy = planCancellationPolicy;
            SafetyPolicy = safetyPolicy;
            PayloadSchemaVersion = payloadSchemaVersion ?? string.Empty;
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default(DateTime) ||
                value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Pure, deterministic conversion from the existing Production
    /// ExecutionRequest plus explicit orchestration metadata. This class
    /// performs no validation and calls no runtime service.
    /// </summary>
    public static class ExecutionRequestReactionPlanBuilder
    {
        public const string BuilderVersion =
            "execution-request-reaction-plan-builder-3d4b.1";

        public static ReactionExecutionPlan Build(
            global::ExecutionRequest request,
            ExecutionRequestPlanConstructionSpec spec)
        {
            global::ExecutionRequest source =
                request ?? new global::ExecutionRequest();
            ExecutionRequestPlanConstructionSpec value =
                spec ?? EmptySpec();

            var step = new ReactionExecutionStep(
                value.StepId,
                value.PlanId,
                value.StepType,
                value.StepRole,
                value.Domain,
                source.RequestId,
                value.Requiredness,
                source.Priority,
                value.ResourceRequirements,
                value.CompletionPolicy,
                value.PermissionWaitPolicy,
                value.TimeoutPolicy,
                value.RetryPolicy,
                value.StepFailurePolicy,
                value.StepCancellationPolicy,
                new ExecutionRequestReference(
                    source.ActionId,
                    source.RequestId,
                    value.PayloadSchemaVersion),
                value.ResourceWaitTimeoutPolicy);

            return new ReactionExecutionPlan(
                value.PlanId,
                value.PolicyVersion,
                value.SourceKind,
                value.SourceEventId,
                value.InteractionId,
                value.InputId,
                value.AnalysisId,
                value.ReactionId,
                value.PlanGeneration,
                source.Priority,
                value.PlanType,
                value.PolicyKind,
                value.CreatedAtUtc,
                new[] { step },
                new ExecutionDependency[0],
                value.PlanCompletionPolicy,
                value.PlanFailurePolicy,
                value.PlanCancellationPolicy,
                value.SafetyPolicy);
        }

        private static ExecutionRequestPlanConstructionSpec EmptySpec()
        {
            return new ExecutionRequestPlanConstructionSpec(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                default(DateTime),
                null,
                ExecutionPlanType.None,
                ExecutionPolicyKind.DependencyGraph,
                ExecutionStepType.None,
                ExecutionStepRole.Other,
                ExecutionDomain.None,
                ExecutionRequiredness.Required,
                new ExecutionResourceRequirement[0],
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                string.Empty);
        }
    }

    public sealed class ExecutionRequestProductionHandoffResult
    {
        public ReactionExecutionPlan Plan { get; }
        public ExecutionProductionAdmissionResult Admission { get; }
        public bool IsStarted => Admission != null && Admission.IsStarted;

        internal ExecutionRequestProductionHandoffResult(
            ReactionExecutionPlan plan,
            ExecutionProductionAdmissionResult admission)
        {
            Plan = plan;
            Admission = admission;
        }
    }

    /// <summary>
    /// Production handoff that cannot bypass the existing admission service.
    /// Plan validation remains exclusively inside that service.
    /// </summary>
    public sealed class ExecutionRequestProductionHandoff
    {
        private readonly ExecutionProductionAdmissionService admission;

        public ExecutionRequestProductionHandoff(
            ExecutionProductionAdmissionService admission)
        {
            this.admission = admission ??
                throw new ArgumentNullException("admission");
        }

        public ExecutionRequestProductionHandoffResult BuildAndAdmit(
            global::ExecutionRequest request,
            ExecutionRequestPlanConstructionSpec spec,
            string structureValidationId,
            string policyValidationId,
            DateTime evaluatedAtUtc)
        {
            ReactionExecutionPlan plan =
                ExecutionRequestReactionPlanBuilder.Build(request, spec);
            ExecutionProductionAdmissionResult result =
                admission.AdmitAndStart(
                    plan,
                    structureValidationId,
                    policyValidationId,
                    evaluatedAtUtc);
            return new ExecutionRequestProductionHandoffResult(
                plan, result);
        }
    }
}
