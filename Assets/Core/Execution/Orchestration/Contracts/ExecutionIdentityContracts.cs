// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    public sealed class ExecutionOrchestrationPolicyVersion
    {
        public const string PolicyV01Id =
            "salieri.execution.orchestration";
        public const int PolicyV01Major = 0;
        public const int PolicyV01Minor = 1;

        public string PolicyId { get; }
        public int Major { get; }
        public int Minor { get; }

        public ExecutionOrchestrationPolicyVersion(
            string policyId,
            int major,
            int minor)
        {
            PolicyId = ExecutionContractUtility.Text(policyId);
            Major = major;
            Minor = minor;
        }

        public bool IsPolicyV01 =>
            PolicyId == PolicyV01Id &&
            Major == PolicyV01Major &&
            Minor == PolicyV01Minor;
    }

    public sealed class ExecutionCorrelationIds
    {
        public string InteractionId { get; }
        public string InputId { get; }
        public string AnalysisId { get; }
        public string ReactionId { get; }
        public string OutputId { get; }

        public ExecutionCorrelationIds(
            string interactionId,
            string inputId,
            string analysisId,
            string reactionId,
            string outputId)
        {
            InteractionId = ExecutionContractUtility.Text(interactionId);
            InputId = ExecutionContractUtility.Text(inputId);
            AnalysisId = ExecutionContractUtility.Text(analysisId);
            ReactionId = ExecutionContractUtility.Text(reactionId);
            OutputId = ExecutionContractUtility.Text(outputId);
        }
    }

    /// <summary>
    /// Type-safe identity of a request owned by another domain. It never
    /// carries a delegate, executor instance, or mutable runtime object.
    /// </summary>
    public sealed class ExecutionRequestReference
    {
        public string RequestTypeId { get; }
        public string RequestId { get; }
        public string PayloadSchemaVersion { get; }

        public ExecutionRequestReference(
            string requestTypeId,
            string requestId,
            string payloadSchemaVersion)
        {
            RequestTypeId =
                ExecutionContractUtility.Text(requestTypeId);
            RequestId = ExecutionContractUtility.Text(requestId);
            PayloadSchemaVersion =
                ExecutionContractUtility.Text(payloadSchemaVersion);
        }
    }
}
