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
    /// Immutable grounding result. Grounding is a middle-end responsibility,
    /// not a CGL parser responsibility.
    /// </summary>
    public sealed class GroundingResult
    {
        public string GroundingId { get; }
        public string CandidateId { get; }
        public GroundingStatus Status { get; }
        public string ResolvedTargetId { get; }
        public string ResolvedTargetType { get; }
        public SemanticDirection ResolvedDirection { get; }
        public IReadOnlyList<SemanticParameter> GroundedParameters { get; }
        public DateTime CheckedAtUtc { get; }
        public string FailureCode { get; }
        public string DiagnosticMessage { get; }

        public GroundingResult(
            string groundingId,
            string candidateId,
            GroundingStatus status,
            string resolvedTargetId,
            string resolvedTargetType,
            SemanticDirection resolvedDirection,
            IEnumerable<SemanticParameter> groundedParameters,
            DateTime checkedAtUtc,
            string failureCode,
            string diagnosticMessage)
        {
            GroundingId = SccContractUtility.Text(groundingId);
            CandidateId = SccContractUtility.Text(candidateId);
            Status = status;
            ResolvedTargetId =
                SccContractUtility.Text(resolvedTargetId);
            ResolvedTargetType =
                SccContractUtility.Text(resolvedTargetType);
            ResolvedDirection = resolvedDirection;
            GroundedParameters =
                SccContractUtility.ReadOnlyCopy(groundedParameters);
            CheckedAtUtc = SccContractUtility.Utc(checkedAtUtc);
            FailureCode = SccContractUtility.Text(failureCode);
            DiagnosticMessage =
                SccContractUtility.Text(diagnosticMessage);
        }
    }

    /// <summary>
    /// Grounded target identity used after semantic target resolution.
    /// It deliberately remains separate from SemanticTargetRef and does not
    /// carry a UnityEngine.Transform.
    /// </summary>
    public sealed class GroundedTargetReference
    {
        public string TargetId { get; }
        public string TargetType { get; }

        public GroundedTargetReference(
            string targetId,
            string targetType)
        {
            TargetId = SccContractUtility.Text(targetId);
            TargetType = SccContractUtility.Text(targetType);
        }
    }

    /// <summary>
    /// Immutable final semantic intent after validation and grounding.
    /// Safety cannot be released by SCC; these status values only record the
    /// downstream policy decision observed during compilation.
    /// </summary>
    public sealed class GroundedIntent
    {
        public string GroundedIntentId { get; }
        public string CandidateId { get; }
        public string GroundingId { get; }
        public string ValidationId { get; }
        public SemanticAction Action { get; }
        public GroundedTargetReference GroundedTarget { get; }
        public SemanticDirection Direction { get; }
        public IReadOnlyList<SemanticParameter> Parameters { get; }
        public CapabilityStatus CapabilityStatus { get; }
        public ModeValidationStatus ModeStatus { get; }
        public SafetyValidationStatus SafetyStatus { get; }
        public ExecutionAuthorizationStatus AuthorizationStatus { get; }
        public DateTime FinalizedAtUtc { get; }

        public GroundedIntent(
            string groundedIntentId,
            string candidateId,
            string groundingId,
            string validationId,
            SemanticAction action,
            GroundedTargetReference groundedTarget,
            SemanticDirection direction,
            IEnumerable<SemanticParameter> parameters,
            CapabilityStatus capabilityStatus,
            ModeValidationStatus modeStatus,
            SafetyValidationStatus safetyStatus,
            ExecutionAuthorizationStatus authorizationStatus,
            DateTime finalizedAtUtc)
        {
            GroundedIntentId =
                SccContractUtility.Text(groundedIntentId);
            CandidateId = SccContractUtility.Text(candidateId);
            GroundingId = SccContractUtility.Text(groundingId);
            ValidationId = SccContractUtility.Text(validationId);
            Action = action;
            GroundedTarget = groundedTarget;
            Direction = direction;
            Parameters = SccContractUtility.ReadOnlyCopy(parameters);
            CapabilityStatus = capabilityStatus;
            ModeStatus = modeStatus;
            SafetyStatus = safetyStatus;
            AuthorizationStatus = authorizationStatus;
            FinalizedAtUtc = SccContractUtility.Utc(finalizedAtUtc);
        }
    }
}
