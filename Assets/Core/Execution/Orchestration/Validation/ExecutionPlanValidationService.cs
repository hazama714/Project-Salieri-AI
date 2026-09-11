// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Execution.Orchestration.Contracts;

namespace SalieriAI.Core.Execution.Orchestration.Validation
{
    public sealed class ExecutionPlanValidationBundle
    {
        public ExecutionPlanValidationResult StructureResult { get; }
        public ExecutionPlanValidationResult PolicyResult { get; }
        public ValidatedExecutionPlan ValidatedPlan { get; }
        public bool IsValid =>
            StructureResult != null &&
            StructureResult.IsValid &&
            PolicyResult != null &&
            PolicyResult.IsValid &&
            ValidatedPlan != null;

        internal ExecutionPlanValidationBundle(
            ExecutionPlanValidationResult structureResult,
            ExecutionPlanValidationResult policyResult,
            ValidatedExecutionPlan validatedPlan)
        {
            StructureResult = structureResult;
            PolicyResult = policyResult;
            ValidatedPlan = validatedPlan;
        }
    }

    public static class ExecutionPlanValidationService
    {
        public const string ValidatorVersion =
            "execution-plan-validation-service-3a.1";

        public static ExecutionPlanValidationBundle Validate(
            ReactionExecutionPlan plan,
            ExecutionPlanValidationContext structureContext,
            ExecutionPlanValidationContext policyContext,
            DateTime validatedAtUtc)
        {
            ExecutionPlanValidationResult structure =
                ExecutionPlanStructureValidator.Validate(
                    plan,
                    structureContext
                );
            ExecutionPlanValidationResult policy =
                ExecutionOrchestrationPolicyV01Validator.Validate(
                    plan,
                    plan != null ? plan.PolicyVersion : null,
                    policyContext
                );

            ValidatedExecutionPlan token = null;
            if (structure.IsValid && policy.IsValid)
            {
                DateTime normalized = validatedAtUtc;
                if (validatedAtUtc.Kind == DateTimeKind.Local)
                    normalized = validatedAtUtc.ToUniversalTime();
                else if (
                    validatedAtUtc != default(DateTime) &&
                    validatedAtUtc.Kind ==
                        DateTimeKind.Unspecified)
                {
                    normalized = DateTime.SpecifyKind(
                        validatedAtUtc,
                        DateTimeKind.Utc
                    );
                }

                token = new ValidatedExecutionPlan(
                    plan,
                    normalized,
                    ValidatorVersion,
                    plan.PolicyVersion,
                    structure.ValidationId,
                    policy.ValidationId
                );
            }

            return new ExecutionPlanValidationBundle(
                structure,
                policy,
                token
            );
        }
    }
}
