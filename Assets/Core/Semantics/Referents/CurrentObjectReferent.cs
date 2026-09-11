// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Semantics.Referents
{
    public enum ObjectReferentMemoryStatus
    {
        None = 0,
        Known = 1,
        NoMatch = 2,
        Ambiguous = 3,
        NoKey = 4,
        Unsupported = 5,
        StoreError = 6
    }

    /// <summary>
    /// Immutable semantic snapshot of the currently observed primary object.
    /// ReferentId and target provenance are runtime correlation, never a
    /// persistent Entity identity.
    /// </summary>
    public sealed class CurrentObjectReferent
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

        internal CurrentObjectReferent(
            string referentId,
            string sessionId,
            long sourceFrameId,
            string targetKey,
            int trackId,
            int detectorClassId,
            string detectorClassLabel,
            ObjectReferentMemoryStatus memoryStatus,
            string recallKey,
            string knownName,
            string experienceRecordId,
            DateTime activatedAtUtc,
            DateTime updatedAtUtc,
            string diagnosticError)
        {
            ReferentId = Text(referentId);
            SessionId = Text(sessionId);
            SourceFrameId = sourceFrameId;
            TargetKey = Text(targetKey);
            TrackId = trackId;
            DetectorClassId = detectorClassId;
            DetectorClassLabel = Text(detectorClassLabel);
            MemoryStatus = memoryStatus;
            RecallKey = Text(recallKey);

            if (memoryStatus == ObjectReferentMemoryStatus.Known)
            {
                KnownName = Text(knownName);
                ExperienceRecordId = Text(experienceRecordId);
            }
            else
            {
                KnownName = string.Empty;
                ExperienceRecordId = string.Empty;
            }

            ActivatedAtUtc = Utc(activatedAtUtc);
            UpdatedAtUtc = Utc(updatedAtUtc);
            DiagnosticError = diagnosticError ?? string.Empty;
        }

        internal bool HasSameRuntimeTargetAs(CurrentObjectReferent other)
        {
            return other != null &&
                   TrackId == other.TrackId &&
                   Same(SessionId, other.SessionId) &&
                   Same(TargetKey, other.TargetKey);
        }

        internal bool IsSemanticallyEquivalentTo(CurrentObjectReferent other)
        {
            return other != null &&
                   SourceFrameId == other.SourceFrameId &&
                   TrackId == other.TrackId &&
                   DetectorClassId == other.DetectorClassId &&
                   MemoryStatus == other.MemoryStatus &&
                   Same(SessionId, other.SessionId) &&
                   Same(TargetKey, other.TargetKey) &&
                   Same(DetectorClassLabel, other.DetectorClassLabel) &&
                   Same(RecallKey, other.RecallKey) &&
                   Same(KnownName, other.KnownName) &&
                   Same(ExperienceRecordId, other.ExperienceRecordId) &&
                   Same(DiagnosticError, other.DiagnosticError);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
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
