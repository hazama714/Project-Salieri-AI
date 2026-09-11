// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Language.Compiler
{
    /// <summary>
    /// Immutable SCC completion result. This contract never carries Skill
    /// execution outcomes such as Success, Miss, or Interrupted.
    /// </summary>
    public sealed class SccCompilerResult
    {
        public string CompilationId { get; }
        public string InputId { get; }
        public string InteractionId { get; }
        public SccCompilerStatus Status { get; }
        public string CandidateSetId { get; }
        public string SelectedCandidateId { get; }
        public string GroundedIntentId { get; }
        public string SkillRequestId { get; }
        public DateTime CompletedAtUtc { get; }
        public string FailureCode { get; }
        public string DiagnosticMessage { get; }

        public SccCompilerResult(
            string compilationId,
            string inputId,
            string interactionId,
            SccCompilerStatus status,
            string candidateSetId,
            string selectedCandidateId,
            string groundedIntentId,
            string skillRequestId,
            DateTime completedAtUtc,
            string failureCode,
            string diagnosticMessage)
        {
            CompilationId = SccContractUtility.Text(compilationId);
            InputId = SccContractUtility.Text(inputId);
            InteractionId = SccContractUtility.Text(interactionId);
            Status = status;
            CandidateSetId = SccContractUtility.Text(candidateSetId);
            SelectedCandidateId =
                SccContractUtility.Text(selectedCandidateId);
            GroundedIntentId =
                SccContractUtility.Text(groundedIntentId);
            SkillRequestId =
                SccContractUtility.Text(skillRequestId);
            CompletedAtUtc = SccContractUtility.Utc(completedAtUtc);
            FailureCode = SccContractUtility.Text(failureCode);
            DiagnosticMessage =
                SccContractUtility.Text(diagnosticMessage);
        }
    }
}
