// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Execution.Orchestration.Contracts
{
    public enum ExecutionSubmissionSourceKind
    {
        Unknown = 0,
        ConversationReaction = 1,
        Autonomous = 2,
        Idle = 3
    }

    /// <summary>
    /// Explicit request provenance supplied at Production Submission.
    /// Action semantics and plan lifecycle identities are intentionally not
    /// part of this contract.
    /// </summary>
    public sealed class ExecutionRequestProvenance
    {
        public ExecutionSubmissionSourceKind SourceKind { get; }
        public string SourceEventId { get; }
        public string InteractionId { get; }
        public string InputId { get; }
        public string AnalysisId { get; }
        public string ReactionId { get; }

        public ExecutionRequestProvenance(
            string interactionId,
            string inputId,
            string analysisId,
            string reactionId)
            : this(
                ExecutionSubmissionSourceKind.ConversationReaction,
                string.Empty,
                interactionId,
                inputId,
                analysisId,
                reactionId)
        {
        }

        public ExecutionRequestProvenance(
            ExecutionSubmissionSourceKind sourceKind,
            string sourceEventId,
            string interactionId,
            string inputId,
            string analysisId,
            string reactionId)
        {
            SourceKind = sourceKind;
            SourceEventId = ExecutionContractUtility.Text(sourceEventId);
            InteractionId = ExecutionContractUtility.Text(interactionId);
            InputId = ExecutionContractUtility.Text(inputId);
            AnalysisId = ExecutionContractUtility.Text(analysisId);
            ReactionId = ExecutionContractUtility.Text(reactionId);
        }

        public static ExecutionRequestProvenance ConversationReaction(
            string interactionId,
            string inputId,
            string reactionId)
        {
            return new ExecutionRequestProvenance(
                ExecutionSubmissionSourceKind.ConversationReaction,
                string.Empty,
                interactionId,
                inputId,
                string.Empty,
                reactionId);
        }

        public static ExecutionRequestProvenance Autonomous(
            string sourceEventId = "")
        {
            return SourceOnly(
                ExecutionSubmissionSourceKind.Autonomous,
                sourceEventId);
        }

        public static ExecutionRequestProvenance Idle(
            string sourceEventId = "")
        {
            return SourceOnly(
                ExecutionSubmissionSourceKind.Idle,
                sourceEventId);
        }

        private static ExecutionRequestProvenance SourceOnly(
            ExecutionSubmissionSourceKind sourceKind,
            string sourceEventId)
        {
            return new ExecutionRequestProvenance(
                sourceKind,
                sourceEventId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }
    }

    public enum ExecutionRequestProvenanceValidationStatus
    {
        Valid = 0,
        MissingContext = 1,
        UnknownSourceKind = 2,
        UnexpectedCorrelation = 3
    }

    public sealed class ExecutionRequestProvenanceValidationResult
    {
        public ExecutionRequestProvenanceValidationStatus Status { get; }
        public string FailureCode { get; }
        public bool IsValid =>
            Status == ExecutionRequestProvenanceValidationStatus.Valid;

        internal ExecutionRequestProvenanceValidationResult(
            ExecutionRequestProvenanceValidationStatus status,
            string failureCode)
        {
            Status = status;
            FailureCode = failureCode ?? string.Empty;
        }
    }

    /// <summary>
    /// Pure source-kind-specific validation. It never creates or substitutes
    /// correlation identity.
    /// </summary>
    public static class ExecutionRequestProvenanceValidator
    {
        public static ExecutionRequestProvenanceValidationResult Validate(
            ExecutionRequestProvenance provenance)
        {
            if (provenance == null)
                return Invalid(
                    ExecutionRequestProvenanceValidationStatus.MissingContext,
                    "EXECUTION_REQUEST_PROVENANCE_MISSING");

            switch (provenance.SourceKind)
            {
                case ExecutionSubmissionSourceKind.ConversationReaction:
                    if (Missing(provenance.InteractionId) ||
                        Missing(provenance.InputId) ||
                        Missing(provenance.ReactionId))
                    {
                        return Invalid(
                            ExecutionRequestProvenanceValidationStatus
                                .MissingContext,
                            "CONVERSATION_PROVENANCE_MISSING");
                    }

                    return Valid();

                case ExecutionSubmissionSourceKind.Autonomous:
                case ExecutionSubmissionSourceKind.Idle:
                    if (!Missing(provenance.InteractionId) ||
                        !Missing(provenance.InputId) ||
                        !Missing(provenance.AnalysisId) ||
                        !Missing(provenance.ReactionId))
                    {
                        return Invalid(
                            ExecutionRequestProvenanceValidationStatus
                                .UnexpectedCorrelation,
                            "SOURCE_ONLY_PROVENANCE_HAS_CONVERSATION_CONTEXT");
                    }

                    return Valid();

                default:
                    return Invalid(
                        ExecutionRequestProvenanceValidationStatus
                            .UnknownSourceKind,
                        "EXECUTION_SUBMISSION_SOURCE_KIND_UNKNOWN");
            }
        }

        private static bool Missing(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        private static ExecutionRequestProvenanceValidationResult Valid()
        {
            return new ExecutionRequestProvenanceValidationResult(
                ExecutionRequestProvenanceValidationStatus.Valid,
                string.Empty);
        }

        private static ExecutionRequestProvenanceValidationResult Invalid(
            ExecutionRequestProvenanceValidationStatus status,
            string failureCode)
        {
            return new ExecutionRequestProvenanceValidationResult(
                status,
                failureCode);
        }
    }
}
