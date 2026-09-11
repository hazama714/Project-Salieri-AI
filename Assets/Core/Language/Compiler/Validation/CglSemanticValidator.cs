// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SalieriAI.Core.Language.Compiler.Validation
{
    /// <summary>
    /// Caller-controlled identity and time for one validation record.
    /// Supplying this context makes validation deterministic in tests without
    /// mutable global clocks or ID generators.
    /// </summary>
    public sealed class CglSemanticValidationContext
    {
        public string ValidationId { get; }
        public DateTime CheckedAtUtc { get; }

        public CglSemanticValidationContext(
            string validationId,
            DateTime checkedAtUtc)
        {
            ValidationId = validationId ?? string.Empty;
            CheckedAtUtc = NormalizeUtc(checkedAtUtc);
        }

        public static CglSemanticValidationContext CreateDefault()
        {
            return new CglSemanticValidationContext(
                "validation_" + Guid.NewGuid().ToString("N"),
                DateTime.UtcNow
            );
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default(DateTime))
                return value;

            if (value.Kind == DateTimeKind.Utc)
                return value;

            if (value.Kind == DateTimeKind.Local)
                return value.ToUniversalTime();

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Pure set-level result. Candidate records remain separate and immutable.
    /// This result does not select a candidate or authorize execution.
    /// </summary>
    public sealed class CglSemanticCandidateSetValidationResult
    {
        public string CandidateSetId { get; }
        public SemanticValidationStatus Status { get; }
        public DateTime CheckedAtUtc { get; }
        public IReadOnlyList<string> FailureCodes { get; }
        public IReadOnlyList<string> InvalidFields { get; }
        public bool RequiresClarification { get; }
        public string DiagnosticMessage { get; }
        public IReadOnlyList<SemanticValidationRecord> CandidateRecords
        {
            get;
        }

        public bool IsStructurallyValid =>
            Status == SemanticValidationStatus.Valid ||
            Status == SemanticValidationStatus.NeedsClarification;

        internal CglSemanticCandidateSetValidationResult(
            string candidateSetId,
            SemanticValidationStatus status,
            DateTime checkedAtUtc,
            IEnumerable<string> failureCodes,
            IEnumerable<string> invalidFields,
            bool requiresClarification,
            string diagnosticMessage,
            IEnumerable<SemanticValidationRecord> candidateRecords)
        {
            CandidateSetId = candidateSetId ?? string.Empty;
            Status = status;
            CheckedAtUtc = checkedAtUtc;
            FailureCodes = ReadOnlyCopy(failureCodes);
            InvalidFields = ReadOnlyCopy(invalidFields);
            RequiresClarification = requiresClarification;
            DiagnosticMessage = diagnosticMessage ?? string.Empty;
            CandidateRecords = ReadOnlyCopy(candidateRecords);
        }

        private static IReadOnlyList<T> ReadOnlyCopy<T>(
            IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(
                source != null
                    ? new List<T>(source)
                    : new List<T>()
            );
        }
    }

    /// <summary>
    /// Side-effect-free CGL Semantic Validator.
    ///
    /// It checks only the internal structure of a SemanticCandidateSet and
    /// SemanticIrCandidate. It does not mutate candidates, select a candidate,
    /// ground a target, inspect Scene state, check capability/mode/safety, or
    /// create/execute a Skill request.
    /// </summary>
    public static class CglSemanticValidator
    {
        public const string ValidatorVersion =
            "cgl-semantic-validator-3c1.1";

        public static SemanticValidationRecord Validate(
            SemanticCandidateSet candidateSet,
            SemanticIrCandidate candidate)
        {
            return Validate(
                candidateSet,
                candidate,
                CglSemanticValidationContext.CreateDefault()
            );
        }

        public static SemanticValidationRecord Validate(
            SemanticCandidateSet candidateSet,
            SemanticIrCandidate candidate,
            CglSemanticValidationContext context)
        {
            CglSemanticValidationContext safeContext =
                context ?? CglSemanticValidationContext.CreateDefault();

            try
            {
                ValidationIssues issues = new ValidationIssues();

                ValidateCandidateSetIdentity(
                    candidateSet,
                    candidate,
                    issues
                );

                if (candidate != null)
                {
                    ValidateEnums(candidate, issues);
                    ValidateMessageAndAction(candidate, issues);
                    ValidateParameters(candidate, issues);
                    ValidateResolution(candidate, issues);
                }

                return CreateRecord(
                    candidateSet,
                    candidate,
                    safeContext,
                    issues
                );
            }
            catch (Exception exception)
            {
                return new SemanticValidationRecord(
                    safeContext.ValidationId,
                    candidate != null
                        ? candidate.CandidateId
                        : string.Empty,
                    candidateSet != null
                        ? candidateSet.CandidateSetId
                        : string.Empty,
                    SemanticValidationStatus.InternalError,
                    safeContext.CheckedAtUtc,
                    new[]
                    {
                        CglSemanticValidationFailureCodes
                            .InternalValidationError
                    },
                    new string[0],
                    false,
                    exception.GetType().Name + ": " + exception.Message
                );
            }
        }

        public static IReadOnlyList<SemanticValidationRecord> ValidateSet(
            SemanticCandidateSet candidateSet)
        {
            return ValidateSetDetailed(candidateSet).CandidateRecords;
        }

        public static CglSemanticCandidateSetValidationResult
            ValidateSetDetailed(SemanticCandidateSet candidateSet)
        {
            return ValidateSetDetailed(candidateSet, null);
        }

        public static CglSemanticCandidateSetValidationResult
            ValidateSetDetailed(
                SemanticCandidateSet candidateSet,
                Func<
                    SemanticIrCandidate,
                    CglSemanticValidationContext> contextFactory)
        {
            DateTime checkedAtUtc = DateTime.UtcNow;

            try
            {
                ValidationIssues setIssues = new ValidationIssues();
                List<SemanticValidationRecord> records =
                    new List<SemanticValidationRecord>();

                ValidateSetStructure(candidateSet, setIssues);

                if (candidateSet != null)
                {
                    for (
                        int i = 0;
                        i < candidateSet.Candidates.Count;
                        i++)
                    {
                        SemanticIrCandidate candidate =
                            candidateSet.Candidates[i];
                        CglSemanticValidationContext context =
                            contextFactory != null
                                ? contextFactory(candidate)
                                : CglSemanticValidationContext
                                    .CreateDefault();

                        if (context == null)
                        {
                            context = CglSemanticValidationContext
                                .CreateDefault();
                        }

                        checkedAtUtc = context.CheckedAtUtc;
                        records.Add(
                            Validate(candidateSet, candidate, context)
                        );
                    }
                }

                SemanticValidationStatus status =
                    ResolveSetStatus(setIssues, records);
                bool requiresClarification =
                    setIssues.NeedsClarification ||
                    ContainsStatus(
                        records,
                        SemanticValidationStatus.NeedsClarification
                    );

                return new CglSemanticCandidateSetValidationResult(
                    candidateSet != null
                        ? candidateSet.CandidateSetId
                        : string.Empty,
                    status,
                    checkedAtUtc,
                    setIssues.FailureCodes,
                    setIssues.InvalidFields,
                    requiresClarification,
                    BuildDiagnostic(
                        setIssues,
                        "Candidate Set validation completed."
                    ),
                    records
                );
            }
            catch (Exception exception)
            {
                return new CglSemanticCandidateSetValidationResult(
                    candidateSet != null
                        ? candidateSet.CandidateSetId
                        : string.Empty,
                    SemanticValidationStatus.InternalError,
                    checkedAtUtc,
                    new[]
                    {
                        CglSemanticValidationFailureCodes
                            .InternalValidationError
                    },
                    new string[0],
                    false,
                    exception.GetType().Name + ": " + exception.Message,
                    null
                );
            }
        }

        private static void ValidateCandidateSetIdentity(
            SemanticCandidateSet candidateSet,
            SemanticIrCandidate candidate,
            ValidationIssues issues)
        {
            if (candidateSet == null)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.NullCandidateSet,
                    "candidateSet"
                );
            }

            if (candidate == null)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.NullCandidate,
                    "candidate"
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(candidate.CandidateId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.MissingCandidateId,
                    "candidateId"
                );
            }

            if (string.IsNullOrWhiteSpace(candidate.SemanticIrId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.MissingSemanticIrId,
                    "semanticIrId"
                );
            }

            if (string.IsNullOrWhiteSpace(candidate.CandidateSetId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .MissingCandidateSetId,
                    "candidateSetId"
                );
            }

            if (string.IsNullOrWhiteSpace(candidate.InputId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.MissingInputId,
                    "inputId"
                );
            }

            if (string.IsNullOrWhiteSpace(candidate.InteractionId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .MissingInteractionId,
                    "interactionId"
                );
            }

            if (string.IsNullOrWhiteSpace(candidate.AnalysisId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.MissingAnalysisId,
                    "analysisId"
                );
            }

            if (candidateSet == null)
                return;

            if (!Same(
                    candidate.CandidateSetId,
                    candidateSet.CandidateSetId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .CandidateSetIdMismatch,
                    "candidateSetId"
                );
            }

            if (!Same(candidate.InputId, candidateSet.InputId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.InputIdMismatch,
                    "inputId"
                );
            }

            if (!Same(
                    candidate.InteractionId,
                    candidateSet.InteractionId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .InteractionIdMismatch,
                    "interactionId"
                );
            }

            if (!Same(candidate.AnalysisId, candidateSet.AnalysisId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.AnalysisIdMismatch,
                    "analysisId"
                );
            }
        }

        private static void ValidateEnums(
            SemanticIrCandidate candidate,
            ValidationIssues issues)
        {
            if (!IsDefined(candidate.ParserSource) ||
                candidate.ParserSource == SemanticParserSource.Unknown)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.InvalidParserSource,
                    "parserSource"
                );
            }

            if (!IsDefined(candidate.MessageType) ||
                candidate.MessageType == SemanticMessageType.Unknown)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.InvalidMessageType,
                    "messageType"
                );
            }

            if (!IsDefined(candidate.RequestType) ||
                candidate.RequestType == SemanticRequestType.Unknown)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.InvalidRequestType,
                    "requestType"
                );
            }

            if (!IsDefined(candidate.Action) ||
                candidate.Action == SemanticAction.Unknown)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.InvalidAction,
                    "action"
                );
            }

            if (!IsDefined(candidate.Direction))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.InvalidDirection,
                    "direction"
                );
            }

            if (!IsDefined(candidate.TargetRef))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.InvalidTargetRef,
                    "targetRef"
                );
            }

            if (!IsDefined(candidate.ResolutionStatus))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .InvalidResolutionState,
                    "resolutionStatus"
                );
            }
        }

        private static void ValidateMessageAndAction(
            SemanticIrCandidate candidate,
            ValidationIssues issues)
        {
            SemanticMessageType expectedMessageType;
            if (!TryGetExpectedMessageType(
                    candidate.RequestType,
                    out expectedMessageType))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .UnsupportedRequestType,
                    "requestType"
                );
            }
            else if (candidate.MessageType != expectedMessageType)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .RequestTypeMessageTypeMismatch,
                    "messageType"
                );
            }

            if (!IsActionAllowedForRequest(
                    candidate.RequestType,
                    candidate.Action))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .RequestTypeActionMismatch,
                    "action"
                );
            }

            switch (candidate.Action)
            {
                case SemanticAction.None:
                    ValidateNoDirectionTargetOrParameters(
                        candidate,
                        issues
                    );
                    break;

                case SemanticAction.Conversation:
                    ValidateNoDirectionTargetOrParameters(
                        candidate,
                        issues
                    );
                    break;

                case SemanticAction.LookDirection:
                case SemanticAction.TurnHead:
                case SemanticAction.TurnUpperBody:
                case SemanticAction.TurnBody:
                case SemanticAction.RotatePlatform:
                case SemanticAction.MoveDirection:
                    ValidateDirectionAction(candidate, issues);
                    break;

                case SemanticAction.LookTarget:
                case SemanticAction.PointTarget:
                    ValidateTargetAction(candidate, issues);
                    break;

                case SemanticAction.Stop:
                case SemanticAction.EmergencyStop:
                    ValidateNoDirectionTargetOrParameters(
                        candidate,
                        issues
                    );
                    break;

                case SemanticAction.Unknown:
                    break;

                default:
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes
                            .UnsupportedAction,
                        "action"
                    );
                    break;
            }
        }

        private static void ValidateDirectionAction(
            SemanticIrCandidate candidate,
            ValidationIssues issues)
        {
            if (!HasConcreteDirection(candidate.Direction))
            {
                AddMissingRequirement(
                    candidate,
                    issues,
                    CglSemanticValidationFailureCodes.MissingDirection,
                    "direction"
                );
            }

            if (!IsDirectionOnlyOrEmpty(candidate.TargetRef))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.UnexpectedTargetRef,
                    "targetRef"
                );
            }
        }

        private static void ValidateTargetAction(
            SemanticIrCandidate candidate,
            ValidationIssues issues)
        {
            if (!HasSemanticTarget(candidate.TargetRef))
            {
                AddMissingRequirement(
                    candidate,
                    issues,
                    CglSemanticValidationFailureCodes.MissingTargetRef,
                    "targetRef"
                );
            }

            if (candidate.Direction == SemanticDirection.Unknown)
            {
                AddMissingRequirement(
                    candidate,
                    issues,
                    CglSemanticValidationFailureCodes.InvalidDirection,
                    "direction"
                );
            }
        }

        private static void ValidateNoDirectionTargetOrParameters(
            SemanticIrCandidate candidate,
            ValidationIssues issues)
        {
            if (!IsEmptyDirection(candidate.Direction))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .UnexpectedDirection,
                    "direction"
                );
            }

            if (!IsEmptyTarget(candidate.TargetRef))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .UnexpectedTargetRef,
                    "targetRef"
                );
            }

            if (candidate.Parameters.Count > 0)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .UnexpectedParameter,
                    "parameters"
                );
            }
        }

        private static void ValidateParameters(
            SemanticIrCandidate candidate,
            ValidationIssues issues)
        {
            HashSet<string> parameterIds =
                new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < candidate.Parameters.Count; i++)
            {
                SemanticParameter parameter = candidate.Parameters[i];
                string field = "parameters[" + i + "]";

                if (parameter == null)
                {
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes
                            .InvalidParameter,
                        field
                    );
                    continue;
                }

                if (string.IsNullOrWhiteSpace(parameter.ParameterId))
                {
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes
                            .MissingParameterId,
                        field + ".parameterId"
                    );
                }
                else if (!parameterIds.Add(parameter.ParameterId))
                {
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes
                            .DuplicateParameter,
                        field + ".parameterId"
                    );
                }

                ValidateParameterValue(parameter, field, issues);
            }
        }

        private static void ValidateParameterValue(
            SemanticParameter parameter,
            string field,
            ValidationIssues issues)
        {
            if (!IsDefined(parameter.ValueType))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .InvalidParameterValueType,
                    field + ".valueType"
                );
                return;
            }

            bool emptyString =
                string.IsNullOrEmpty(parameter.StringValue);
            bool emptyEnum = string.IsNullOrEmpty(parameter.EnumValue);
            bool numericUnitAllowed =
                parameter.ValueType ==
                    SemanticParameterValueType.Integer ||
                parameter.ValueType ==
                    SemanticParameterValueType.Float;

            if (!numericUnitAllowed &&
                !string.IsNullOrEmpty(parameter.Unit))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.InvalidParameterUnit,
                    field + ".unit"
                );
            }

            switch (parameter.ValueType)
            {
                case SemanticParameterValueType.Unspecified:
                    if (parameter.IsSpecified)
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .ParameterSpecificationMismatch,
                            field + ".isSpecified"
                        );
                    }

                    if (!emptyString ||
                        parameter.IntValue != 0 ||
                        parameter.FloatValue != 0f ||
                        parameter.BoolValue ||
                        !emptyEnum)
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .ParameterValueSlotMismatch,
                            field
                        );
                    }
                    break;

                case SemanticParameterValueType.String:
                    RequireSpecified(parameter, field, issues);
                    if (emptyString)
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .MissingParameterValue,
                            field + ".stringValue"
                        );
                    }
                    if (parameter.IntValue != 0 ||
                        parameter.FloatValue != 0f ||
                        parameter.BoolValue ||
                        !emptyEnum)
                    {
                        AddParameterSlotMismatch(field, issues);
                    }
                    break;

                case SemanticParameterValueType.Integer:
                    RequireSpecified(parameter, field, issues);
                    if (!emptyString ||
                        parameter.FloatValue != 0f ||
                        parameter.BoolValue ||
                        !emptyEnum)
                    {
                        AddParameterSlotMismatch(field, issues);
                    }
                    break;

                case SemanticParameterValueType.Float:
                    RequireSpecified(parameter, field, issues);
                    if (float.IsNaN(parameter.FloatValue) ||
                        float.IsInfinity(parameter.FloatValue))
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .NonFiniteParameterValue,
                            field + ".floatValue"
                        );
                    }
                    if (!emptyString ||
                        parameter.IntValue != 0 ||
                        parameter.BoolValue ||
                        !emptyEnum)
                    {
                        AddParameterSlotMismatch(field, issues);
                    }
                    break;

                case SemanticParameterValueType.Boolean:
                    RequireSpecified(parameter, field, issues);
                    if (!emptyString ||
                        parameter.IntValue != 0 ||
                        parameter.FloatValue != 0f ||
                        !emptyEnum)
                    {
                        AddParameterSlotMismatch(field, issues);
                    }
                    break;

                case SemanticParameterValueType.Enum:
                    RequireSpecified(parameter, field, issues);
                    if (emptyEnum)
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .MissingParameterValue,
                            field + ".enumValue"
                        );
                    }
                    if (!emptyString ||
                        parameter.IntValue != 0 ||
                        parameter.FloatValue != 0f ||
                        parameter.BoolValue)
                    {
                        AddParameterSlotMismatch(field, issues);
                    }
                    break;
            }
        }

        private static void ValidateResolution(
            SemanticIrCandidate candidate,
            ValidationIssues issues)
        {
            switch (candidate.ResolutionStatus)
            {
                case SemanticResolutionStatus.Resolved:
                    if (candidate.RequiresConfirmation)
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .ConfirmationFlagMismatch,
                            "requiresConfirmation"
                        );
                    }

                    if (candidate.UnresolvedFields.Count > 0 ||
                        !string.IsNullOrWhiteSpace(
                            candidate.AmbiguityReason))
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .UnexpectedUnresolvedDetail,
                            "unresolvedFields"
                        );
                    }
                    break;

                case SemanticResolutionStatus.PartiallyResolved:
                    issues.RequireClarification(
                        CglSemanticValidationFailureCodes
                            .InvalidResolutionState,
                        "resolutionStatus"
                    );
                    if (candidate.UnresolvedFields.Count == 0 &&
                        string.IsNullOrWhiteSpace(candidate.FailureCode))
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .MissingUnresolvedDetail,
                            "unresolvedFields"
                        );
                    }
                    break;

                case SemanticResolutionStatus.Ambiguous:
                    if (!candidate.RequiresConfirmation)
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .ConfirmationFlagMismatch,
                            "requiresConfirmation"
                        );
                    }
                    if (string.IsNullOrWhiteSpace(
                            candidate.AmbiguityReason))
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .MissingAmbiguityReason,
                            "ambiguityReason"
                        );
                    }
                    if (!issues.HasFailure)
                    {
                        issues.RequireClarification(
                            CglSemanticValidationFailureCodes
                                .InvalidResolutionState,
                            "resolutionStatus"
                        );
                    }
                    break;

                case SemanticResolutionStatus.Unresolved:
                    if (IsExecutableLooking(candidate))
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .UnresolvedExecutableCandidate,
                            "resolutionStatus"
                        );
                    }
                    else
                    {
                        issues.RequireClarification(
                            CglSemanticValidationFailureCodes
                                .InvalidResolutionState,
                            "resolutionStatus"
                        );
                    }

                    if (candidate.UnresolvedFields.Count == 0 &&
                        string.IsNullOrWhiteSpace(candidate.FailureCode))
                    {
                        issues.AddFailure(
                            CglSemanticValidationFailureCodes
                                .MissingUnresolvedDetail,
                            "unresolvedFields"
                        );
                    }
                    break;

                case SemanticResolutionStatus.Invalid:
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes
                            .InvalidResolutionState,
                        "resolutionStatus"
                    );
                    break;

                case SemanticResolutionStatus.Rejected:
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes
                            .RejectedCandidate,
                        "resolutionStatus"
                    );
                    break;

                case SemanticResolutionStatus.Unknown:
                default:
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes
                            .InvalidResolutionState,
                        "resolutionStatus"
                    );
                    break;
            }
        }

        private static void ValidateSetStructure(
            SemanticCandidateSet candidateSet,
            ValidationIssues issues)
        {
            if (candidateSet == null)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.NullCandidateSet,
                    "candidateSet"
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(candidateSet.CandidateSetId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .MissingCandidateSetId,
                    "candidateSetId"
                );
            }

            if (string.IsNullOrWhiteSpace(candidateSet.InputId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.MissingInputId,
                    "inputId"
                );
            }

            if (string.IsNullOrWhiteSpace(candidateSet.InteractionId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .MissingInteractionId,
                    "interactionId"
                );
            }

            if (string.IsNullOrWhiteSpace(candidateSet.AnalysisId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.MissingAnalysisId,
                    "analysisId"
                );
            }

            if (!IsDefined(candidateSet.SetStatus) ||
                candidateSet.SetStatus ==
                    SemanticCandidateSetStatus.Unknown)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .InvalidCandidateSetStatus,
                    "setStatus"
                );
            }

            if (candidateSet.Candidates.Count == 0)
            {
                if (candidateSet.SetStatus !=
                    SemanticCandidateSetStatus.Empty)
                {
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes
                            .CandidateCountStatusMismatch,
                        "setStatus"
                    );
                }

                return;
            }

            if (candidateSet.SetStatus ==
                SemanticCandidateSetStatus.Empty)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .EmptySetStatusMismatch,
                    "setStatus"
                );
            }

            HashSet<string> candidateIds =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> semanticIrIds =
                new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < candidateSet.Candidates.Count; i++)
            {
                SemanticIrCandidate candidate =
                    candidateSet.Candidates[i];
                string field = "candidates[" + i + "]";

                if (candidate == null)
                {
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes.NullCandidate,
                        field
                    );
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(candidate.CandidateId) &&
                    !candidateIds.Add(candidate.CandidateId))
                {
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes
                            .DuplicateCandidateId,
                        field + ".candidateId"
                    );
                }

                if (!string.IsNullOrWhiteSpace(candidate.SemanticIrId) &&
                    !semanticIrIds.Add(candidate.SemanticIrId))
                {
                    issues.AddFailure(
                        CglSemanticValidationFailureCodes
                            .DuplicateSemanticIrId,
                        field + ".semanticIrId"
                    );
                }

                AddSetIdentityMismatch(
                    candidateSet,
                    candidate,
                    field,
                    issues
                );
            }
        }

        private static void AddSetIdentityMismatch(
            SemanticCandidateSet candidateSet,
            SemanticIrCandidate candidate,
            string field,
            ValidationIssues issues)
        {
            if (!Same(
                    candidate.CandidateSetId,
                    candidateSet.CandidateSetId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .CandidateSetIdMismatch,
                    field + ".candidateSetId"
                );
            }

            if (!Same(candidate.InputId, candidateSet.InputId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.InputIdMismatch,
                    field + ".inputId"
                );
            }

            if (!Same(
                    candidate.InteractionId,
                    candidateSet.InteractionId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .InteractionIdMismatch,
                    field + ".interactionId"
                );
            }

            if (!Same(candidate.AnalysisId, candidateSet.AnalysisId))
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes.AnalysisIdMismatch,
                    field + ".analysisId"
                );
            }
        }

        private static SemanticValidationRecord CreateRecord(
            SemanticCandidateSet candidateSet,
            SemanticIrCandidate candidate,
            CglSemanticValidationContext context,
            ValidationIssues issues)
        {
            SemanticValidationStatus status =
                issues.HasFailure
                    ? SemanticValidationStatus.Invalid
                    : issues.NeedsClarification
                        ? SemanticValidationStatus.NeedsClarification
                        : SemanticValidationStatus.Valid;

            return new SemanticValidationRecord(
                context.ValidationId,
                candidate != null
                    ? candidate.CandidateId
                    : string.Empty,
                candidateSet != null
                    ? candidateSet.CandidateSetId
                    : string.Empty,
                status,
                context.CheckedAtUtc,
                issues.FailureCodes,
                issues.InvalidFields,
                status ==
                    SemanticValidationStatus.NeedsClarification,
                BuildDiagnostic(
                    issues,
                    status == SemanticValidationStatus.Valid
                        ? "Semantic candidate is structurally valid."
                        : "Semantic candidate failed structural validation."
                )
            );
        }

        private static SemanticValidationStatus ResolveSetStatus(
            ValidationIssues setIssues,
            IReadOnlyList<SemanticValidationRecord> records)
        {
            if (setIssues.HasFailure ||
                ContainsStatus(
                    records,
                    SemanticValidationStatus.Invalid) ||
                ContainsStatus(
                    records,
                    SemanticValidationStatus.InternalError))
            {
                return SemanticValidationStatus.Invalid;
            }

            if (setIssues.NeedsClarification ||
                ContainsStatus(
                    records,
                    SemanticValidationStatus.NeedsClarification))
            {
                return SemanticValidationStatus.NeedsClarification;
            }

            return SemanticValidationStatus.Valid;
        }

        private static bool ContainsStatus(
            IReadOnlyList<SemanticValidationRecord> records,
            SemanticValidationStatus status)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] != null &&
                    records[i].Status == status)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetExpectedMessageType(
            SemanticRequestType requestType,
            out SemanticMessageType messageType)
        {
            switch (requestType)
            {
                case SemanticRequestType.Activation:
                case SemanticRequestType.Conversation:
                    messageType = SemanticMessageType.ConversationInput;
                    return true;

                case SemanticRequestType.Action:
                    messageType = SemanticMessageType.SemanticCommand;
                    return true;

                case SemanticRequestType.Stop:
                case SemanticRequestType.Emergency:
                    messageType = SemanticMessageType.SystemControl;
                    return true;

                default:
                    messageType = SemanticMessageType.Unknown;
                    return false;
            }
        }

        private static bool IsActionAllowedForRequest(
            SemanticRequestType requestType,
            SemanticAction action)
        {
            switch (requestType)
            {
                case SemanticRequestType.Activation:
                    return action == SemanticAction.None;

                case SemanticRequestType.Conversation:
                    return action == SemanticAction.Conversation;

                case SemanticRequestType.Action:
                    return action == SemanticAction.LookTarget ||
                           action == SemanticAction.LookDirection ||
                           action == SemanticAction.TurnHead ||
                           action == SemanticAction.TurnUpperBody ||
                           action == SemanticAction.TurnBody ||
                           action == SemanticAction.RotatePlatform ||
                           action == SemanticAction.MoveDirection ||
                           action == SemanticAction.PointTarget;

                case SemanticRequestType.Stop:
                    return action == SemanticAction.Stop;

                case SemanticRequestType.Emergency:
                    return action == SemanticAction.EmergencyStop;

                default:
                    return false;
            }
        }

        private static void AddMissingRequirement(
            SemanticIrCandidate candidate,
            ValidationIssues issues,
            string code,
            string field)
        {
            if (candidate.ResolutionStatus ==
                    SemanticResolutionStatus.PartiallyResolved ||
                candidate.ResolutionStatus ==
                    SemanticResolutionStatus.Ambiguous)
            {
                issues.RequireClarification(code, field);
                return;
            }

            issues.AddFailure(code, field);
        }

        private static void RequireSpecified(
            SemanticParameter parameter,
            string field,
            ValidationIssues issues)
        {
            if (!parameter.IsSpecified)
            {
                issues.AddFailure(
                    CglSemanticValidationFailureCodes
                        .ParameterSpecificationMismatch,
                    field + ".isSpecified"
                );
            }
        }

        private static void AddParameterSlotMismatch(
            string field,
            ValidationIssues issues)
        {
            issues.AddFailure(
                CglSemanticValidationFailureCodes
                    .ParameterValueSlotMismatch,
                field
            );
        }

        private static bool IsExecutableLooking(
            SemanticIrCandidate candidate)
        {
            return candidate.Action != SemanticAction.Unknown &&
                   candidate.Action != SemanticAction.None;
        }

        private static bool HasConcreteDirection(
            SemanticDirection direction)
        {
            return direction == SemanticDirection.Left ||
                   direction == SemanticDirection.Right ||
                   direction == SemanticDirection.Forward ||
                   direction == SemanticDirection.Backward ||
                   direction == SemanticDirection.Up ||
                   direction == SemanticDirection.Down;
        }

        private static bool IsEmptyDirection(
            SemanticDirection direction)
        {
            return direction == SemanticDirection.None ||
                   direction == SemanticDirection.Unspecified;
        }

        private static bool IsEmptyTarget(SemanticTargetRef targetRef)
        {
            return targetRef == SemanticTargetRef.None ||
                   targetRef == SemanticTargetRef.Unspecified;
        }

        private static bool IsDirectionOnlyOrEmpty(
            SemanticTargetRef targetRef)
        {
            return targetRef == SemanticTargetRef.DirectionOnly ||
                   IsEmptyTarget(targetRef);
        }

        private static bool HasSemanticTarget(
            SemanticTargetRef targetRef)
        {
            return targetRef == SemanticTargetRef.CurrentAttentionTarget ||
                   targetRef == SemanticTargetRef.PreviousAttentionTarget ||
                   targetRef ==
                       SemanticTargetRef.CurrentObservationTarget ||
                   targetRef == SemanticTargetRef.Speaker ||
                   targetRef == SemanticTargetRef.Self ||
                   targetRef == SemanticTargetRef.This ||
                   targetRef == SemanticTargetRef.That;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(
                left ?? string.Empty,
                right ?? string.Empty,
                StringComparison.Ordinal
            );
        }

        private static bool IsDefined<T>(T value)
            where T : struct
        {
            return Enum.IsDefined(typeof(T), value);
        }

        private static string BuildDiagnostic(
            ValidationIssues issues,
            string fallback)
        {
            if (issues.FailureCodes.Count == 0)
                return fallback;

            return string.Join(", ", issues.FailureCodes);
        }

        private sealed class ValidationIssues
        {
            private readonly List<string> failureCodes =
                new List<string>();
            private readonly List<string> invalidFields =
                new List<string>();
            private readonly HashSet<string> failureCodeSet =
                new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> invalidFieldSet =
                new HashSet<string>(StringComparer.Ordinal);

            public IReadOnlyList<string> FailureCodes => failureCodes;
            public IReadOnlyList<string> InvalidFields => invalidFields;
            public bool HasFailure { get; private set; }
            public bool NeedsClarification { get; private set; }

            public void AddFailure(string code, string field)
            {
                HasFailure = true;
                Add(code, field);
            }

            public void RequireClarification(
                string code,
                string field)
            {
                NeedsClarification = true;
                Add(code, field);
            }

            private void Add(string code, string field)
            {
                if (!string.IsNullOrWhiteSpace(code) &&
                    failureCodeSet.Add(code))
                {
                    failureCodes.Add(code);
                }

                if (!string.IsNullOrWhiteSpace(field) &&
                    invalidFieldSet.Add(field))
                {
                    invalidFields.Add(field);
                }
            }
        }
    }
}
