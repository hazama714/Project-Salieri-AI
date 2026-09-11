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
    /// Immutable record of candidate-set resolution.
    /// A SelectedCandidateId does not mean execution was authorized.
    /// </summary>
    public sealed class SemanticCandidateResolution
    {
        public string ResolutionId { get; }
        public string CandidateSetId { get; }
        public CandidateResolutionStatus Status { get; }
        public string SelectedCandidateId { get; }
        public IReadOnlyList<string> RejectedCandidateIds { get; }
        public bool RequiresClarification { get; }
        public bool RequiresExecutionAuthorization { get; }
        public DateTime ResolvedAtUtc { get; }
        public CandidateResolutionSource ResolutionSource { get; }
        public string DiagnosticMessage { get; }

        public SemanticCandidateResolution(
            string resolutionId,
            string candidateSetId,
            CandidateResolutionStatus status,
            string selectedCandidateId,
            IEnumerable<string> rejectedCandidateIds,
            bool requiresClarification,
            bool requiresExecutionAuthorization,
            DateTime resolvedAtUtc,
            CandidateResolutionSource resolutionSource,
            string diagnosticMessage)
        {
            ResolutionId = SccContractUtility.Text(resolutionId);
            CandidateSetId = SccContractUtility.Text(candidateSetId);
            Status = status;
            SelectedCandidateId =
                SccContractUtility.Text(selectedCandidateId);
            RejectedCandidateIds =
                SccContractUtility.ReadOnlyCopy(rejectedCandidateIds);
            RequiresClarification = requiresClarification;
            RequiresExecutionAuthorization =
                requiresExecutionAuthorization;
            ResolvedAtUtc = SccContractUtility.Utc(resolvedAtUtc);
            ResolutionSource = resolutionSource;
            DiagnosticMessage =
                SccContractUtility.Text(diagnosticMessage);
        }
    }
}
