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

using SalieriAI.Core.Behavior.FindPointAsk;
using SalieriAI.Core.Experience.Storage;
using SalieriAI.Core.Memory.WorldModel.Contracts;
using SalieriAI.Core.Memory.WorldModel.Storage;

namespace SalieriAI.Core.Memory.WorldModel.Projection
{
    /// <summary>
    /// M5-DB3's single explicit mapping:
    /// IdentifyObject Experience -> Entity --name--> normalized answer.
    /// It does not parse language, resolve identity, promote an Entity, or
    /// mutate the originating Experience.
    /// </summary>
    public sealed class WorldMemoryExperienceFactProjectionService
    {
        public const string NamePredicate = "name";

        private readonly object gate = new object();
        private readonly IWorldMemoryAtomicMutationStore store;
        private readonly IWorldMemoryProjectionIdentityGenerator identities;

        public WorldMemoryExperienceFactProjectionService(
            IWorldMemoryAtomicMutationStore store,
            IWorldMemoryProjectionIdentityGenerator identities)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.identities = identities ??
                throw new ArgumentNullException(nameof(identities));
        }

        public WorldMemoryExperienceProjectionResult Project(
            WorldMemoryExperienceProjectionRequest request)
        {
            lock (gate)
            {
                WorldMemoryExperienceProjectionResult rejected =
                    ValidateInput(request);
                if (rejected != null)
                    return rejected;

                ExperienceRecord experience = request.Experience;
                if (!store.TryGetEntity(
                        request.EntityId,
                        out WorldMemoryEntity _))
                {
                    return Result(
                        WorldMemoryExperienceProjectionStatus.MissingEntity,
                        request, null, null, false,
                        "Persistent Entity does not exist.");
                }

                if (!ValidateSources(request, out string sourceError))
                {
                    WorldMemoryExperienceProjectionStatus status =
                        sourceError == "missing"
                            ? WorldMemoryExperienceProjectionStatus
                                .MissingSource
                            : WorldMemoryExperienceProjectionStatus
                                .InvalidProvenance;
                    return Result(status, request, null, null, false,
                        "Source provenance is " + sourceError + ".");
                }

                WorldMemoryFact fact = FindEquivalentFact(
                    request.EntityId,
                    experience.NormalizedAnswer);
                if (fact != null)
                {
                    IReadOnlyList<WorldMemoryEvidence> existingEvidence =
                        store.GetEvidenceByFact(fact.FactId);
                    for (int i = 0; i < existingEvidence.Count; i++)
                    {
                        if (string.Equals(
                                existingEvidence[i].ExperienceRecordId,
                                experience.RecordId,
                                StringComparison.Ordinal))
                        {
                            return new WorldMemoryExperienceProjectionResult(
                                WorldMemoryExperienceProjectionStatus
                                    .AlreadyProjected,
                                request.EntityId,
                                fact,
                                existingEvidence[i],
                                request.SourceId,
                                false,
                                "Experience mapping is already projected.");
                        }
                    }
                }

                bool createsFact = fact == null;
                string factId = createsFact
                    ? WorldMemoryContractUtility.Id(identities.CreateFactId())
                    : fact.FactId;
                string evidenceId = WorldMemoryContractUtility.Id(
                    identities.CreateEvidenceId());

                if (!ValidGeneratedIds(
                        factId,
                        evidenceId,
                        request,
                        createsFact))
                {
                    return Result(
                        WorldMemoryExperienceProjectionStatus
                            .InvalidGeneratedIdentity,
                        request, null, null, false,
                        "Generated IDs are invalid, duplicate, or provenance-derived.");
                }

                if (createsFact)
                {
                    fact = new WorldMemoryFact(
                        factId,
                        request.EntityId,
                        NamePredicate,
                        WorldMemoryFactValueKind.Literal,
                        "",
                        experience.NormalizedAnswer,
                        WorldMemoryLiteralType.String,
                        WorldMemoryFactStatus.Active,
                        request.ProjectedAtUtc,
                        request.ProjectedAtUtc);
                }

                var evidence = new WorldMemoryEvidence(
                    evidenceId,
                    fact.FactId,
                    request.EvidenceType,
                    WorldMemoryEvidenceStance.Supports,
                    request.SourceId,
                    request.AttributedSourceId,
                    experience.HasConfidence,
                    experience.Confidence,
                    request.ProjectedAtUtc,
                    null,
                    experience.CreatedAt,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "",
                    "",
                    experience.RecordId,
                    "",
                    WorldMemoryEvidenceStatus.Active,
                    "M5-DB3 IdentifyObject/name projection");

                if (!fact.IsValid || !evidence.IsValid)
                {
                    return Result(
                        WorldMemoryExperienceProjectionStatus
                            .InvalidProvenance,
                        request, null, null, false,
                        "Staged Fact or Evidence is invalid.");
                }

                if (createsFact)
                {
                    WorldMemoryAtomicMutationResult mutation =
                        store.ApplyAtomicMutation(
                            WorldMemoryAtomicMutation
                                .ForExperienceProjection(fact, evidence));
                    if (!mutation.Succeeded)
                    {
                        return Result(
                            WorldMemoryExperienceProjectionStatus.StoreRejected,
                            request, fact, evidence, false, mutation.Error);
                    }
                }
                else
                {
                    WorldMemoryWriteResult<WorldMemoryEvidence>
                        evidenceWrite = store.AddEvidence(evidence);
                    if (!evidenceWrite.Succeeded)
                    {
                        return Result(
                            WorldMemoryExperienceProjectionStatus
                                .StoreRejected,
                            request, fact, evidence, false,
                            evidenceWrite.Error);
                    }
                }

                return Result(
                    WorldMemoryExperienceProjectionStatus.Projected,
                    request, fact, evidence, createsFact, string.Empty);
            }
        }

        private WorldMemoryExperienceProjectionResult ValidateInput(
            WorldMemoryExperienceProjectionRequest request)
        {
            if (request == null || request.Experience == null)
            {
                return Result(
                    WorldMemoryExperienceProjectionStatus.InvalidExperience,
                    request, null, null, false,
                    "Experience is missing.");
            }

            if (request.Experience.NormalizedAnswer.Length == 0)
            {
                return Result(
                    WorldMemoryExperienceProjectionStatus.InvalidAnswer,
                    request, null, null, false,
                    "Normalized answer is empty.");
            }

            if (!request.Experience.IsValid ||
                request.Experience.Source !=
                    ExperienceRecordSource.AnswerBinding ||
                !WorldMemoryContractUtility.IsCanonicalId(request.EntityId) ||
                !WorldMemoryContractUtility.IsUtc(request.ProjectedAtUtc))
            {
                return Result(
                    WorldMemoryExperienceProjectionStatus.InvalidExperience,
                    request, null, null, false,
                    "Experience or projection context is invalid.");
            }

            if (request.Experience.QuestionType !=
                QuestionType.IdentifyObject)
            {
                return Result(
                    WorldMemoryExperienceProjectionStatus.UnsupportedQuestion,
                    request, null, null, false,
                    "QuestionType has no explicit M5-DB3 mapping.");
            }

            if (!SupportedEvidenceType(request.EvidenceType))
            {
                return Result(
                    WorldMemoryExperienceProjectionStatus.InvalidProvenance,
                    request, null, null, false,
                    "EvidenceType is outside the supported provenance set.");
            }

            return null;
        }

        private bool ValidateSources(
            WorldMemoryExperienceProjectionRequest request,
            out string error)
        {
            bool humanStatement =
                request.EvidenceType == WorldMemoryEvidenceType.HumanTeaching ||
                request.EvidenceType == WorldMemoryEvidenceType.Hearsay;
            if (humanStatement && request.SourceId.Length == 0)
            {
                error = "missing";
                return false;
            }

            if (request.SourceId.Length > 0)
            {
                if (!store.TryGetSource(
                        request.SourceId,
                        out WorldMemorySource source))
                {
                    error = "missing";
                    return false;
                }

                if (humanStatement &&
                    source.SourceType !=
                        WorldMemorySourceType.IdentifiedHuman &&
                    source.SourceType !=
                        WorldMemorySourceType.UnidentifiedSpeaker)
                {
                    error = "incompatible";
                    return false;
                }
            }

            if (request.AttributedSourceId.Length > 0 &&
                !store.TryGetSource(
                    request.AttributedSourceId,
                    out WorldMemorySource _))
            {
                error = "missing";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private WorldMemoryFact FindEquivalentFact(
            string entityId,
            string normalizedAnswer)
        {
            IReadOnlyList<WorldMemoryFact> facts =
                store.GetFactsBySubjectAndPredicate(
                    entityId, NamePredicate);
            for (int i = 0; i < facts.Count; i++)
            {
                WorldMemoryFact item = facts[i];
                if (item.ValueKind == WorldMemoryFactValueKind.Literal &&
                    item.ValueType == WorldMemoryLiteralType.String &&
                    item.Status == WorldMemoryFactStatus.Active &&
                    string.Equals(
                        item.LiteralValue,
                        normalizedAnswer,
                        StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private bool ValidGeneratedIds(
            string factId,
            string evidenceId,
            WorldMemoryExperienceProjectionRequest request,
            bool createsFact)
        {
            if (!WorldMemoryContractUtility.IsCanonicalId(factId) ||
                !WorldMemoryContractUtility.IsCanonicalId(evidenceId) ||
                string.Equals(factId, evidenceId, StringComparison.Ordinal) ||
                ProvenanceContains(request, factId) ||
                ProvenanceContains(request, evidenceId))
            {
                return false;
            }

            if ((createsFact && store.TryGetFact(
                    factId,
                    out WorldMemoryFact _)) ||
                store.TryGetEvidence(
                    evidenceId,
                    out WorldMemoryEvidence _))
            {
                return false;
            }

            return true;
        }

        private static bool ProvenanceContains(
            WorldMemoryExperienceProjectionRequest request,
            string generatedId)
        {
            ExperienceRecord value = request.Experience;
            string track = value.TrackId.ToString(
                CultureInfo.InvariantCulture);
            return Same(generatedId, request.EntityId) ||
                Same(generatedId, value.RecordId) ||
                Same(generatedId, value.BehaviorRunId) ||
                Same(generatedId, value.QuestionId) ||
                Same(generatedId, value.TargetKey) ||
                Same(generatedId, value.ObservationReference) ||
                Same(generatedId, value.RecallKey) ||
                Same(generatedId, track) ||
                Same(generatedId, request.SourceId) ||
                Same(generatedId, request.AttributedSourceId);
        }

        private static bool Same(string first, string second)
        {
            return string.Equals(
                first,
                WorldMemoryContractUtility.Id(second),
                StringComparison.Ordinal);
        }

        private static bool SupportedEvidenceType(
            WorldMemoryEvidenceType type)
        {
            return type == WorldMemoryEvidenceType.HumanTeaching ||
                type == WorldMemoryEvidenceType.Hearsay ||
                type == WorldMemoryEvidenceType.ExperienceDerived;
        }

        private static WorldMemoryExperienceProjectionResult Result(
            WorldMemoryExperienceProjectionStatus status,
            WorldMemoryExperienceProjectionRequest request,
            WorldMemoryFact fact,
            WorldMemoryEvidence evidence,
            bool factWasCreated,
            string diagnostic)
        {
            return new WorldMemoryExperienceProjectionResult(
                status,
                request == null ? string.Empty : request.EntityId,
                fact,
                evidence,
                request == null ? string.Empty : request.SourceId,
                factWasCreated,
                diagnostic);
        }
    }
}
