// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    /// <summary>
    /// Explicit plan-composition semantics supplied independently from an
    /// action descriptor. It owns neither action semantics nor lifecycle,
    /// provenance, validation, permission, resource, or runtime behavior.
    /// </summary>
    public sealed class ExecutionPlanCompositionPolicy
    {
        public ExecutionRequiredness Requiredness { get; }
        public ExecutionPlanCompletionPolicy PlanCompletionPolicy { get; }
        public ExecutionFailurePolicy PlanFailurePolicy { get; }
        public ExecutionCancellationPolicy PlanCancellationPolicy { get; }

        public ExecutionPlanCompositionPolicy(
            ExecutionRequiredness requiredness,
            ExecutionPlanCompletionPolicy planCompletionPolicy,
            ExecutionFailurePolicy planFailurePolicy,
            ExecutionCancellationPolicy planCancellationPolicy)
        {
            Requiredness = requiredness;
            PlanCompletionPolicy = planCompletionPolicy;
            PlanFailurePolicy = planFailurePolicy;
            PlanCancellationPolicy = planCancellationPolicy;
        }
    }
}
