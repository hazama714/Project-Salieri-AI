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

namespace SalieriAI.Core.Memory.WorldModel.Contracts
{
    public sealed class WorldMemoryValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public IReadOnlyList<string> Errors { get; }

        internal WorldMemoryValidationResult(IEnumerable<string> errors)
        {
            Errors = new ReadOnlyCollection<string>(
                errors != null
                    ? new List<string>(errors)
                    : new List<string>());
        }

        public bool Contains(string error)
        {
            for (int i = 0; i < Errors.Count; i++)
            {
                if (string.Equals(Errors[i], error, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }

    public static class WorldMemoryContractValidator
    {
        public const string ValidatorVersion =
            "world-memory-contract-validator-v0.1";

        public static WorldMemoryValidationResult Validate(
            WorldMemoryEntity value)
        {
            var errors = new List<string>();
            if (value == null)
            {
                errors.Add("Entity.Null");
                return Result(errors);
            }

            Id(errors, value.EntityId, "Entity.EntityId");
            DefinedNonUnknown(errors, value.EntityType, "Entity.EntityType");
            DefinedNonUnknown(errors, value.Status, "Entity.Status");
            RequiredUtc(errors, value.CreatedAtUtc, "Entity.CreatedAtUtc");
            RequiredUtc(errors, value.UpdatedAtUtc, "Entity.UpdatedAtUtc");
            Order(errors, value.CreatedAtUtc, value.UpdatedAtUtc,
                "Entity.CreatedAfterUpdated");

            if (value.Status == WorldMemoryEntityStatus.Merged)
            {
                Id(errors, value.MergedIntoEntityId,
                    "Entity.MergedIntoEntityId");
                if (string.Equals(value.EntityId, value.MergedIntoEntityId,
                    StringComparison.Ordinal))
                {
                    errors.Add("Entity.SelfMerge");
                }
            }
            else if (value.MergedIntoEntityId.Length > 0)
            {
                errors.Add("Entity.UnexpectedMergedIntoEntityId");
            }

            return Result(errors);
        }

        public static WorldMemoryValidationResult Validate(
            WorldMemoryFact value)
        {
            var errors = new List<string>();
            if (value == null)
            {
                errors.Add("Fact.Null");
                return Result(errors);
            }

            Id(errors, value.FactId, "Fact.FactId");
            Id(errors, value.SubjectEntityId, "Fact.SubjectEntityId");
            RequiredText(errors, value.Predicate, "Fact.Predicate");
            DefinedNonUnknown(errors, value.ValueKind, "Fact.ValueKind");
            DefinedNonUnknown(errors, value.Status, "Fact.Status");
            RequiredUtc(errors, value.CreatedAtUtc, "Fact.CreatedAtUtc");
            RequiredUtc(errors, value.UpdatedAtUtc, "Fact.UpdatedAtUtc");
            Order(errors, value.CreatedAtUtc, value.UpdatedAtUtc,
                "Fact.CreatedAfterUpdated");
            OptionalUtc(errors, value.ValidFromUtc, "Fact.ValidFromUtc");
            OptionalUtc(errors, value.ValidUntilUtc, "Fact.ValidUntilUtc");
            OptionalUtc(errors, value.LastVerifiedAtUtc,
                "Fact.LastVerifiedAtUtc");
            OptionalOrder(errors, value.ValidFromUtc, value.ValidUntilUtc,
                "Fact.InvalidValidityInterval");

            if (value.ValueKind == WorldMemoryFactValueKind.EntityReference)
            {
                Id(errors, value.ObjectEntityId, "Fact.ObjectEntityId");
                if (value.LiteralValue.Length > 0)
                    errors.Add("Fact.BothEntityAndLiteralValue");
                if (value.ValueType != WorldMemoryLiteralType.Unknown)
                    errors.Add("Fact.EntityReferenceHasLiteralType");
            }
            else if (value.ValueKind == WorldMemoryFactValueKind.Literal)
            {
                if (value.ObjectEntityId.Length > 0)
                    errors.Add("Fact.BothEntityAndLiteralValue");
                RequiredText(errors, value.LiteralValue,
                    "Fact.LiteralValue");
                DefinedNonUnknown(errors, value.ValueType,
                    "Fact.ValueType");
            }

            OptionalId(errors, value.SupersededByFactId,
                "Fact.SupersededByFactId");
            if (value.SupersededByFactId.Length > 0 &&
                string.Equals(value.FactId, value.SupersededByFactId,
                    StringComparison.Ordinal))
            {
                errors.Add("Fact.SelfSupersession");
            }

            return Result(errors);
        }

        public static WorldMemoryValidationResult Validate(
            WorldMemoryEvidence value)
        {
            var errors = new List<string>();
            if (value == null)
            {
                errors.Add("Evidence.Null");
                return Result(errors);
            }

            Id(errors, value.EvidenceId, "Evidence.EvidenceId");
            Id(errors, value.FactId, "Evidence.FactId");
            DefinedNonUnknown(errors, value.EvidenceType,
                "Evidence.EvidenceType");
            DefinedNonUnknown(errors, value.Stance, "Evidence.Stance");
            OptionalId(errors, value.SourceId, "Evidence.SourceId");
            OptionalId(errors, value.AttributedSourceId,
                "Evidence.AttributedSourceId");
            Score(errors, value.HasConfidence, value.Confidence,
                "Evidence.Confidence");
            RequiredUtc(errors, value.CreatedAtUtc, "Evidence.CreatedAtUtc");
            OptionalUtc(errors, value.ObservedAtUtc,
                "Evidence.ObservedAtUtc");
            OptionalUtc(errors, value.AssertedAtUtc,
                "Evidence.AssertedAtUtc");
            OptionalUtc(errors, value.RetrievedAtUtc,
                "Evidence.RetrievedAtUtc");
            OptionalUtc(errors, value.PublishedAtUtc,
                "Evidence.PublishedAtUtc");
            OptionalUtc(errors, value.ValidFromUtc,
                "Evidence.ValidFromUtc");
            OptionalUtc(errors, value.ValidUntilUtc,
                "Evidence.ValidUntilUtc");
            OptionalUtc(errors, value.LastVerifiedAtUtc,
                "Evidence.LastVerifiedAtUtc");
            OptionalOrder(errors, value.ValidFromUtc, value.ValidUntilUtc,
                "Evidence.InvalidValidityInterval");
            OptionalId(errors, value.ObservationId,
                "Evidence.ObservationId");
            OptionalId(errors, value.RecognitionEvidenceId,
                "Evidence.RecognitionEvidenceId");
            DefinedNonUnknown(errors, value.Status, "Evidence.Status");
            return Result(errors);
        }

        public static WorldMemoryValidationResult Validate(
            WorldMemorySource value)
        {
            var errors = new List<string>();
            if (value == null)
            {
                errors.Add("Source.Null");
                return Result(errors);
            }

            Id(errors, value.SourceId, "Source.SourceId");
            DefinedNonUnknown(errors, value.SourceType, "Source.SourceType");
            RequiredUtc(errors, value.CreatedAtUtc, "Source.CreatedAtUtc");
            DefinedNonUnknown(errors, value.Status, "Source.Status");

            if (value.SourceType == WorldMemorySourceType.WebResource)
            {
                RequiredText(errors, value.Uri, "Source.WebUri");
                if (value.Uri.Length > 0 &&
                    (!System.Uri.TryCreate(value.Uri, UriKind.Absolute,
                        out Uri parsed) ||
                     (parsed.Scheme != Uri.UriSchemeHttp &&
                      parsed.Scheme != Uri.UriSchemeHttps)))
                {
                    errors.Add("Source.InvalidWebUri");
                }
            }

            return Result(errors);
        }

        public static WorldMemoryValidationResult Validate(
            WorldMemoryObservation value)
        {
            var errors = new List<string>();
            if (value == null)
            {
                errors.Add("Observation.Null");
                return Result(errors);
            }

            Id(errors, value.ObservationId, "Observation.ObservationId");
            DefinedNonUnknown(errors, value.Modality, "Observation.Modality");
            RequiredUtc(errors, value.ObservedAtUtc,
                "Observation.ObservedAtUtc");
            RequiredUtc(errors, value.CreatedAtUtc,
                "Observation.CreatedAtUtc");
            if (value.SourceFrameId.HasValue && value.SourceFrameId.Value <= 0)
                errors.Add("Observation.InvalidSourceFrameId");
            if (value.TrackId.HasValue && value.TrackId.Value < 0)
                errors.Add("Observation.InvalidTrackId");
            OptionalId(errors, value.SourceId, "Observation.SourceId");
            return Result(errors);
        }

        public static WorldMemoryValidationResult Validate(
            WorldMemoryRecognitionEvidence value)
        {
            var errors = new List<string>();
            if (value == null)
            {
                errors.Add("RecognitionEvidence.Null");
                return Result(errors);
            }

            Id(errors, value.RecognitionEvidenceId,
                "RecognitionEvidence.RecognitionEvidenceId");
            Id(errors, value.ObservationId,
                "RecognitionEvidence.ObservationId");
            OptionalId(errors, value.CandidateEntityId,
                "RecognitionEvidence.CandidateEntityId");
            DefinedNonUnknown(errors, value.RecognitionType,
                "RecognitionEvidence.RecognitionType");
            RequiredText(errors, value.FeatureReference,
                "RecognitionEvidence.FeatureReference");
            Score(errors, value.HasScore, value.Score,
                "RecognitionEvidence.Score");
            DefinedNonUnknown(errors, value.Status,
                "RecognitionEvidence.Status");
            RequiredUtc(errors, value.CreatedAtUtc,
                "RecognitionEvidence.CreatedAtUtc");
            OptionalUtc(errors, value.ObservedAtUtc,
                "RecognitionEvidence.ObservedAtUtc");
            return Result(errors);
        }

        public static WorldMemoryValidationResult Validate(
            WorldMemoryEntityResolution value)
        {
            var errors = new List<string>();
            if (value == null)
            {
                errors.Add("EntityResolution.Null");
                return Result(errors);
            }

            Id(errors, value.ResolutionId, "EntityResolution.ResolutionId");
            Id(errors, value.ObservationId,
                "EntityResolution.ObservationId");
            DefinedNonUnknown(errors, value.ResolutionStatus,
                "EntityResolution.ResolutionStatus");
            DefinedNonUnknown(errors, value.ResolutionMethod,
                "EntityResolution.ResolutionMethod");
            RequiredUtc(errors, value.CreatedAtUtc,
                "EntityResolution.CreatedAtUtc");
            Score(errors, value.HasConfidence, value.Confidence,
                "EntityResolution.Confidence");

            if (value.ResolutionStatus == WorldMemoryResolutionStatus.Resolved)
            {
                Id(errors, value.EntityId, "EntityResolution.EntityId");
            }
            else if ((value.ResolutionStatus ==
                      WorldMemoryResolutionStatus.Unresolved ||
                      value.ResolutionStatus ==
                      WorldMemoryResolutionStatus.Ambiguous) &&
                     value.EntityId.Length > 0)
            {
                errors.Add("EntityResolution.UnresolvedHasEntityId");
            }
            else
            {
                OptionalId(errors, value.EntityId,
                    "EntityResolution.EntityId");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0;
                 i < value.SupportingRecognitionEvidenceIds.Count;
                 i++)
            {
                string id = value.SupportingRecognitionEvidenceIds[i];
                Id(errors, id,
                    "EntityResolution.SupportingRecognitionEvidenceId");
                if (!seen.Add(id))
                    errors.Add("EntityResolution.DuplicateSupportingEvidence");
            }

            if (value.ResolutionMethod ==
                    WorldMemoryResolutionMethod.RecognitionEvidence &&
                value.SupportingRecognitionEvidenceIds.Count == 0)
            {
                errors.Add("EntityResolution.MissingRecognitionEvidence");
            }

            return Result(errors);
        }

        private static WorldMemoryValidationResult Result(
            IEnumerable<string> errors)
        {
            return new WorldMemoryValidationResult(errors);
        }

        private static void Id(
            ICollection<string> errors,
            string value,
            string field)
        {
            if (!WorldMemoryContractUtility.IsCanonicalId(value))
                errors.Add(field);
        }

        private static void OptionalId(
            ICollection<string> errors,
            string value,
            string field)
        {
            if (!string.IsNullOrEmpty(value) &&
                !WorldMemoryContractUtility.IsCanonicalId(value))
            {
                errors.Add(field);
            }
        }

        private static void RequiredText(
            ICollection<string> errors,
            string value,
            string field)
        {
            if (string.IsNullOrWhiteSpace(value))
                errors.Add(field);
        }

        private static void DefinedNonUnknown<T>(
            ICollection<string> errors,
            T value,
            string field) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value) ||
                Convert.ToInt32(value) == 0)
            {
                errors.Add(field);
            }
        }

        private static void RequiredUtc(
            ICollection<string> errors,
            DateTime value,
            string field)
        {
            if (!WorldMemoryContractUtility.IsUtc(value))
                errors.Add(field);
        }

        private static void OptionalUtc(
            ICollection<string> errors,
            DateTime? value,
            string field)
        {
            if (!WorldMemoryContractUtility.IsUtc(value))
                errors.Add(field);
        }

        private static void Score(
            ICollection<string> errors,
            bool hasValue,
            float value,
            string field)
        {
            if (!WorldMemoryContractUtility.ValidOptionalScore(hasValue, value))
                errors.Add(field);
        }

        private static void Order(
            ICollection<string> errors,
            DateTime first,
            DateTime second,
            string error)
        {
            if (first != default(DateTime) &&
                second != default(DateTime) && first > second)
            {
                errors.Add(error);
            }
        }

        private static void OptionalOrder(
            ICollection<string> errors,
            DateTime? first,
            DateTime? second,
            string error)
        {
            if (first.HasValue && second.HasValue && first.Value > second.Value)
                errors.Add(error);
        }
    }
}
