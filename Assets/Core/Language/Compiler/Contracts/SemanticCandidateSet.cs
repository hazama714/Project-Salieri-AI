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
    /// Immutable collection of zero, one, or multiple semantic candidates
    /// produced for one interaction analysis.
    /// </summary>
    public sealed class SemanticCandidateSet
    {
        public string CandidateSetId { get; }
        public string InputId { get; }
        public string InteractionId { get; }
        public string AnalysisId { get; }
        public DateTime CreatedAtUtc { get; }
        public IReadOnlyList<SemanticIrCandidate> Candidates { get; }
        public SemanticCandidateSetStatus SetStatus { get; }
        public string DiagnosticMessage { get; }

        public SemanticCandidateSet(
            string candidateSetId,
            string inputId,
            string interactionId,
            string analysisId,
            DateTime createdAtUtc,
            IEnumerable<SemanticIrCandidate> candidates,
            SemanticCandidateSetStatus setStatus,
            string diagnosticMessage)
        {
            CandidateSetId = SccContractUtility.Text(candidateSetId);
            InputId = SccContractUtility.Text(inputId);
            InteractionId = SccContractUtility.Text(interactionId);
            AnalysisId = SccContractUtility.Text(analysisId);
            CreatedAtUtc = SccContractUtility.Utc(createdAtUtc);
            Candidates = SccContractUtility.ReadOnlyCopy(candidates);
            SetStatus = setStatus;
            DiagnosticMessage =
                SccContractUtility.Text(diagnosticMessage);
        }
    }
}
