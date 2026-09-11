// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using SalieriAI.Core.Execution.Orchestration.Contracts;
using SalieriAI.Core.Execution.Orchestration.Reduction;
using SalieriAI.Core.Execution.Orchestration.Runtime;

namespace SalieriAI.Core.Execution.Orchestration.Adapters
{
    public static class ExecutionAdapterContractValidator
    {
        public const string ValidatorVersion =
            "execution-runtime-adapter-contract-3c1.1";

        public static string ValidateIntent(
            ExecutionCoordinatorEffectIntent intent,
            ExecutionCoordinatorEffectIntentType expectedType)
        {
            if (intent == null)
                return ExecutionAdapterFailureCodes.NullSource;
            if (intent.IntentType != expectedType)
                return ExecutionAdapterFailureCodes.WrongDomain;
            if (Empty(intent.IntentId) || Empty(intent.PlanId) ||
                Empty(intent.StepId))
                return ExecutionAdapterFailureCodes.MissingId;
            return string.Empty;
        }

        public static string ValidateCorrelation(
            string requestPlanId, string requestStepId,
            string requestAttemptId, string requestId,
            string resultPlanId, string resultStepId,
            string resultAttemptId, string resultRequestId)
        {
            if (Empty(requestId) || Empty(requestPlanId) ||
                Empty(requestStepId))
                return ExecutionAdapterFailureCodes.MissingId;
            if (!Same(requestId, resultRequestId) ||
                !Same(requestPlanId, resultPlanId) ||
                !Same(requestStepId, resultStepId) ||
                !Same(requestAttemptId, resultAttemptId))
                return ExecutionAdapterFailureCodes.CorrelationMismatch;
            return string.Empty;
        }

        public static string ValidateResourceCorrelation(
            ResourceAcquireRequest request,
            ResourceAcquireResult result)
        {
            if (request == null || result == null)
                return ExecutionAdapterFailureCodes.NullSource;
            string common = ValidateCorrelation(
                request.PlanId, request.StepId,
                request.ExecutionAttemptId, request.RequestId,
                result.PlanId, result.StepId,
                result.ExecutionAttemptId, result.RequestId);
            if (!Empty(common)) return common;
            if (!Same(request.ResourceId, result.ResourceId) ||
                !Same(request.ResourceType, result.ResourceType) ||
                request.AccessMode != result.AccessMode)
                return ExecutionAdapterFailureCodes.CorrelationMismatch;
            if (result.Status == ExecutionResourceLeaseStatus.Acquired &&
                Empty(result.ExternalLeaseId))
                return ExecutionAdapterFailureCodes.MissingId;
            if (result.Status == ExecutionResourceLeaseStatus.Rejected &&
                !Empty(result.ExternalLeaseId))
                return ExecutionAdapterFailureCodes.InvalidPayload;
            if (result.Status != ExecutionResourceLeaseStatus.Acquired &&
                result.Status != ExecutionResourceLeaseStatus.Rejected)
                return ExecutionAdapterFailureCodes.InvalidStatus;
            return string.Empty;
        }

        public static string ValidateResourceCorrelation(
            ResourceReleaseRequest request,
            ResourceReleaseResult result)
        {
            if (request == null || result == null)
                return ExecutionAdapterFailureCodes.NullSource;
            if (Empty(request.RequestId) || Empty(request.PlanId) ||
                Empty(request.StepId) || Empty(request.ExternalLeaseId))
                return ExecutionAdapterFailureCodes.MissingId;
            if (!Same(request.RequestId, result.RequestId) ||
                !Same(request.PlanId, result.PlanId) ||
                !Same(request.StepId, result.StepId) ||
                !Same(request.ExternalLeaseId, result.ExternalLeaseId) ||
                !Same(request.ResourceId, result.ResourceId) ||
                !Same(request.ResourceType, result.ResourceType) ||
                request.AccessMode != result.AccessMode)
                return ExecutionAdapterFailureCodes.CorrelationMismatch;
            if (result.Status != ExecutionResourceLeaseStatus.Released &&
                result.Status != ExecutionResourceLeaseStatus.ReleaseFailed)
                return ExecutionAdapterFailureCodes.InvalidStatus;
            return string.Empty;
        }

        public static string ValidateScope(SafetyControlRequest request)
        {
            if (request == null)
                return ExecutionAdapterFailureCodes.NullSource;
            if (Empty(request.ControlRequestId) || Empty(request.PlanId))
                return ExecutionAdapterFailureCodes.MissingId;
            if (request.Scope != ExecutionControlScope.Step &&
                request.Scope != ExecutionControlScope.Plan &&
                request.Scope != ExecutionControlScope.Resource)
                return ExecutionAdapterFailureCodes.InvalidScope;
            if (request.Scope == ExecutionControlScope.Step &&
                Empty(request.TargetStepId))
                return ExecutionAdapterFailureCodes.MissingId;
            if (request.Scope == ExecutionControlScope.Resource &&
                Empty(request.TargetResourceId))
                return ExecutionAdapterFailureCodes.MissingId;
            if (request.ControlKind == ExecutionControlType.None)
                return ExecutionAdapterFailureCodes.InvalidStatus;
            return string.Empty;
        }

        internal static bool Empty(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        internal static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
