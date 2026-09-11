// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    public sealed class ExecutionDependency
    {
        public string DependencyId { get; }
        public string PlanId { get; }
        public string PredecessorStepId { get; }
        public string SuccessorStepId { get; }
        public ExecutionDependencyGate GateType { get; }
        public ExecutionTerminalOutcome RequiredOutcome { get; }
        public bool Optional { get; }

        public ExecutionDependency(
            string dependencyId,
            string planId,
            string predecessorStepId,
            string successorStepId,
            ExecutionDependencyGate gateType,
            ExecutionTerminalOutcome requiredOutcome,
            bool optional)
        {
            DependencyId =
                ExecutionContractUtility.Text(dependencyId);
            PlanId = ExecutionContractUtility.Text(planId);
            PredecessorStepId =
                ExecutionContractUtility.Text(predecessorStepId);
            SuccessorStepId =
                ExecutionContractUtility.Text(successorStepId);
            GateType = gateType;
            RequiredOutcome = requiredOutcome;
            Optional = optional;
        }
    }
}
