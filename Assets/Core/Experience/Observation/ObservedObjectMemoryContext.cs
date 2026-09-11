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

using SalieriAI.Core.Experience.Recall;

namespace SalieriAI.Core.Experience.Observation
{
    /// <summary>
    /// One read-only memory assessment for one exact observed target frame.
    /// TargetKey/TrackId are runtime provenance, never persistent identity.
    /// </summary>
    public sealed class ObservedObjectMemoryContext
    {
        public string SessionId { get; }
        public long SourceFrameId { get; }
        public string TargetKey { get; }
        public int TrackId { get; }
        public int DetectorClassId { get; }
        public string DetectorClassLabel { get; }

        public RecallStatus RecallStatus { get; }
        public string RecallKey { get; }
        public IReadOnlyList<string> MatchedExperienceRecordIds { get; }
        public string ExperienceRecordId { get; }
        public string KnownName { get; }

        public DateTime EvaluatedAtUtc { get; }
        public string DiagnosticError { get; }
        public bool IsKnown =>
            RecallStatus ==
            SalieriAI.Core.Experience.Recall.RecallStatus.Known;

        internal ObservedObjectMemoryContext(
            string sessionId,
            long sourceFrameId,
            string targetKey,
            int trackId,
            int detectorClassId,
            string detectorClassLabel,
            RecallStatus recallStatus,
            string recallKey,
            IEnumerable<string> matchedExperienceRecordIds,
            string knownName,
            DateTime evaluatedAtUtc,
            string diagnosticError)
        {
            SessionId = Text(sessionId);
            SourceFrameId = sourceFrameId;
            TargetKey = Text(targetKey);
            TrackId = trackId;
            DetectorClassId = detectorClassId;
            DetectorClassLabel = Text(detectorClassLabel);
            RecallStatus = recallStatus;
            RecallKey = Text(recallKey);

            var ids = new List<string>();
            if (matchedExperienceRecordIds != null)
            {
                foreach (string value in matchedExperienceRecordIds)
                {
                    string id = Text(value);
                    if (id.Length > 0)
                        ids.Add(id);
                }
            }
            MatchedExperienceRecordIds =
                new ReadOnlyCollection<string>(ids);

            if (recallStatus == RecallStatus.Known)
            {
                KnownName = Text(knownName);
                ExperienceRecordId = ids.Count > 0
                    ? ids[0]
                    : string.Empty;
            }
            else
            {
                KnownName = string.Empty;
                ExperienceRecordId = string.Empty;
            }

            EvaluatedAtUtc = Utc(evaluatedAtUtc);
            DiagnosticError = diagnosticError ?? string.Empty;
        }

        internal bool IsSemanticallyEquivalentTo(
            ObservedObjectMemoryContext other)
        {
            if (other == null ||
                SourceFrameId != other.SourceFrameId ||
                TrackId != other.TrackId ||
                DetectorClassId != other.DetectorClassId ||
                RecallStatus != other.RecallStatus ||
                !Same(SessionId, other.SessionId) ||
                !Same(TargetKey, other.TargetKey) ||
                !Same(DetectorClassLabel, other.DetectorClassLabel) ||
                !Same(RecallKey, other.RecallKey) ||
                !Same(ExperienceRecordId, other.ExperienceRecordId) ||
                !Same(KnownName, other.KnownName) ||
                !Same(DiagnosticError, other.DiagnosticError) ||
                MatchedExperienceRecordIds.Count !=
                    other.MatchedExperienceRecordIds.Count)
            {
                return false;
            }

            for (int i = 0; i < MatchedExperienceRecordIds.Count; i++)
            {
                if (!Same(
                        MatchedExperienceRecordIds[i],
                        other.MatchedExperienceRecordIds[i]))
                {
                    return false;
                }
            }
            return true;
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
