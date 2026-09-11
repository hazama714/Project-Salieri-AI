// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Language.Compiler.Validation
{
    /// <summary>
    /// Stable diagnostic codes produced by the pure CGL Semantic Validator.
    /// These codes describe semantic structure only. They do not represent
    /// grounding, capability, mode, safety, authorization, or execution
    /// outcomes.
    /// </summary>
    public static class CglSemanticValidationFailureCodes
    {
        public const string NullCandidateSet = "NullCandidateSet";
        public const string NullCandidate = "NullCandidate";

        public const string MissingCandidateSetId = "MissingCandidateSetId";
        public const string MissingCandidateId = "MissingCandidateId";
        public const string MissingSemanticIrId = "MissingSemanticIrId";
        public const string MissingInputId = "MissingInputId";
        public const string MissingInteractionId = "MissingInteractionId";
        public const string MissingAnalysisId = "MissingAnalysisId";

        public const string CandidateSetIdMismatch =
            "CandidateSetIdMismatch";
        public const string InputIdMismatch = "InputIdMismatch";
        public const string InteractionIdMismatch =
            "InteractionIdMismatch";
        public const string AnalysisIdMismatch = "AnalysisIdMismatch";

        public const string InvalidParserSource = "InvalidParserSource";
        public const string InvalidMessageType = "InvalidMessageType";
        public const string InvalidRequestType = "InvalidRequestType";
        public const string UnsupportedRequestType =
            "UnsupportedRequestType";
        public const string InvalidAction = "InvalidAction";
        public const string UnsupportedAction = "UnsupportedAction";
        public const string InvalidDirection = "InvalidDirection";
        public const string InvalidTargetRef = "InvalidTargetRef";
        public const string InvalidResolutionState =
            "InvalidResolutionState";
        public const string RequestTypeActionMismatch =
            "RequestTypeActionMismatch";
        public const string RequestTypeMessageTypeMismatch =
            "RequestTypeMessageTypeMismatch";

        public const string MissingDirection = "MissingDirection";
        public const string UnexpectedDirection = "UnexpectedDirection";
        public const string MissingTargetRef = "MissingTargetRef";
        public const string UnexpectedTargetRef = "UnexpectedTargetRef";
        public const string UnexpectedParameter = "UnexpectedParameter";

        public const string ConfirmationFlagMismatch =
            "ConfirmationFlagMismatch";
        public const string MissingAmbiguityReason =
            "MissingAmbiguityReason";
        public const string MissingUnresolvedDetail =
            "MissingUnresolvedDetail";
        public const string UnexpectedUnresolvedDetail =
            "UnexpectedUnresolvedDetail";
        public const string UnresolvedExecutableCandidate =
            "UnresolvedExecutableCandidate";
        public const string RejectedCandidate = "RejectedCandidate";

        public const string InvalidParameter = "InvalidParameter";
        public const string MissingParameterId = "MissingParameterId";
        public const string DuplicateParameter = "DuplicateParameter";
        public const string InvalidParameterValueType =
            "InvalidParameterValueType";
        public const string ParameterSpecificationMismatch =
            "ParameterSpecificationMismatch";
        public const string ParameterValueSlotMismatch =
            "ParameterValueSlotMismatch";
        public const string MissingParameterValue =
            "MissingParameterValue";
        public const string InvalidParameterUnit =
            "InvalidParameterUnit";
        public const string NonFiniteParameterValue =
            "NonFiniteParameterValue";

        public const string InvalidCandidateSetStatus =
            "InvalidCandidateSetStatus";
        public const string EmptySetStatusMismatch =
            "EmptySetStatusMismatch";
        public const string CandidateCountStatusMismatch =
            "CandidateCountStatusMismatch";
        public const string DuplicateCandidateId =
            "DuplicateCandidateId";
        public const string DuplicateSemanticIrId =
            "DuplicateSemanticIrId";

        public const string InternalValidationError =
            "InternalValidationError";
    }
}
