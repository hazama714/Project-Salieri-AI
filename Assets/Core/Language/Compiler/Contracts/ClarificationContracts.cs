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
    /// Immutable clarification session. Candidate selection and execution
    /// authorization are separate states and must not be inferred from one
    /// another.
    /// </summary>
    public sealed class ClarificationSession
    {
        public string ClarificationSessionId { get; }
        public string ParentInteractionId { get; }
        public string CandidateSetId { get; }
        public string OriginalInputId { get; }
        public ClarificationSessionStatus Status { get; }
        public IReadOnlyList<string> PresentedCandidateIds { get; }
        public string SelectedCandidateId { get; }
        public IReadOnlyList<string> RejectedCandidateIds { get; }
        public DateTime CreatedAtUtc { get; }
        public DateTime ExpiresAtUtc { get; }
        public int TurnCount { get; }

        public ClarificationSession(
            string clarificationSessionId,
            string parentInteractionId,
            string candidateSetId,
            string originalInputId,
            ClarificationSessionStatus status,
            IEnumerable<string> presentedCandidateIds,
            string selectedCandidateId,
            IEnumerable<string> rejectedCandidateIds,
            DateTime createdAtUtc,
            DateTime expiresAtUtc,
            int turnCount)
        {
            ClarificationSessionId =
                SccContractUtility.Text(clarificationSessionId);
            ParentInteractionId =
                SccContractUtility.Text(parentInteractionId);
            CandidateSetId = SccContractUtility.Text(candidateSetId);
            OriginalInputId = SccContractUtility.Text(originalInputId);
            Status = status;
            PresentedCandidateIds =
                SccContractUtility.ReadOnlyCopy(presentedCandidateIds);
            SelectedCandidateId =
                SccContractUtility.Text(selectedCandidateId);
            RejectedCandidateIds =
                SccContractUtility.ReadOnlyCopy(rejectedCandidateIds);
            CreatedAtUtc = SccContractUtility.Utc(createdAtUtc);
            ExpiresAtUtc = SccContractUtility.Utc(expiresAtUtc);
            TurnCount = turnCount;
        }
    }

    /// <summary>
    /// One immutable human response associated with a clarification session.
    /// SelectCandidate does not authorize execution. AuthorizeExecution is a
    /// distinct response type.
    /// </summary>
    public sealed class ClarificationResponse
    {
        public string ClarificationResponseId { get; }
        public string ClarificationSessionId { get; }
        public string InputId { get; }
        public ClarificationResponseType ResponseType { get; }
        public string SelectedCandidateId { get; }
        public string RawResponseText { get; }
        public DateTime ReceivedAtUtc { get; }

        public ClarificationResponse(
            string clarificationResponseId,
            string clarificationSessionId,
            string inputId,
            ClarificationResponseType responseType,
            string selectedCandidateId,
            string rawResponseText,
            DateTime receivedAtUtc)
        {
            ClarificationResponseId =
                SccContractUtility.Text(clarificationResponseId);
            ClarificationSessionId =
                SccContractUtility.Text(clarificationSessionId);
            InputId = SccContractUtility.Text(inputId);
            ResponseType = responseType;
            SelectedCandidateId =
                SccContractUtility.Text(selectedCandidateId);
            RawResponseText =
                SccContractUtility.Text(rawResponseText);
            ReceivedAtUtc = SccContractUtility.Utc(receivedAtUtc);
        }
    }
}
