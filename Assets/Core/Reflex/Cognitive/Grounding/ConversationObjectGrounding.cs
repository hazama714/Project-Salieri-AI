// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Language.Compiler;
using SalieriAI.Core.Semantics.Referents;

namespace SalieriAI.Core.Reflex.Cognitive.Grounding
{
    public enum ConversationObjectGroundingStatus
    {
        NoReferent = 0,
        SnapshotAvailable = 1,
        ResolvedThis = 2,
        UnresolvedThis = 3,
        UnsupportedTarget = 4,
        InvalidKnownProvenance = 5,
        ResolvedCurrent = 6,
        ResolvedPrevious = 7,
        MissingCurrent = 8,
        MissingPrevious = 9,
        Unsupported = 10,
        Ambiguous = 11,
        NeedsClarification = 12,
        Unspecified = 13
    }

    /// <summary>
    /// Immutable value snapshot copied from the M5-B semantic referent.
    /// Runtime correlation fields remain provenance and never become a
    /// persistent Entity identity.
    /// </summary>
    public sealed class FrozenObjectReferentSnapshot
    {
        public string ReferentId { get; }
        public string SessionId { get; }
        public long SourceFrameId { get; }
        public string TargetKey { get; }
        public int TrackId { get; }
        public int DetectorClassId { get; }
        public string DetectorClassLabel { get; }
        public ObjectReferentMemoryStatus MemoryStatus { get; }
        public string RecallKey { get; }
        public string KnownName { get; }
        public string ExperienceRecordId { get; }
        public DateTime ActivatedAtUtc { get; }
        public DateTime UpdatedAtUtc { get; }
        public string DiagnosticError { get; }

        public bool IsKnown =>
            MemoryStatus == ObjectReferentMemoryStatus.Known;

        public bool HasUsableKnownIdentity =>
            IsKnown &&
            !string.IsNullOrWhiteSpace(KnownName) &&
            !string.IsNullOrWhiteSpace(ExperienceRecordId);

        internal FrozenObjectReferentSnapshot(CurrentObjectReferent source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ReferentId = Text(source.ReferentId);
            SessionId = Text(source.SessionId);
            SourceFrameId = source.SourceFrameId;
            TargetKey = Text(source.TargetKey);
            TrackId = source.TrackId;
            DetectorClassId = source.DetectorClassId;
            DetectorClassLabel = Text(source.DetectorClassLabel);
            MemoryStatus = source.MemoryStatus;
            RecallKey = Text(source.RecallKey);
            KnownName = source.IsKnown ? Text(source.KnownName) : string.Empty;
            ExperienceRecordId = source.IsKnown
                ? Text(source.ExperienceRecordId)
                : string.Empty;
            ActivatedAtUtc = Utc(source.ActivatedAtUtc);
            UpdatedAtUtc = Utc(source.UpdatedAtUtc);
            DiagnosticError = source.DiagnosticError ?? string.Empty;
        }

        private static string Text(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime) || value.Kind == DateTimeKind.Utc)
                return value;
            return value.ToUniversalTime();
        }
    }

    /// <summary>
    /// Immutable per-turn object grounding. It owns copied values only and
    /// never retains ICurrentObjectReferentProvider or a Unity object.
    /// </summary>
    public sealed class ConversationObjectGrounding
    {
        public string GroundingId { get; }
        public string InputId { get; }
        public string InteractionId { get; }
        public SemanticTargetRef RequestedTargetRef { get; }
        public ConversationObjectGroundingStatus Status { get; }
        public ConversationReferentSource ReferentSource { get; }
        public FrozenObjectReferentSnapshot Referent { get; }
        public DateTime GroundedAtUtc { get; }
        public string Diagnostic { get; }

        public bool HasReferent => Referent != null;
        public bool IsResolved =>
            Status == ConversationObjectGroundingStatus.ResolvedThis ||
            Status == ConversationObjectGroundingStatus.ResolvedCurrent ||
            Status == ConversationObjectGroundingStatus.ResolvedPrevious;

        internal ConversationObjectGrounding(
            string groundingId,
            string inputId,
            string interactionId,
            SemanticTargetRef requestedTargetRef,
            ConversationObjectGroundingStatus status,
            FrozenObjectReferentSnapshot referent,
            DateTime groundedAtUtc,
            string diagnostic)
            : this(
                groundingId,
                inputId,
                interactionId,
                requestedTargetRef,
                status,
                referent != null
                    ? ConversationReferentSource.CurrentObjectReferent
                    : ConversationReferentSource.None,
                referent,
                groundedAtUtc,
                diagnostic)
        {
        }

        internal ConversationObjectGrounding(
            string groundingId,
            string inputId,
            string interactionId,
            SemanticTargetRef requestedTargetRef,
            ConversationObjectGroundingStatus status,
            ConversationReferentSource referentSource,
            FrozenObjectReferentSnapshot referent,
            DateTime groundedAtUtc,
            string diagnostic)
        {
            GroundingId = Text(groundingId);
            InputId = Text(inputId);
            InteractionId = Text(interactionId);
            RequestedTargetRef = requestedTargetRef;
            Status = status;
            ReferentSource = referentSource;
            Referent = referent;
            GroundedAtUtc = Utc(groundedAtUtc);
            Diagnostic = diagnostic ?? string.Empty;
        }

        private static string Text(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime) || value.Kind == DateTimeKind.Utc)
                return value;
            return value.ToUniversalTime();
        }
    }
}
