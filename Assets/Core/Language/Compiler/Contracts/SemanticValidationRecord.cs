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
    /// Immutable validation result recorded separately from its candidate.
    /// A candidate marked Resolved is not implicitly valid.
    /// </summary>
    public sealed class SemanticValidationRecord
    {
        public string ValidationId { get; }
        public string CandidateId { get; }
        public string CandidateSetId { get; }
        public SemanticValidationStatus Status { get; }
        public DateTime CheckedAtUtc { get; }
        public IReadOnlyList<string> FailureCodes { get; }
        public IReadOnlyList<string> InvalidFields { get; }
        public bool RequiresClarification { get; }
        public string DiagnosticMessage { get; }

        public SemanticValidationRecord(
            string validationId,
            string candidateId,
            string candidateSetId,
            SemanticValidationStatus status,
            DateTime checkedAtUtc,
            IEnumerable<string> failureCodes,
            IEnumerable<string> invalidFields,
            bool requiresClarification,
            string diagnosticMessage)
        {
            ValidationId = SccContractUtility.Text(validationId);
            CandidateId = SccContractUtility.Text(candidateId);
            CandidateSetId = SccContractUtility.Text(candidateSetId);
            Status = status;
            CheckedAtUtc = SccContractUtility.Utc(checkedAtUtc);
            FailureCodes =
                SccContractUtility.ReadOnlyCopy(failureCodes);
            InvalidFields =
                SccContractUtility.ReadOnlyCopy(invalidFields);
            RequiresClarification = requiresClarification;
            DiagnosticMessage =
                SccContractUtility.Text(diagnosticMessage);
        }
    }
}
