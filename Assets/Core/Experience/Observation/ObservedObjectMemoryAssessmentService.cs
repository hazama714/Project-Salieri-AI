// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Experience.Recall;
using SalieriAI.Core.Experience.Storage;
using SalieriAI.Core.Perception.ObjectTargeting;
using SalieriAI.Core.Perception.VisualSnapshots;

namespace SalieriAI.Core.Experience.Observation
{
    /// <summary>
    /// Immutable copy of the target facts required by memory assessment.
    /// </summary>
    public sealed class ObservedObjectMemoryAssessmentRequest
    {
        public string SessionId { get; }
        public long SourceFrameId { get; }
        public string TargetKey { get; }
        public int TrackId { get; }
        public int DetectorClassId { get; }
        public string DetectorClassLabel { get; }
        public float NormalizedX1 { get; }
        public float NormalizedY1 { get; }
        public float NormalizedX2 { get; }
        public float NormalizedY2 { get; }

        private ObservedObjectMemoryAssessmentRequest(
            string sessionId,
            long sourceFrameId,
            string targetKey,
            int trackId,
            int detectorClassId,
            string detectorClassLabel,
            float normalizedX1,
            float normalizedY1,
            float normalizedX2,
            float normalizedY2)
        {
            SessionId = Text(sessionId);
            SourceFrameId = sourceFrameId;
            TargetKey = Text(targetKey);
            TrackId = trackId;
            DetectorClassId = detectorClassId;
            DetectorClassLabel = Text(detectorClassLabel);
            NormalizedX1 = normalizedX1;
            NormalizedY1 = normalizedY1;
            NormalizedX2 = normalizedX2;
            NormalizedY2 = normalizedY2;
        }

        public static ObservedObjectMemoryAssessmentRequest FromTarget(
            ObservationTarget target)
        {
            if (target == null)
                return null;

            return new ObservedObjectMemoryAssessmentRequest(
                target.SessionId,
                target.SourceFrameId,
                target.TargetKey,
                target.TrackId,
                target.ClassId,
                target.ClassLabel,
                target.TrackSnapshot != null
                    ? target.TrackSnapshot.NormalizedX1
                    : 0f,
                target.TrackSnapshot != null
                    ? target.TrackSnapshot.NormalizedY1
                    : 0f,
                target.TrackSnapshot != null
                    ? target.TrackSnapshot.NormalizedX2
                    : 0f,
                target.TrackSnapshot != null
                    ? target.TrackSnapshot.NormalizedY2
                    : 0f);
        }

        public bool Matches(ObservationTarget target)
        {
            return target != null &&
                SourceFrameId == target.SourceFrameId &&
                string.Equals(SessionId, target.SessionId, StringComparison.Ordinal) &&
                string.Equals(TargetKey, target.TargetKey, StringComparison.Ordinal);
        }

        public VisualNormalizedBoundingBox CreateBoundingBox()
        {
            return new VisualNormalizedBoundingBox(
                NormalizedX1,
                NormalizedY1,
                NormalizedX2,
                NormalizedY2);
        }

        private static string Text(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }

    /// <summary>
    /// Read-only RK0 + ExperienceRecallResolver assessment.
    /// It does not write Experience or initiate any runtime behavior.
    /// </summary>
    public sealed class ObservedObjectMemoryAssessmentService
    {
        private readonly IExperienceStore store;

        public ObservedObjectMemoryAssessmentService(IExperienceStore store)
        {
            this.store = store;
        }

        public ObservedObjectMemoryContext Evaluate(
            ObservedObjectMemoryAssessmentRequest request,
            VisualCropSnapshot crop,
            DateTime evaluatedAtUtc,
            string visualEvidenceError = "")
        {
            if (request == null)
                return null;

            if (crop == null || !crop.IsValid ||
                !string.Equals(
                    crop.SessionId,
                    request.SessionId,
                    StringComparison.Ordinal) ||
                crop.SourceFrameId != request.SourceFrameId)
            {
                return CreateContext(
                    request,
                    new ExperienceRecallResult(
                        RecallStatus.NoKey,
                        string.Empty,
                        string.Empty,
                        null,
                        ErrorOrDefault(
                            visualEvidenceError,
                            "Exact VisualCropSnapshot is unavailable.")),
                    evaluatedAtUtc);
            }

            if (!RecallKeyV0Builder.TryBuild(
                    request.DetectorClassId,
                    crop,
                    out RecallKeyV0 key,
                    out string keyError))
            {
                return CreateContext(
                    request,
                    new ExperienceRecallResult(
                        RecallStatus.NoKey,
                        string.Empty,
                        string.Empty,
                        null,
                        keyError),
                    evaluatedAtUtc);
            }

            ExperienceRecallResult result =
                new ExperienceRecallResolver(store).Resolve(key.Serialized);
            return CreateContext(request, result, evaluatedAtUtc);
        }

        internal static ObservedObjectMemoryContext CreateContext(
            ObservedObjectMemoryAssessmentRequest request,
            ExperienceRecallResult result,
            DateTime evaluatedAtUtc)
        {
            if (request == null)
                return null;

            ExperienceRecallResult safe = result;
            if (safe == null)
            {
                safe = new ExperienceRecallResult(
                    RecallStatus.StoreError,
                    string.Empty,
                    string.Empty,
                    null,
                    "Experience recall result is unavailable.");
            }
            else if (safe.Status == RecallStatus.Known &&
                     (string.IsNullOrWhiteSpace(safe.NormalizedAnswer) ||
                      safe.MatchedRecordIds == null ||
                      safe.MatchedRecordIds.Count == 0))
            {
                safe = new ExperienceRecallResult(
                    RecallStatus.StoreError,
                    safe.RecallKey,
                    string.Empty,
                    safe.MatchedRecordIds,
                    "Known recall result is incomplete.");
            }

            return new ObservedObjectMemoryContext(
                request.SessionId,
                request.SourceFrameId,
                request.TargetKey,
                request.TrackId,
                request.DetectorClassId,
                request.DetectorClassLabel,
                safe.Status,
                safe.RecallKey,
                safe.MatchedRecordIds,
                safe.Status == RecallStatus.Known
                    ? safe.NormalizedAnswer
                    : string.Empty,
                evaluatedAtUtc,
                safe.Error);
        }

        private static string ErrorOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value;
        }
    }
}
