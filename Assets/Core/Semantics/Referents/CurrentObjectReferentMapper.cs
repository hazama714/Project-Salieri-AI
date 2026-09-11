// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Experience.Observation;
using SalieriAI.Core.Experience.Recall;

namespace SalieriAI.Core.Semantics.Referents
{
    /// <summary>
    /// Pure semantic projection from the frozen M5-A assessment. It performs
    /// no Recall, persistence, perception query, conversation, or actuation.
    /// </summary>
    public static class CurrentObjectReferentMapper
    {
        public static CurrentObjectReferent Map(
            ObservedObjectMemoryContext source,
            CurrentObjectReferent current,
            string newReferentId,
            DateTime updatedAtUtc)
        {
            if (source == null)
                return null;

            ObjectReferentMemoryStatus status = MapStatus(source.RecallStatus);
            string knownName = source.KnownName;
            string recordId = source.ExperienceRecordId;
            string diagnostic = source.DiagnosticError;

            if (status == ObjectReferentMemoryStatus.Known &&
                (string.IsNullOrWhiteSpace(knownName) ||
                 string.IsNullOrWhiteSpace(recordId)))
            {
                status = ObjectReferentMemoryStatus.StoreError;
                knownName = string.Empty;
                recordId = string.Empty;
                diagnostic =
                    "Known memory assessment lacks a name or experience record.";
            }

            DateTime updated = Utc(updatedAtUtc);
            bool sameActivation = current != null &&
                current.TrackId == source.TrackId &&
                string.Equals(
                    current.SessionId,
                    source.SessionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    current.TargetKey,
                    source.TargetKey,
                    StringComparison.Ordinal);

            return new CurrentObjectReferent(
                sameActivation ? current.ReferentId : newReferentId,
                source.SessionId,
                source.SourceFrameId,
                source.TargetKey,
                source.TrackId,
                source.DetectorClassId,
                source.DetectorClassLabel,
                status,
                source.RecallKey,
                knownName,
                recordId,
                sameActivation ? current.ActivatedAtUtc : updated,
                updated,
                diagnostic);
        }

        private static ObjectReferentMemoryStatus MapStatus(RecallStatus status)
        {
            switch (status)
            {
                case RecallStatus.Known:
                    return ObjectReferentMemoryStatus.Known;
                case RecallStatus.NoMatch:
                    return ObjectReferentMemoryStatus.NoMatch;
                case RecallStatus.Ambiguous:
                    return ObjectReferentMemoryStatus.Ambiguous;
                case RecallStatus.NoKey:
                    return ObjectReferentMemoryStatus.NoKey;
                case RecallStatus.UnsupportedKey:
                    return ObjectReferentMemoryStatus.Unsupported;
                case RecallStatus.StoreError:
                default:
                    return ObjectReferentMemoryStatus.StoreError;
            }
        }

        private static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime) || value.Kind == DateTimeKind.Utc)
                return value;
            return value.ToUniversalTime();
        }
    }
}
