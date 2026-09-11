// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Execution.Orchestration.Contracts;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    /// <summary>
    /// Explicit Production registrations. There is intentionally no
    /// inferred ActionId mapping and no shared composition-policy default.
    /// </summary>
    public static class ExecutionProductionActionDescriptorRegistry
    {
        public const string NoOpActionId = "none";
        public const string LookAroundActionId =
            ExecutionLookAroundContract.ActionId;

        public static ExecutionActionDescriptorRegistry Create()
        {
            var registry = new ExecutionActionDescriptorRegistry();
            ExecutionActionDescriptorRegistrationResult result =
                registry.Register(CreateNoOpDescriptor());
            if (result == null || !result.IsRegistered)
            {
                throw new InvalidOperationException(
                    "Production NoOp descriptor registration failed: " +
                    (result != null ? result.FailureReason : "null result"));
            }
            result = registry.Register(CreateLookAroundDescriptor());
            if (result == null || !result.IsRegistered)
            {
                throw new InvalidOperationException(
                    "Production lookAround descriptor registration failed: " +
                    (result != null ? result.FailureReason : "null result"));
            }
            return registry;
        }

        public static ExecutionPlanCompositionPolicy
            CreateNoOpCompositionPolicy()
        {
            return ExecutionNoOpContract.CreatePlanCompositionPolicy();
        }

        public static ExecutionPlanCompositionPolicy
            CreateLookAroundCompositionPolicy()
        {
            return ExecutionLookAroundContract
                .CreatePlanCompositionPolicy();
        }

        private static ExecutionActionDescriptor CreateNoOpDescriptor()
        {
            return new ExecutionActionDescriptor(
                NoOpActionId,
                ExecutionNoOpContract.Domain,
                ExecutionNoOpContract.StepType,
                ExecutionNoOpContract.StepRole,
                new ExecutionResourceRequirement[0],
                ExecutionNoOpContract.CreateCompletionPolicy(),
                ExecutionNoOpContract.CreatePermissionWaitPolicy(),
                ExecutionNoOpContract.CreateTimeoutPolicy(),
                ExecutionNoOpContract.CreateResourceWaitTimeoutPolicy(),
                ExecutionNoOpContract.CreateRetryPolicy(),
                ExecutionNoOpContract.CreateFailurePolicy(),
                ExecutionNoOpContract.CreateCancellationPolicy(),
                ExecutionNoOpContract.CreateSafetyPolicy(),
                new ExecutionOrchestrationPolicyVersion(
                    ExecutionOrchestrationPolicyVersion.PolicyV01Id,
                    ExecutionOrchestrationPolicyVersion.PolicyV01Major,
                    ExecutionOrchestrationPolicyVersion.PolicyV01Minor),
                ExecutionNoOpContract.PlanType,
                ExecutionNoOpContract.PolicyKind,
                ExecutionNoOpContract.PayloadSchemaVersion);
        }

        private static ExecutionActionDescriptor
            CreateLookAroundDescriptor()
        {
            return new ExecutionActionDescriptor(
                LookAroundActionId,
                ExecutionLookAroundContract.Domain,
                ExecutionLookAroundContract.StepType,
                ExecutionLookAroundContract.StepRole,
                ExecutionLookAroundContract.CreateResourceRequirements(),
                ExecutionLookAroundContract.CreateCompletionPolicy(),
                ExecutionLookAroundContract.CreatePermissionWaitPolicy(),
                ExecutionLookAroundContract.CreateTimeoutPolicy(),
                ExecutionLookAroundContract
                    .CreateResourceWaitTimeoutPolicy(),
                ExecutionLookAroundContract.CreateRetryPolicy(),
                ExecutionLookAroundContract.CreateFailurePolicy(),
                ExecutionLookAroundContract
                    .CreateStepCancellationPolicy(),
                ExecutionLookAroundContract.CreateSafetyPolicy(),
                new ExecutionOrchestrationPolicyVersion(
                    ExecutionOrchestrationPolicyVersion.PolicyV01Id,
                    ExecutionOrchestrationPolicyVersion.PolicyV01Major,
                    ExecutionOrchestrationPolicyVersion.PolicyV01Minor),
                ExecutionLookAroundContract.PlanType,
                ExecutionLookAroundContract.PolicyKind,
                ExecutionLookAroundContract.PayloadSchemaVersion);
        }
    }
}
