// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

namespace SalieriAI.Core.Language.Compiler
{
    /// <summary>
    /// Immutable interpretation candidate produced by one parser analysis.
    /// A semantic candidate is not an execution command. Even Resolved does
    /// not imply validation, grounding, capability, authorization, or safety.
    /// </summary>
    public sealed class SemanticIrCandidate
    {
        public string SemanticIrId { get; }
        public string CandidateId { get; }
        public string CandidateSetId { get; }
        public string InputId { get; }
        public string InteractionId { get; }
        public string TraceId { get; }
        public string AnalysisId { get; }
        public int RequestGeneration { get; }

        public int SchemaMajor { get; }
        public int SchemaMinor { get; }

        public DateTime GeneratedAtUtc { get; }
        public DateTime SourceInputCreatedAtUtc { get; }
        public string NormalizedText { get; }

        public SemanticParserSource ParserSource { get; }
        public string ParserVersion { get; }
        public string ModelId { get; }
        public string ModelVersion { get; }
        public string PromptVersion { get; }
        public string GrammarVersion { get; }
        public string MatchedRuleId { get; }
        public string MatchedRuleVersion { get; }

        public SemanticMessageType MessageType { get; }
        public SemanticRequestType RequestType { get; }
        public SemanticAction Action { get; }
        public SemanticTargetRef TargetRef { get; }
        public SemanticDirection Direction { get; }
        public IReadOnlyList<SemanticParameter> Parameters { get; }

        public SemanticResolutionStatus ResolutionStatus { get; }
        public bool RequiresConfirmation { get; }
        public string AmbiguityReason { get; }
        public IReadOnlyList<string> UnresolvedFields { get; }

        public string FailureCode { get; }
        public string DiagnosticMessage { get; }

        /// <summary>
        /// Presentation and diagnostic rank only. It never grants automatic
        /// selection, authorization, or execution.
        /// </summary>
        public int CandidateRank { get; }

        public SemanticIrCandidate(
            string semanticIrId,
            string candidateId,
            string candidateSetId,
            string inputId,
            string interactionId,
            string traceId,
            string analysisId,
            int requestGeneration,
            int schemaMajor,
            int schemaMinor,
            DateTime generatedAtUtc,
            DateTime sourceInputCreatedAtUtc,
            string normalizedText,
            SemanticParserSource parserSource,
            string parserVersion,
            string modelId,
            string modelVersion,
            string promptVersion,
            string grammarVersion,
            string matchedRuleId,
            string matchedRuleVersion,
            SemanticMessageType messageType,
            SemanticRequestType requestType,
            SemanticAction action,
            SemanticTargetRef targetRef,
            SemanticDirection direction,
            IEnumerable<SemanticParameter> parameters,
            SemanticResolutionStatus resolutionStatus,
            bool requiresConfirmation,
            string ambiguityReason,
            IEnumerable<string> unresolvedFields,
            string failureCode,
            string diagnosticMessage,
            int candidateRank = 0)
        {
            SemanticIrId = SccContractUtility.Text(semanticIrId);
            CandidateId = SccContractUtility.Text(candidateId);
            CandidateSetId = SccContractUtility.Text(candidateSetId);
            InputId = SccContractUtility.Text(inputId);
            InteractionId = SccContractUtility.Text(interactionId);
            TraceId = SccContractUtility.Text(traceId);
            AnalysisId = SccContractUtility.Text(analysisId);
            RequestGeneration = requestGeneration;

            SchemaMajor = schemaMajor;
            SchemaMinor = schemaMinor;

            GeneratedAtUtc = SccContractUtility.Utc(generatedAtUtc);
            SourceInputCreatedAtUtc =
                SccContractUtility.Utc(sourceInputCreatedAtUtc);
            NormalizedText = SccContractUtility.Text(normalizedText);

            ParserSource = parserSource;
            ParserVersion = SccContractUtility.Text(parserVersion);
            ModelId = SccContractUtility.Text(modelId);
            ModelVersion = SccContractUtility.Text(modelVersion);
            PromptVersion = SccContractUtility.Text(promptVersion);
            GrammarVersion = SccContractUtility.Text(grammarVersion);
            MatchedRuleId = SccContractUtility.Text(matchedRuleId);
            MatchedRuleVersion =
                SccContractUtility.Text(matchedRuleVersion);

            MessageType = messageType;
            RequestType = requestType;
            Action = action;
            TargetRef = targetRef;
            Direction = direction;
            Parameters = SccContractUtility.ReadOnlyCopy(parameters);

            ResolutionStatus = resolutionStatus;
            RequiresConfirmation = requiresConfirmation;
            AmbiguityReason = SccContractUtility.Text(ambiguityReason);
            UnresolvedFields =
                SccContractUtility.ReadOnlyCopy(unresolvedFields);

            FailureCode = SccContractUtility.Text(failureCode);
            DiagnosticMessage =
                SccContractUtility.Text(diagnosticMessage);
            CandidateRank = candidateRank;
        }
    }
}
