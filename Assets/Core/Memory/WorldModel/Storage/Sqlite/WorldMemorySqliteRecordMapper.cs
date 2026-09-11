// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Globalization;

using SQLite;

using SalieriAI.Core.Memory.WorldModel.Contracts;

namespace SalieriAI.Core.Memory.WorldModel.Storage.Sqlite
{
    internal static class WorldMemorySqliteRecordMapper
    {
        private const string UtcFormat =
            "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

        public static string Utc(DateTime value)
        {
            return value.ToUniversalTime().ToString(UtcFormat,
                CultureInfo.InvariantCulture);
        }

        public static string Utc(DateTime? value)
        {
            return value.HasValue ? Utc(value.Value) : null;
        }

        public static DateTime ParseUtc(string value)
        {
            return DateTime.ParseExact(value, UtcFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal);
        }

        public static DateTime? ParseNullableUtc(string value)
        {
            return value == null ? (DateTime?)null : ParseUtc(value);
        }

        public static string NullId(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public static string EmptyId(string value)
        {
            return value ?? string.Empty;
        }

        public static WorldMemoryEntity Entity(EntityRow row)
        {
            return new WorldMemoryEntity(row.EntityId,
                (WorldMemoryEntityType)row.EntityType, row.CanonicalName,
                (WorldMemoryEntityStatus)row.Status,
                ParseUtc(row.CreatedAtUtc), ParseUtc(row.UpdatedAtUtc),
                EmptyId(row.MergedIntoEntityId));
        }

        public static WorldMemorySource Source(SourceRow row)
        {
            return new WorldMemorySource(row.SourceId,
                (WorldMemorySourceType)row.SourceType, row.DisplayName,
                row.Uri, row.Publisher, row.ExternalReference,
                row.ComponentId, ParseUtc(row.CreatedAtUtc),
                (WorldMemorySourceStatus)row.Status);
        }

        public static WorldMemoryObservation Observation(ObservationRow row)
        {
            return new WorldMemoryObservation(row.ObservationId,
                (WorldMemoryObservationModality)row.Modality,
                ParseUtc(row.ObservedAtUtc), row.SessionId,
                row.SourceFrameId, row.TargetKey, row.TrackId,
                EmptyId(row.SourceId), ParseUtc(row.CreatedAtUtc));
        }

        public static WorldMemoryRecognitionEvidence Recognition(
            RecognitionRow row)
        {
            return new WorldMemoryRecognitionEvidence(
                row.RecognitionEvidenceId, row.ObservationId,
                EmptyId(row.CandidateEntityId),
                (WorldMemoryRecognitionType)row.RecognitionType,
                row.FeatureReference, row.ModelName, row.ModelVersion,
                row.HasScore != 0, (float)row.Score,
                (WorldMemoryRecognitionEvidenceStatus)row.Status,
                ParseUtc(row.CreatedAtUtc),
                ParseNullableUtc(row.ObservedAtUtc));
        }

        public static WorldMemoryFact Fact(FactRow row)
        {
            return new WorldMemoryFact(row.FactId, row.SubjectEntityId,
                row.Predicate, (WorldMemoryFactValueKind)row.ValueKind,
                EmptyId(row.ObjectEntityId), row.LiteralValue,
                (WorldMemoryLiteralType)row.ValueType,
                (WorldMemoryFactStatus)row.Status,
                ParseUtc(row.CreatedAtUtc), ParseUtc(row.UpdatedAtUtc),
                ParseNullableUtc(row.ValidFromUtc),
                ParseNullableUtc(row.ValidUntilUtc),
                ParseNullableUtc(row.LastVerifiedAtUtc),
                row.FreshnessPolicyId,
                EmptyId(row.SupersededByFactId));
        }

        public static WorldMemoryEvidence Evidence(EvidenceRow row)
        {
            return new WorldMemoryEvidence(row.EvidenceId, row.FactId,
                (WorldMemoryEvidenceType)row.EvidenceType,
                (WorldMemoryEvidenceStance)row.Stance,
                EmptyId(row.SourceId), EmptyId(row.AttributedSourceId),
                row.HasConfidence != 0, (float)row.Confidence,
                ParseUtc(row.CreatedAtUtc),
                ParseNullableUtc(row.ObservedAtUtc),
                ParseNullableUtc(row.AssertedAtUtc),
                ParseNullableUtc(row.RetrievedAtUtc),
                ParseNullableUtc(row.PublishedAtUtc),
                ParseNullableUtc(row.ValidFromUtc),
                ParseNullableUtc(row.ValidUntilUtc),
                ParseNullableUtc(row.LastVerifiedAtUtc),
                EmptyId(row.ObservationId),
                EmptyId(row.RecognitionEvidenceId),
                row.ExperienceRecordId, row.InferenceProcessId,
                (WorldMemoryEvidenceStatus)row.Status,
                row.DiagnosticNote);
        }

        public static WorldMemoryEntityResolution Resolution(
            ResolutionRow row,
            IEnumerable<string> supportIds)
        {
            return new WorldMemoryEntityResolution(row.ResolutionId,
                row.ObservationId, EmptyId(row.EntityId),
                (WorldMemoryResolutionStatus)row.ResolutionStatus,
                (WorldMemoryResolutionMethod)row.ResolutionMethod,
                ParseUtc(row.CreatedAtUtc), row.HasConfidence != 0,
                (float)row.Confidence, supportIds);
        }

        internal sealed class EntityRow
        {
            [Column("entity_id")] public string EntityId { get; set; }
            [Column("entity_type")] public int EntityType { get; set; }
            [Column("canonical_name")] public string CanonicalName { get; set; }
            [Column("status")] public int Status { get; set; }
            [Column("created_at_utc")] public string CreatedAtUtc { get; set; }
            [Column("updated_at_utc")] public string UpdatedAtUtc { get; set; }
            [Column("merged_into_entity_id")] public string MergedIntoEntityId { get; set; }
        }

        internal sealed class SourceRow
        {
            [Column("source_id")] public string SourceId { get; set; }
            [Column("source_type")] public int SourceType { get; set; }
            [Column("display_name")] public string DisplayName { get; set; }
            [Column("uri")] public string Uri { get; set; }
            [Column("publisher")] public string Publisher { get; set; }
            [Column("external_reference")] public string ExternalReference { get; set; }
            [Column("component_id")] public string ComponentId { get; set; }
            [Column("created_at_utc")] public string CreatedAtUtc { get; set; }
            [Column("status")] public int Status { get; set; }
        }

        internal sealed class ObservationRow
        {
            [Column("observation_id")] public string ObservationId { get; set; }
            [Column("modality")] public int Modality { get; set; }
            [Column("observed_at_utc")] public string ObservedAtUtc { get; set; }
            [Column("session_id")] public string SessionId { get; set; }
            [Column("source_frame_id")] public long? SourceFrameId { get; set; }
            [Column("target_key")] public string TargetKey { get; set; }
            [Column("track_id")] public int? TrackId { get; set; }
            [Column("source_id")] public string SourceId { get; set; }
            [Column("created_at_utc")] public string CreatedAtUtc { get; set; }
        }

        internal sealed class RecognitionRow
        {
            [Column("recognition_evidence_id")] public string RecognitionEvidenceId { get; set; }
            [Column("observation_id")] public string ObservationId { get; set; }
            [Column("candidate_entity_id")] public string CandidateEntityId { get; set; }
            [Column("recognition_type")] public int RecognitionType { get; set; }
            [Column("feature_reference")] public string FeatureReference { get; set; }
            [Column("model_name")] public string ModelName { get; set; }
            [Column("model_version")] public string ModelVersion { get; set; }
            [Column("has_score")] public int HasScore { get; set; }
            [Column("score")] public double Score { get; set; }
            [Column("status")] public int Status { get; set; }
            [Column("created_at_utc")] public string CreatedAtUtc { get; set; }
            [Column("observed_at_utc")] public string ObservedAtUtc { get; set; }
        }

        internal sealed class FactRow
        {
            [Column("fact_id")] public string FactId { get; set; }
            [Column("subject_entity_id")] public string SubjectEntityId { get; set; }
            [Column("predicate")] public string Predicate { get; set; }
            [Column("value_kind")] public int ValueKind { get; set; }
            [Column("object_entity_id")] public string ObjectEntityId { get; set; }
            [Column("literal_value")] public string LiteralValue { get; set; }
            [Column("value_type")] public int ValueType { get; set; }
            [Column("status")] public int Status { get; set; }
            [Column("created_at_utc")] public string CreatedAtUtc { get; set; }
            [Column("updated_at_utc")] public string UpdatedAtUtc { get; set; }
            [Column("valid_from_utc")] public string ValidFromUtc { get; set; }
            [Column("valid_until_utc")] public string ValidUntilUtc { get; set; }
            [Column("last_verified_at_utc")] public string LastVerifiedAtUtc { get; set; }
            [Column("freshness_policy_id")] public string FreshnessPolicyId { get; set; }
            [Column("superseded_by_fact_id")] public string SupersededByFactId { get; set; }
        }

        internal sealed class EvidenceRow
        {
            [Column("evidence_id")] public string EvidenceId { get; set; }
            [Column("fact_id")] public string FactId { get; set; }
            [Column("evidence_type")] public int EvidenceType { get; set; }
            [Column("stance")] public int Stance { get; set; }
            [Column("source_id")] public string SourceId { get; set; }
            [Column("attributed_source_id")] public string AttributedSourceId { get; set; }
            [Column("has_confidence")] public int HasConfidence { get; set; }
            [Column("confidence")] public double Confidence { get; set; }
            [Column("created_at_utc")] public string CreatedAtUtc { get; set; }
            [Column("observed_at_utc")] public string ObservedAtUtc { get; set; }
            [Column("asserted_at_utc")] public string AssertedAtUtc { get; set; }
            [Column("retrieved_at_utc")] public string RetrievedAtUtc { get; set; }
            [Column("published_at_utc")] public string PublishedAtUtc { get; set; }
            [Column("valid_from_utc")] public string ValidFromUtc { get; set; }
            [Column("valid_until_utc")] public string ValidUntilUtc { get; set; }
            [Column("last_verified_at_utc")] public string LastVerifiedAtUtc { get; set; }
            [Column("observation_id")] public string ObservationId { get; set; }
            [Column("recognition_evidence_id")] public string RecognitionEvidenceId { get; set; }
            [Column("experience_record_id")] public string ExperienceRecordId { get; set; }
            [Column("inference_process_id")] public string InferenceProcessId { get; set; }
            [Column("status")] public int Status { get; set; }
            [Column("diagnostic_note")] public string DiagnosticNote { get; set; }
        }

        internal sealed class ResolutionRow
        {
            [Column("resolution_id")] public string ResolutionId { get; set; }
            [Column("observation_id")] public string ObservationId { get; set; }
            [Column("entity_id")] public string EntityId { get; set; }
            [Column("resolution_status")] public int ResolutionStatus { get; set; }
            [Column("resolution_method")] public int ResolutionMethod { get; set; }
            [Column("created_at_utc")] public string CreatedAtUtc { get; set; }
            [Column("has_confidence")] public int HasConfidence { get; set; }
            [Column("confidence")] public double Confidence { get; set; }
        }

        internal sealed class SupportRow
        {
            [Column("recognition_evidence_id")]
            public string RecognitionEvidenceId { get; set; }
        }
    }
}
