// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Core.Language.Contracts;

namespace SalieriAI.Core.Language.Compiler.Compatibility
{
    /// <summary>
    /// Explicit provenance and ID context for one compatibility conversion.
    /// Reusing this immutable context reproduces the same output IDs. Creating
    /// a new context represents a new conversion snapshot and creates new IDs.
    /// No persistent registry or mutable static state is used.
    /// </summary>
    public sealed class CglInterpretationSemanticAdapterContext
    {
        public string CandidateSetId { get; }
        public string SemanticIrId { get; }
        public string CandidateId { get; }
        public DateTime GeneratedAtUtc { get; }
        public SemanticParserSource ParserSource { get; }

        public CglInterpretationSemanticAdapterContext(
            string candidateSetId,
            string semanticIrId,
            string candidateId,
            DateTime generatedAtUtc,
            SemanticParserSource parserSource)
        {
            CandidateSetId = NormalizeId(
                candidateSetId,
                nameof(candidateSetId)
            );
            SemanticIrId = NormalizeId(
                semanticIrId,
                nameof(semanticIrId)
            );
            CandidateId = NormalizeId(candidateId, nameof(candidateId));
            GeneratedAtUtc = NormalizeUtc(generatedAtUtc);
            ParserSource = parserSource;
        }

        public static CglInterpretationSemanticAdapterContext
            CreateForDeterministicPattern()
        {
            return new CglInterpretationSemanticAdapterContext(
                CommunicationIdGenerator.Create("candidate_set"),
                CommunicationIdGenerator.Create("semantic_ir"),
                CommunicationIdGenerator.Create("candidate"),
                DateTime.UtcNow,
                SemanticParserSource.DeterministicPattern
            );
        }

        private static string NormalizeId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Compatibility conversion ID must not be empty.",
                    parameterName
                );

            return value.Trim();
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            DateTime safe = value == default(DateTime)
                ? DateTime.UtcNow
                : value;

            if (safe.Kind == DateTimeKind.Utc)
                return safe;

            if (safe.Kind == DateTimeKind.Local)
                return safe.ToUniversalTime();

            return DateTime.SpecifyKind(safe, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Side-effect-free Phase 3B-2A projection from the Phase 2
    /// CglInterpretation contract to a formal SemanticCandidateSet.
    ///
    /// This adapter does not parse additional meaning, perform grounding,
    /// resolve a Skill or Body command, authorize execution, or invoke any
    /// runtime route. CommunicationInput remains the authoritative source for
    /// original input provenance; CglInterpretation remains the authoritative
    /// Phase 2 analysis result.
    /// </summary>
    public static class CglInterpretationSemanticAdapter
    {
        private const int SchemaMajor = 0;
        private const int SchemaMinor = 1;

        /// <summary>
        /// Convenience entry point for the current DeterministicCglParser.
        /// Each call creates a new conversion snapshot and therefore new SCC
        /// IDs. Use the context overload when the caller must control IDs.
        /// </summary>
        public static SemanticCandidateSet Convert(
            CommunicationInput input,
            CglInterpretation interpretation)
        {
            return Convert(
                input,
                interpretation,
                CglInterpretationSemanticAdapterContext
                    .CreateForDeterministicPattern()
            );
        }

        public static SemanticCandidateSet Convert(
            CommunicationInput input,
            CglInterpretation interpretation,
            CglInterpretationSemanticAdapterContext context)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (interpretation == null)
                throw new ArgumentNullException(nameof(interpretation));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            EnsureSameInputIdentity(input, interpretation);

            SemanticRequestType requestType =
                ConvertRequestType(interpretation.RequestType);
            SemanticAction action = ConvertAction(
                requestType,
                interpretation.ActionCandidate
            );
            SemanticDirection direction =
                ConvertDirection(interpretation.DirectionCandidate);
            SemanticResolutionStatus resolutionStatus =
                ConvertResolutionStatus(interpretation);

            bool canCreateCandidate =
                CanCreateCandidate(requestType, action);

            if (!canCreateCandidate)
            {
                return new SemanticCandidateSet(
                    context.CandidateSetId,
                    input.InputId,
                    input.InteractionId,
                    interpretation.AnalysisId,
                    context.GeneratedAtUtc,
                    null,
                    SemanticCandidateSetStatus.Empty,
                    BuildEmptySetDiagnostic(
                        interpretation,
                        requestType,
                        action
                    )
                );
            }

            bool requiresConfirmation =
                resolutionStatus == SemanticResolutionStatus.Ambiguous;

            SemanticIrCandidate candidate = new SemanticIrCandidate(
                context.SemanticIrId,
                context.CandidateId,
                context.CandidateSetId,
                input.InputId,
                input.InteractionId,
                input.InteractionId,
                interpretation.AnalysisId,
                input.LifecycleGeneration,
                SchemaMajor,
                SchemaMinor,
                context.GeneratedAtUtc,
                input.TimestampUtc,
                input.NormalizedText,
                context.ParserSource,
                interpretation.ParserVersion,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                ConvertMessageType(requestType),
                requestType,
                action,
                ConvertTargetRef(action, direction),
                direction,
                new SemanticParameter[0],
                resolutionStatus,
                requiresConfirmation,
                requiresConfirmation
                    ? "Compatibility source marks the interpretation " +
                      "ambiguous but provides no ambiguity reason."
                    : string.Empty,
                new string[0],
                string.Empty,
                string.Empty,
                0
            );

            return new SemanticCandidateSet(
                context.CandidateSetId,
                input.InputId,
                input.InteractionId,
                interpretation.AnalysisId,
                context.GeneratedAtUtc,
                new[] { candidate },
                requiresConfirmation
                    ? SemanticCandidateSetStatus.Ambiguous
                    : SemanticCandidateSetStatus.Generated,
                string.Empty
            );
        }

        private static void EnsureSameInputIdentity(
            CommunicationInput input,
            CglInterpretation interpretation)
        {
            if (!string.Equals(
                    input.InputId,
                    interpretation.InputId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "CommunicationInput and CglInterpretation inputId differ.",
                    nameof(interpretation)
                );
            }

            if (!string.Equals(
                    input.InteractionId,
                    interpretation.InteractionId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "CommunicationInput and CglInterpretation interactionId " +
                    "differ.",
                    nameof(interpretation)
                );
            }
        }

        private static SemanticRequestType ConvertRequestType(string value)
        {
            switch (NormalizeSymbol(value))
            {
                case "ACTIVATION":
                    return SemanticRequestType.Activation;
                case "CONVERSATION":
                    return SemanticRequestType.Conversation;
                case "ACTION":
                    return SemanticRequestType.Action;
                case "STOP":
                    return SemanticRequestType.Stop;
                case "EMERGENCY":
                    return SemanticRequestType.Emergency;
                case "UNKNOWN":
                default:
                    return SemanticRequestType.Unknown;
            }
        }

        private static SemanticAction ConvertAction(
            SemanticRequestType requestType,
            string value)
        {
            switch (NormalizeSymbol(value))
            {
                case "LOOK_DIRECTION":
                    return SemanticAction.LookDirection;
                case "MOVE":
                    return SemanticAction.MoveDirection;
                case "STOP":
                    return SemanticAction.Stop;
                case "EMERGENCY":
                    return SemanticAction.EmergencyStop;
                case "CONVERSATION":
                    return SemanticAction.Conversation;
            }

            switch (requestType)
            {
                case SemanticRequestType.Activation:
                    return SemanticAction.None;
                case SemanticRequestType.Conversation:
                    return SemanticAction.Conversation;
                case SemanticRequestType.Stop:
                    return SemanticAction.Stop;
                case SemanticRequestType.Emergency:
                    return SemanticAction.EmergencyStop;
                default:
                    return SemanticAction.Unknown;
            }
        }

        private static SemanticDirection ConvertDirection(string value)
        {
            switch (NormalizeSymbol(value))
            {
                case "RIGHT":
                    return SemanticDirection.Right;
                case "LEFT":
                    return SemanticDirection.Left;
                case "FORWARD":
                    return SemanticDirection.Forward;
                case "BACKWARD":
                    return SemanticDirection.Backward;
                case "":
                case "NONE":
                    return SemanticDirection.Unspecified;
                default:
                    return SemanticDirection.Unknown;
            }
        }

        private static SemanticResolutionStatus ConvertResolutionStatus(
            CglInterpretation interpretation)
        {
            if (interpretation.Ambiguous)
                return SemanticResolutionStatus.Ambiguous;

            if (interpretation.Unknown || interpretation.Unresolved)
                return SemanticResolutionStatus.Unresolved;

            switch (NormalizeSymbol(interpretation.InterpretationStatus))
            {
                case "RESOLVED":
                    return SemanticResolutionStatus.Resolved;
                case "PARTIALLY_RESOLVED":
                    return SemanticResolutionStatus.PartiallyResolved;
                case "AMBIGUOUS":
                    return SemanticResolutionStatus.Ambiguous;
                case "UNRESOLVED":
                    return SemanticResolutionStatus.Unresolved;
                case "INVALID":
                    return SemanticResolutionStatus.Invalid;
                case "REJECTED":
                    return SemanticResolutionStatus.Rejected;
                default:
                    return SemanticResolutionStatus.Unknown;
            }
        }

        private static SemanticMessageType ConvertMessageType(
            SemanticRequestType requestType)
        {
            switch (requestType)
            {
                case SemanticRequestType.Activation:
                case SemanticRequestType.Conversation:
                    return SemanticMessageType.ConversationInput;
                case SemanticRequestType.Action:
                    return SemanticMessageType.SemanticCommand;
                case SemanticRequestType.Stop:
                case SemanticRequestType.Emergency:
                    return SemanticMessageType.SystemControl;
                default:
                    return SemanticMessageType.Unknown;
            }
        }

        private static SemanticTargetRef ConvertTargetRef(
            SemanticAction action,
            SemanticDirection direction)
        {
            bool isDirectionAction =
                action == SemanticAction.LookDirection ||
                action == SemanticAction.MoveDirection;
            bool hasDirection =
                direction != SemanticDirection.Unknown &&
                direction != SemanticDirection.None &&
                direction != SemanticDirection.Unspecified;

            return isDirectionAction && hasDirection
                ? SemanticTargetRef.DirectionOnly
                : SemanticTargetRef.Unspecified;
        }

        private static bool CanCreateCandidate(
            SemanticRequestType requestType,
            SemanticAction action)
        {
            if (requestType == SemanticRequestType.Unknown)
                return false;

            if (requestType == SemanticRequestType.Action)
            {
                return action != SemanticAction.Unknown &&
                       action != SemanticAction.None;
            }

            return true;
        }

        private static string BuildEmptySetDiagnostic(
            CglInterpretation interpretation,
            SemanticRequestType requestType,
            SemanticAction action)
        {
            if (requestType == SemanticRequestType.Unknown)
            {
                return "Compatibility conversion produced no candidate " +
                       "because the source request type is unknown or " +
                       "unsupported.";
            }

            if (requestType == SemanticRequestType.Action &&
                (action == SemanticAction.Unknown ||
                 action == SemanticAction.None))
            {
                return "Compatibility conversion produced no candidate " +
                       "because the source action is unknown or unsupported.";
            }

            return "Compatibility conversion produced no candidate for " +
                   "source status " +
                   NormalizeSymbol(interpretation.InterpretationStatus) +
                   ".";
        }

        private static string NormalizeSymbol(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }
    }
}
