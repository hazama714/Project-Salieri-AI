// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

namespace SalieriAI.Core.Memory.WorldModel.Contracts
{
    public sealed class WorldMemoryEntity
    {
        public const int SchemaVersion = 1;

        public string EntityId { get; }
        public WorldMemoryEntityType EntityType { get; }
        /// <summary>
        /// Optional presentation hint only. Authoritative names are Facts and
        /// this value is never identity or conflict-resolution authority.
        /// </summary>
        public string CanonicalName { get; }
        public WorldMemoryEntityStatus Status { get; }
        public DateTime CreatedAtUtc { get; }
        public DateTime UpdatedAtUtc { get; }
        public string MergedIntoEntityId { get; }

        public WorldMemoryEntity(
            string entityId,
            WorldMemoryEntityType entityType,
            string canonicalName,
            WorldMemoryEntityStatus status,
            DateTime createdAtUtc,
            DateTime updatedAtUtc,
            string mergedIntoEntityId = "")
        {
            EntityId = WorldMemoryContractUtility.Id(entityId);
            EntityType = entityType;
            CanonicalName = WorldMemoryContractUtility.Text(canonicalName);
            Status = status;
            CreatedAtUtc = WorldMemoryContractUtility.Utc(createdAtUtc);
            UpdatedAtUtc = WorldMemoryContractUtility.Utc(updatedAtUtc);
            MergedIntoEntityId = WorldMemoryContractUtility.Id(
                mergedIntoEntityId);
        }

        public bool IsValid =>
            WorldMemoryContractValidator.Validate(this).IsValid;
    }

    public sealed class WorldMemoryFact
    {
        public const int SchemaVersion = 1;

        public string FactId { get; }
        public string SubjectEntityId { get; }
        public string Predicate { get; }
        public WorldMemoryFactValueKind ValueKind { get; }
        public string ObjectEntityId { get; }
        public string LiteralValue { get; }
        public WorldMemoryLiteralType ValueType { get; }
        public WorldMemoryFactStatus Status { get; }
        public DateTime CreatedAtUtc { get; }
        public DateTime UpdatedAtUtc { get; }
        public DateTime? ValidFromUtc { get; }
        public DateTime? ValidUntilUtc { get; }
        public DateTime? LastVerifiedAtUtc { get; }
        public string FreshnessPolicyId { get; }
        public string SupersededByFactId { get; }

        public WorldMemoryFact(
            string factId,
            string subjectEntityId,
            string predicate,
            WorldMemoryFactValueKind valueKind,
            string objectEntityId,
            string literalValue,
            WorldMemoryLiteralType valueType,
            WorldMemoryFactStatus status,
            DateTime createdAtUtc,
            DateTime updatedAtUtc,
            DateTime? validFromUtc = null,
            DateTime? validUntilUtc = null,
            DateTime? lastVerifiedAtUtc = null,
            string freshnessPolicyId = "",
            string supersededByFactId = "")
        {
            FactId = WorldMemoryContractUtility.Id(factId);
            SubjectEntityId = WorldMemoryContractUtility.Id(subjectEntityId);
            Predicate = WorldMemoryContractUtility.Text(predicate);
            ValueKind = valueKind;
            ObjectEntityId = WorldMemoryContractUtility.Id(objectEntityId);
            LiteralValue = literalValue ?? string.Empty;
            ValueType = valueType;
            Status = status;
            CreatedAtUtc = WorldMemoryContractUtility.Utc(createdAtUtc);
            UpdatedAtUtc = WorldMemoryContractUtility.Utc(updatedAtUtc);
            ValidFromUtc = WorldMemoryContractUtility.Utc(validFromUtc);
            ValidUntilUtc = WorldMemoryContractUtility.Utc(validUntilUtc);
            LastVerifiedAtUtc = WorldMemoryContractUtility.Utc(lastVerifiedAtUtc);
            FreshnessPolicyId = WorldMemoryContractUtility.Text(
                freshnessPolicyId);
            SupersededByFactId = WorldMemoryContractUtility.Id(
                supersededByFactId);
        }

        public bool IsValid =>
            WorldMemoryContractValidator.Validate(this).IsValid;
    }

    public sealed class WorldMemoryEvidence
    {
        public const int SchemaVersion = 1;

        public string EvidenceId { get; }
        public string FactId { get; }
        public WorldMemoryEvidenceType EvidenceType { get; }
        public WorldMemoryEvidenceStance Stance { get; }
        public string SourceId { get; }
        public string AttributedSourceId { get; }
        public bool HasConfidence { get; }
        public float Confidence { get; }
        public DateTime CreatedAtUtc { get; }
        public DateTime? ObservedAtUtc { get; }
        public DateTime? AssertedAtUtc { get; }
        public DateTime? RetrievedAtUtc { get; }
        public DateTime? PublishedAtUtc { get; }
        public DateTime? ValidFromUtc { get; }
        public DateTime? ValidUntilUtc { get; }
        public DateTime? LastVerifiedAtUtc { get; }
        public string ObservationId { get; }
        public string RecognitionEvidenceId { get; }
        public string ExperienceRecordId { get; }
        public string InferenceProcessId { get; }
        public WorldMemoryEvidenceStatus Status { get; }
        public string DiagnosticNote { get; }

        public WorldMemoryEvidence(
            string evidenceId,
            string factId,
            WorldMemoryEvidenceType evidenceType,
            WorldMemoryEvidenceStance stance,
            string sourceId,
            string attributedSourceId,
            bool hasConfidence,
            float confidence,
            DateTime createdAtUtc,
            DateTime? observedAtUtc = null,
            DateTime? assertedAtUtc = null,
            DateTime? retrievedAtUtc = null,
            DateTime? publishedAtUtc = null,
            DateTime? validFromUtc = null,
            DateTime? validUntilUtc = null,
            DateTime? lastVerifiedAtUtc = null,
            string observationId = "",
            string recognitionEvidenceId = "",
            string experienceRecordId = "",
            string inferenceProcessId = "",
            WorldMemoryEvidenceStatus status =
                WorldMemoryEvidenceStatus.Active,
            string diagnosticNote = "")
        {
            EvidenceId = WorldMemoryContractUtility.Id(evidenceId);
            FactId = WorldMemoryContractUtility.Id(factId);
            EvidenceType = evidenceType;
            Stance = stance;
            SourceId = WorldMemoryContractUtility.Id(sourceId);
            AttributedSourceId = WorldMemoryContractUtility.Id(
                attributedSourceId);
            HasConfidence = hasConfidence;
            Confidence = confidence;
            CreatedAtUtc = WorldMemoryContractUtility.Utc(createdAtUtc);
            ObservedAtUtc = WorldMemoryContractUtility.Utc(observedAtUtc);
            AssertedAtUtc = WorldMemoryContractUtility.Utc(assertedAtUtc);
            RetrievedAtUtc = WorldMemoryContractUtility.Utc(retrievedAtUtc);
            PublishedAtUtc = WorldMemoryContractUtility.Utc(publishedAtUtc);
            ValidFromUtc = WorldMemoryContractUtility.Utc(validFromUtc);
            ValidUntilUtc = WorldMemoryContractUtility.Utc(validUntilUtc);
            LastVerifiedAtUtc = WorldMemoryContractUtility.Utc(lastVerifiedAtUtc);
            ObservationId = WorldMemoryContractUtility.Id(observationId);
            RecognitionEvidenceId = WorldMemoryContractUtility.Id(
                recognitionEvidenceId);
            ExperienceRecordId = WorldMemoryContractUtility.Text(
                experienceRecordId);
            InferenceProcessId = WorldMemoryContractUtility.Text(
                inferenceProcessId);
            Status = status;
            DiagnosticNote = WorldMemoryContractUtility.Text(diagnosticNote);
        }

        public bool IsValid =>
            WorldMemoryContractValidator.Validate(this).IsValid;
    }

    public sealed class WorldMemorySource
    {
        public const int SchemaVersion = 1;

        public string SourceId { get; }
        public WorldMemorySourceType SourceType { get; }
        public string DisplayName { get; }
        public string Uri { get; }
        public string Publisher { get; }
        public string ExternalReference { get; }
        public string ComponentId { get; }
        public DateTime CreatedAtUtc { get; }
        public WorldMemorySourceStatus Status { get; }

        public WorldMemorySource(
            string sourceId,
            WorldMemorySourceType sourceType,
            string displayName,
            string uri,
            string publisher,
            string externalReference,
            string componentId,
            DateTime createdAtUtc,
            WorldMemorySourceStatus status = WorldMemorySourceStatus.Active)
        {
            SourceId = WorldMemoryContractUtility.Id(sourceId);
            SourceType = sourceType;
            DisplayName = WorldMemoryContractUtility.Text(displayName);
            Uri = WorldMemoryContractUtility.Text(uri);
            Publisher = WorldMemoryContractUtility.Text(publisher);
            ExternalReference = WorldMemoryContractUtility.Text(
                externalReference);
            ComponentId = WorldMemoryContractUtility.Text(componentId);
            CreatedAtUtc = WorldMemoryContractUtility.Utc(createdAtUtc);
            Status = status;
        }

        public bool IsValid =>
            WorldMemoryContractValidator.Validate(this).IsValid;
    }

    public sealed class WorldMemoryObservation
    {
        public const int SchemaVersion = 1;

        public string ObservationId { get; }
        public WorldMemoryObservationModality Modality { get; }
        public DateTime ObservedAtUtc { get; }
        public string SessionId { get; }
        public long? SourceFrameId { get; }
        public string TargetKey { get; }
        public int? TrackId { get; }
        public string SourceId { get; }
        public DateTime CreatedAtUtc { get; }

        public WorldMemoryObservation(
            string observationId,
            WorldMemoryObservationModality modality,
            DateTime observedAtUtc,
            string sessionId,
            long? sourceFrameId,
            string targetKey,
            int? trackId,
            string sourceId,
            DateTime createdAtUtc)
        {
            ObservationId = WorldMemoryContractUtility.Id(observationId);
            Modality = modality;
            ObservedAtUtc = WorldMemoryContractUtility.Utc(observedAtUtc);
            SessionId = WorldMemoryContractUtility.Text(sessionId);
            SourceFrameId = sourceFrameId;
            TargetKey = WorldMemoryContractUtility.Text(targetKey);
            TrackId = trackId;
            SourceId = WorldMemoryContractUtility.Id(sourceId);
            CreatedAtUtc = WorldMemoryContractUtility.Utc(createdAtUtc);
        }

        public bool IsValid =>
            WorldMemoryContractValidator.Validate(this).IsValid;
    }

    public sealed class WorldMemoryRecognitionEvidence
    {
        public const int SchemaVersion = 1;

        public string RecognitionEvidenceId { get; }
        public string ObservationId { get; }
        public string CandidateEntityId { get; }
        public WorldMemoryRecognitionType RecognitionType { get; }
        public string FeatureReference { get; }
        public string ModelName { get; }
        public string ModelVersion { get; }
        public bool HasScore { get; }
        public float Score { get; }
        public WorldMemoryRecognitionEvidenceStatus Status { get; }
        public DateTime CreatedAtUtc { get; }
        public DateTime? ObservedAtUtc { get; }

        public WorldMemoryRecognitionEvidence(
            string recognitionEvidenceId,
            string observationId,
            string candidateEntityId,
            WorldMemoryRecognitionType recognitionType,
            string featureReference,
            string modelName,
            string modelVersion,
            bool hasScore,
            float score,
            WorldMemoryRecognitionEvidenceStatus status,
            DateTime createdAtUtc,
            DateTime? observedAtUtc = null)
        {
            RecognitionEvidenceId = WorldMemoryContractUtility.Id(
                recognitionEvidenceId);
            ObservationId = WorldMemoryContractUtility.Id(observationId);
            CandidateEntityId = WorldMemoryContractUtility.Id(candidateEntityId);
            RecognitionType = recognitionType;
            FeatureReference = WorldMemoryContractUtility.Text(featureReference);
            ModelName = WorldMemoryContractUtility.Text(modelName);
            ModelVersion = WorldMemoryContractUtility.Text(modelVersion);
            HasScore = hasScore;
            Score = score;
            Status = status;
            CreatedAtUtc = WorldMemoryContractUtility.Utc(createdAtUtc);
            ObservedAtUtc = WorldMemoryContractUtility.Utc(observedAtUtc);
        }

        public bool IsValid =>
            WorldMemoryContractValidator.Validate(this).IsValid;
    }

    public sealed class WorldMemoryEntityResolution
    {
        public const int SchemaVersion = 1;

        public string ResolutionId { get; }
        public string ObservationId { get; }
        public string EntityId { get; }
        public WorldMemoryResolutionStatus ResolutionStatus { get; }
        public WorldMemoryResolutionMethod ResolutionMethod { get; }
        public DateTime CreatedAtUtc { get; }
        public bool HasConfidence { get; }
        public float Confidence { get; }
        public IReadOnlyList<string> SupportingRecognitionEvidenceIds { get; }

        public WorldMemoryEntityResolution(
            string resolutionId,
            string observationId,
            string entityId,
            WorldMemoryResolutionStatus resolutionStatus,
            WorldMemoryResolutionMethod resolutionMethod,
            DateTime createdAtUtc,
            bool hasConfidence,
            float confidence,
            IEnumerable<string> supportingRecognitionEvidenceIds)
        {
            ResolutionId = WorldMemoryContractUtility.Id(resolutionId);
            ObservationId = WorldMemoryContractUtility.Id(observationId);
            EntityId = WorldMemoryContractUtility.Id(entityId);
            ResolutionStatus = resolutionStatus;
            ResolutionMethod = resolutionMethod;
            CreatedAtUtc = WorldMemoryContractUtility.Utc(createdAtUtc);
            HasConfidence = hasConfidence;
            Confidence = confidence;
            SupportingRecognitionEvidenceIds =
                WorldMemoryContractUtility.IdCopy(
                    supportingRecognitionEvidenceIds);
        }

        public bool IsValid =>
            WorldMemoryContractValidator.Validate(this).IsValid;
    }
}
