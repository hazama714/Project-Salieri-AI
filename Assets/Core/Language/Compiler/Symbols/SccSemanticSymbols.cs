// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Language.Compiler
{
    /// <summary>
    /// Identifies the producer of a semantic candidate.
    /// A parser source is provenance, not execution authority.
    /// </summary>
    public enum SemanticParserSource
    {
        Unknown = 0,
        SafetyRule = 1,
        ActiveRule = 2,
        DeterministicPattern = 3,
        LlmGrammar = 4,
        HumanCorrection = 5,
        LegacyShadow = 6,
        ProvisionalRule = 7,
        SystemEvent = 8,
        UiDirectCommand = 9
    }

    public enum SemanticMessageType
    {
        Unknown = 0,
        ConversationInput = 1,
        SemanticCommand = 2,
        TeachingInput = 3,
        CorrectionInput = 4,
        ConfirmationInput = 5,
        SystemControl = 6
    }

    public enum SemanticRequestType
    {
        Unknown = 0,
        Activation = 1,
        Conversation = 2,
        Action = 3,
        Stop = 4,
        Emergency = 5,
        Query = 6,
        Teaching = 7,
        Correction = 8,
        Confirmation = 9,
        CandidateSelection = 10,
        CandidateRejection = 11,
        Cancellation = 12,
        SystemEvent = 13
    }

    /// <summary>
    /// Platform-independent semantic actions.
    /// These values are not BodyCommand IDs, ProgrammedActionType values,
    /// Skill IDs, servo IDs, or device commands.
    /// </summary>
    public enum SemanticAction
    {
        Unknown = 0,
        None = 1,
        Conversation = 2,
        LookTarget = 3,
        LookDirection = 4,
        TurnHead = 5,
        TurnUpperBody = 6,
        TurnBody = 7,
        RotatePlatform = 8,
        MoveDirection = 9,
        PointTarget = 10,
        Stop = 11,
        EmergencyStop = 12
    }

    /// <summary>
    /// Abstract target reference used before grounding.
    /// It must not be treated as a Scene Transform, Observation TargetKey,
    /// or Attention Target identifier.
    /// </summary>
    public enum SemanticTargetRef
    {
        Unknown = 0,
        None = 1,
        Unspecified = 2,
        CurrentAttentionTarget = 3,
        PreviousAttentionTarget = 4,
        CurrentObservationTarget = 5,
        Speaker = 6,
        Self = 7,
        This = 8,
        That = 9,
        DirectionOnly = 10,
        Unresolved = 11
    }

    public enum SemanticDirection
    {
        Unknown = 0,
        None = 1,
        Unspecified = 2,
        Left = 3,
        Right = 4,
        Forward = 5,
        Backward = 6,
        Up = 7,
        Down = 8
    }

    public enum SemanticParameterValueType
    {
        Unspecified = 0,
        String = 1,
        Integer = 2,
        Float = 3,
        Boolean = 4,
        Enum = 5
    }

    /// <summary>
    /// Semantic resolution alone does not imply validation, grounding,
    /// capability availability, authorization, or execution.
    /// </summary>
    public enum SemanticResolutionStatus
    {
        Unknown = 0,
        Unresolved = 1,
        PartiallyResolved = 2,
        Ambiguous = 3,
        Resolved = 4,
        Invalid = 5,
        Rejected = 6
    }

    public enum SemanticCandidateSetStatus
    {
        Unknown = 0,
        Empty = 1,
        Generated = 2,
        Validated = 3,
        Ambiguous = 4,
        AwaitingSelection = 5,
        Selected = 6,
        Rejected = 7,
        Cancelled = 8,
        Expired = 9
    }

    public enum SemanticValidationStatus
    {
        NotChecked = 0,
        Valid = 1,
        Invalid = 2,
        NeedsClarification = 3,
        InternalError = 4
    }

    public enum CandidateResolutionStatus
    {
        NotStarted = 0,
        Ambiguous = 1,
        AwaitingSelection = 2,
        Selected = 3,
        AllRejected = 4,
        Cancelled = 5,
        Expired = 6,
        Failed = 7
    }

    public enum CandidateResolutionSource
    {
        Unknown = 0,
        Policy = 1,
        HumanSelection = 2,
        HumanCorrection = 3,
        ActiveRule = 4,
        SafetyRule = 5,
        UiDirectCommand = 6,
        SystemEvent = 7
    }

    public enum ClarificationSessionStatus
    {
        Unknown = 0,
        Created = 1,
        AwaitingResponse = 2,
        CandidateSelected = 3,
        AwaitingAuthorization = 4,
        ExecutionAuthorized = 5,
        CandidateRejected = 6,
        AllCandidatesRejected = 7,
        AwaitingTeaching = 8,
        Resolved = 9,
        Cancelled = 10,
        Expired = 11
    }

    public enum ClarificationResponseType
    {
        Unknown = 0,
        SelectCandidate = 1,
        AuthorizeExecution = 2,
        RejectCandidate = 3,
        RejectAll = 4,
        TeachMeaning = 5,
        Cancel = 6,
        Unclear = 7
    }

    public enum GroundingStatus
    {
        NotStarted = 0,
        NotRequired = 1,
        Succeeded = 2,
        Ambiguous = 3,
        Failed = 4,
        Expired = 5
    }

    public enum CapabilityStatus
    {
        Unknown = 0,
        NotChecked = 1,
        Available = 2,
        Unavailable = 3,
        NotImplemented = 4,
        Disabled = 5
    }

    public enum ModeValidationStatus
    {
        Unknown = 0,
        NotChecked = 1,
        Allowed = 2,
        Rejected = 3
    }

    public enum SafetyValidationStatus
    {
        Unknown = 0,
        NotChecked = 1,
        Allowed = 2,
        Rejected = 3,
        EmergencyActive = 4
    }

    public enum ExecutionAuthorizationStatus
    {
        Unknown = 0,
        NotRequired = 1,
        Required = 2,
        Authorized = 3,
        Rejected = 4,
        Expired = 5
    }

    /// <summary>
    /// Compiler completion status only. Skill execution outcomes such as
    /// Success or Miss must not be stored here.
    /// </summary>
    public enum SccCompilerStatus
    {
        Unknown = 0,
        Compiled = 1,
        NeedsClarification = 2,
        Unresolved = 3,
        InvalidInput = 4,
        RejectedByPolicy = 5,
        GroundingFailed = 6,
        CapabilityUnavailable = 7,
        AuthorizationRequired = 8,
        Cancelled = 9,
        Expired = 10,
        InternalError = 11
    }
}
