// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    /// <summary>
    /// Immutable execution intent. It contains no mutable runtime state and
    /// does not imply capability, grounding, safety, authorization, resource,
    /// permission, executor availability, or execution success.
    /// </summary>
    public sealed class ReactionExecutionPlan
    {
        public string PlanId { get; }
        public ExecutionOrchestrationPolicyVersion PolicyVersion { get; }
        public ExecutionSubmissionSourceKind SourceKind { get; }
        public string SourceEventId { get; }
        public string InteractionId { get; }
        public string InputId { get; }
        public string AnalysisId { get; }
        public string ReactionId { get; }
        public int PlanGeneration { get; }
        public int Priority { get; }
        public ExecutionPlanType PlanType { get; }
        public ExecutionPolicyKind PolicyKind { get; }
        public DateTime CreatedAtUtc { get; }
        public IReadOnlyList<ReactionExecutionStep> Steps { get; }
        public IReadOnlyList<ExecutionDependency> Dependencies { get; }
        public ExecutionPlanCompletionPolicy PlanCompletionPolicy
        {
            get;
        }
        public ExecutionFailurePolicy FailurePolicy { get; }
        public ExecutionCancellationPolicy CancellationPolicy { get; }
        public ExecutionSafetyPolicy SafetyPolicy { get; }

        public ReactionExecutionPlan(
            string planId,
            ExecutionOrchestrationPolicyVersion policyVersion,
            string interactionId,
            string inputId,
            string analysisId,
            string reactionId,
            int planGeneration,
            int priority,
            ExecutionPlanType planType,
            ExecutionPolicyKind policyKind,
            DateTime createdAtUtc,
            IEnumerable<ReactionExecutionStep> steps,
            IEnumerable<ExecutionDependency> dependencies,
            ExecutionPlanCompletionPolicy planCompletionPolicy,
            ExecutionFailurePolicy failurePolicy,
            ExecutionCancellationPolicy cancellationPolicy,
            ExecutionSafetyPolicy safetyPolicy)
            : this(
                planId,
                policyVersion,
                ExecutionSubmissionSourceKind.Unknown,
                string.Empty,
                interactionId,
                inputId,
                analysisId,
                reactionId,
                planGeneration,
                priority,
                planType,
                policyKind,
                createdAtUtc,
                steps,
                dependencies,
                planCompletionPolicy,
                failurePolicy,
                cancellationPolicy,
                safetyPolicy)
        {
        }

        public ReactionExecutionPlan(
            string planId,
            ExecutionOrchestrationPolicyVersion policyVersion,
            ExecutionSubmissionSourceKind sourceKind,
            string sourceEventId,
            string interactionId,
            string inputId,
            string analysisId,
            string reactionId,
            int planGeneration,
            int priority,
            ExecutionPlanType planType,
            ExecutionPolicyKind policyKind,
            DateTime createdAtUtc,
            IEnumerable<ReactionExecutionStep> steps,
            IEnumerable<ExecutionDependency> dependencies,
            ExecutionPlanCompletionPolicy planCompletionPolicy,
            ExecutionFailurePolicy failurePolicy,
            ExecutionCancellationPolicy cancellationPolicy,
            ExecutionSafetyPolicy safetyPolicy)
        {
            PlanId = ExecutionContractUtility.Text(planId);
            PolicyVersion = policyVersion;
            SourceKind = sourceKind;
            SourceEventId = ExecutionContractUtility.Text(sourceEventId);
            InteractionId =
                ExecutionContractUtility.Text(interactionId);
            InputId = ExecutionContractUtility.Text(inputId);
            AnalysisId = ExecutionContractUtility.Text(analysisId);
            ReactionId = ExecutionContractUtility.Text(reactionId);
            PlanGeneration = planGeneration;
            Priority = priority;
            PlanType = planType;
            PolicyKind = policyKind;
            CreatedAtUtc = ExecutionContractUtility.Utc(createdAtUtc);
            Steps = ExecutionContractUtility.ReadOnlyCopy(steps);
            Dependencies =
                ExecutionContractUtility.ReadOnlyCopy(dependencies);
            PlanCompletionPolicy = planCompletionPolicy;
            FailurePolicy = failurePolicy;
            CancellationPolicy = cancellationPolicy;
            SafetyPolicy = safetyPolicy;
        }
    }
}
