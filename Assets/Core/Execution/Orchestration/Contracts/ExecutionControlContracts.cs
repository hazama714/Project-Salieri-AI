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
    public sealed class ExecutionControlRequest
    {
        public string ControlRequestId { get; }
        public string TargetPlanId { get; }
        public string TargetStepId { get; }
        public ExecutionControlType ControlType { get; }
        public ExecutionControlScope Scope { get; }
        public string RequestedBy { get; }
        public string ReasonCode { get; }
        public int Priority { get; }
        public DateTime RequestedAtUtc { get; }

        public ExecutionControlRequest(
            string controlRequestId,
            string targetPlanId,
            string targetStepId,
            ExecutionControlType controlType,
            ExecutionControlScope scope,
            string requestedBy,
            string reasonCode,
            int priority,
            DateTime requestedAtUtc)
        {
            ControlRequestId =
                ExecutionContractUtility.Text(controlRequestId);
            TargetPlanId =
                ExecutionContractUtility.Text(targetPlanId);
            TargetStepId =
                ExecutionContractUtility.Text(targetStepId);
            ControlType = controlType;
            Scope = scope;
            RequestedBy =
                ExecutionContractUtility.Text(requestedBy);
            ReasonCode =
                ExecutionContractUtility.Text(reasonCode);
            Priority = priority;
            RequestedAtUtc =
                ExecutionContractUtility.Utc(requestedAtUtc);
        }
    }

    /// <summary>
    /// Records admission of a control request only. It does not prove that
    /// the target stopped or that cleanup completed.
    /// </summary>
    public sealed class ExecutionControlAcceptanceResult
    {
        public string ControlRequestId { get; }
        public ExecutionControlAcceptanceStatus Status { get; }
        public string ReasonCode { get; }
        public DateTime DecidedAtUtc { get; }

        public ExecutionControlAcceptanceResult(
            string controlRequestId,
            ExecutionControlAcceptanceStatus status,
            string reasonCode,
            DateTime decidedAtUtc)
        {
            ControlRequestId =
                ExecutionContractUtility.Text(controlRequestId);
            Status = status;
            ReasonCode =
                ExecutionContractUtility.Text(reasonCode);
            DecidedAtUtc = ExecutionContractUtility.Utc(decidedAtUtc);
        }
    }

    /// <summary>
    /// Separately records observed effect after a control request was
    /// admitted. Acceptance and effect are intentionally not conflated.
    /// </summary>
    public sealed class ExecutionControlEffectResult
    {
        public string ControlRequestId { get; }
        public ExecutionControlEffectStatus Status { get; }
        public DateTime ObservedAtUtc { get; }
        public IReadOnlyList<string> FailureCodes { get; }

        public ExecutionControlEffectResult(
            string controlRequestId,
            ExecutionControlEffectStatus status,
            DateTime observedAtUtc,
            IEnumerable<string> failureCodes)
        {
            ControlRequestId =
                ExecutionContractUtility.Text(controlRequestId);
            Status = status;
            ObservedAtUtc =
                ExecutionContractUtility.Utc(observedAtUtc);
            FailureCodes =
                ExecutionContractUtility.ReadOnlyCopy(failureCodes);
        }
    }

    public static class ExecutionControlPriorityPolicyV01
    {
        public static int GetRank(ExecutionControlType controlType)
        {
            switch (controlType)
            {
                case ExecutionControlType.EmergencyPreempt:
                    return 600;
                case ExecutionControlType.SafetyStop:
                    return 500;
                case ExecutionControlType.Interrupt:
                    return 400;
                case ExecutionControlType.Stop:
                    return 350;
                case ExecutionControlType.Cancel:
                    return 300;
                case ExecutionControlType.Pause:
                case ExecutionControlType.Resume:
                    return 200;
                default:
                    return 100;
            }
        }
    }
}

