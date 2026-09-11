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

using SalieriAI.Core.Memory.WorldModel.Contracts;

namespace SalieriAI.Core.Memory.WorldModel.Storage
{
    /// <summary>
    /// Deterministic insertion-ordered M5-DB1 store. It performs no file IO,
    /// serialization, implicit Entity promotion, or conflict resolution.
    /// </summary>
    public sealed class InMemoryWorldMemoryStore :
        IWorldMemoryAtomicMutationStore
    {
        private readonly object gate = new object();

        private readonly Dictionary<string, WorldMemoryEntity> entities =
            new Dictionary<string, WorldMemoryEntity>(StringComparer.Ordinal);
        private readonly List<WorldMemoryEntity> entityOrder =
            new List<WorldMemoryEntity>();

        private readonly Dictionary<string, WorldMemoryFact> facts =
            new Dictionary<string, WorldMemoryFact>(StringComparer.Ordinal);
        private readonly List<WorldMemoryFact> factOrder =
            new List<WorldMemoryFact>();

        private readonly Dictionary<string, WorldMemorySource> sources =
            new Dictionary<string, WorldMemorySource>(StringComparer.Ordinal);
        private readonly List<WorldMemorySource> sourceOrder =
            new List<WorldMemorySource>();

        private readonly Dictionary<string, WorldMemoryEvidence> evidence =
            new Dictionary<string, WorldMemoryEvidence>(StringComparer.Ordinal);
        private readonly List<WorldMemoryEvidence> evidenceOrder =
            new List<WorldMemoryEvidence>();

        private readonly Dictionary<string, WorldMemoryObservation> observations =
            new Dictionary<string, WorldMemoryObservation>(StringComparer.Ordinal);
        private readonly List<WorldMemoryObservation> observationOrder =
            new List<WorldMemoryObservation>();

        private readonly Dictionary<string, WorldMemoryRecognitionEvidence>
            recognitionEvidence =
                new Dictionary<string, WorldMemoryRecognitionEvidence>(
                    StringComparer.Ordinal);
        private readonly List<WorldMemoryRecognitionEvidence>
            recognitionEvidenceOrder =
                new List<WorldMemoryRecognitionEvidence>();

        private readonly Dictionary<string, WorldMemoryEntityResolution>
            resolutions =
                new Dictionary<string, WorldMemoryEntityResolution>(
                    StringComparer.Ordinal);
        private readonly List<WorldMemoryEntityResolution> resolutionOrder =
            new List<WorldMemoryEntityResolution>();

        public WorldMemoryAtomicMutationResult ApplyAtomicMutation(
            WorldMemoryAtomicMutation mutation)
        {
            lock (gate)
            {
                if (mutation == null ||
                    mutation.Kind == WorldMemoryAtomicMutationKind.Unknown)
                {
                    return AtomicFailure(
                        WorldMemoryWriteStatus.InvalidRecord,
                        WorldMemoryAtomicMutationKind.Unknown,
                        WorldMemoryAtomicMutationComponent.None,
                        -1,
                        "Atomic mutation is invalid.");
                }

                WorldMemoryAtomicMutationResult shape =
                    ValidateMutationShape(mutation);
                if (!shape.Succeeded)
                    return shape;

                WorldMemoryAtomicMutationResult references =
                    ValidateExistingReferences(mutation);
                if (!references.Succeeded)
                    return references;

                InMemoryWorldMemoryStore staged = CloneUnsafe();
                WorldMemoryAtomicMutationResult result =
                    staged.ApplyToStagedStore(mutation);
                if (!result.Succeeded)
                    return result;

                ReplaceUnsafe(staged);
                return result;
            }
        }

        public WorldMemoryWriteResult<WorldMemoryEntity> AddEntity(
            WorldMemoryEntity entity)
        {
            lock (gate)
            {
                if (entity == null || !entity.IsValid)
                    return Invalid(entity, "Entity is invalid.");
                if (entities.ContainsKey(entity.EntityId))
                    return Duplicate(entity, "EntityId is already stored.");
                if (entity.MergedIntoEntityId.Length > 0 &&
                    !entities.ContainsKey(entity.MergedIntoEntityId))
                {
                    return Missing(entity,
                        "MergedIntoEntityId does not reference an Entity.");
                }

                entities.Add(entity.EntityId, entity);
                entityOrder.Add(entity);
                return Added(entity);
            }
        }

        public bool TryGetEntity(string entityId, out WorldMemoryEntity entity)
        {
            lock (gate)
                return entities.TryGetValue(Id(entityId), out entity);
        }

        public IReadOnlyList<WorldMemoryEntity> GetEntities()
        {
            lock (gate)
                return Copy(entityOrder);
        }

        public WorldMemoryWriteResult<WorldMemoryFact> AddFact(
            WorldMemoryFact fact)
        {
            lock (gate)
            {
                if (fact == null || !fact.IsValid)
                    return Invalid(fact, "Fact is invalid.");
                if (facts.ContainsKey(fact.FactId))
                    return Duplicate(fact, "FactId is already stored.");
                if (!entities.ContainsKey(fact.SubjectEntityId))
                    return Missing(fact,
                        "SubjectEntityId does not reference an Entity.");
                if (fact.ValueKind ==
                        WorldMemoryFactValueKind.EntityReference &&
                    !entities.ContainsKey(fact.ObjectEntityId))
                {
                    return Missing(fact,
                        "ObjectEntityId does not reference an Entity.");
                }
                if (fact.SupersededByFactId.Length > 0 &&
                    !facts.ContainsKey(fact.SupersededByFactId))
                {
                    return Missing(fact,
                        "SupersededByFactId does not reference a Fact.");
                }

                facts.Add(fact.FactId, fact);
                factOrder.Add(fact);
                return Added(fact);
            }
        }

        public bool TryGetFact(string factId, out WorldMemoryFact fact)
        {
            lock (gate)
                return facts.TryGetValue(Id(factId), out fact);
        }

        public IReadOnlyList<WorldMemoryFact> GetFactsBySubject(
            string subjectEntityId)
        {
            string id = Id(subjectEntityId);
            lock (gate)
            {
                return Select(factOrder, item =>
                    string.Equals(item.SubjectEntityId, id,
                        StringComparison.Ordinal));
            }
        }

        public IReadOnlyList<WorldMemoryFact> GetFactsBySubjectAndPredicate(
            string subjectEntityId,
            string predicate)
        {
            string id = Id(subjectEntityId);
            string key = Text(predicate);
            lock (gate)
            {
                return Select(factOrder, item =>
                    string.Equals(item.SubjectEntityId, id,
                        StringComparison.Ordinal) &&
                    string.Equals(item.Predicate, key,
                        StringComparison.Ordinal));
            }
        }

        public WorldMemoryWriteResult<WorldMemorySource> AddSource(
            WorldMemorySource source)
        {
            lock (gate)
            {
                if (source == null || !source.IsValid)
                    return Invalid(source, "Source is invalid.");
                if (sources.ContainsKey(source.SourceId))
                    return Duplicate(source, "SourceId is already stored.");

                sources.Add(source.SourceId, source);
                sourceOrder.Add(source);
                return Added(source);
            }
        }

        public bool TryGetSource(string sourceId, out WorldMemorySource source)
        {
            lock (gate)
                return sources.TryGetValue(Id(sourceId), out source);
        }

        public IReadOnlyList<WorldMemorySource> GetSources()
        {
            lock (gate)
                return Copy(sourceOrder);
        }

        public WorldMemoryWriteResult<WorldMemoryEvidence> AddEvidence(
            WorldMemoryEvidence item)
        {
            lock (gate)
            {
                if (item == null || !item.IsValid)
                    return Invalid(item, "Evidence is invalid.");
                if (evidence.ContainsKey(item.EvidenceId))
                    return Duplicate(item, "EvidenceId is already stored.");
                if (!facts.ContainsKey(item.FactId))
                    return Missing(item,
                        "FactId does not reference a Fact.");
                if (item.SourceId.Length > 0 &&
                    !sources.ContainsKey(item.SourceId))
                {
                    return Missing(item,
                        "SourceId does not reference a Source.");
                }
                if (item.AttributedSourceId.Length > 0 &&
                    !sources.ContainsKey(item.AttributedSourceId))
                {
                    return Missing(item,
                        "AttributedSourceId does not reference a Source.");
                }
                if (item.ObservationId.Length > 0 &&
                    !observations.ContainsKey(item.ObservationId))
                {
                    return Missing(item,
                        "ObservationId does not reference an Observation.");
                }
                if (item.RecognitionEvidenceId.Length > 0 &&
                    !recognitionEvidence.ContainsKey(
                        item.RecognitionEvidenceId))
                {
                    return Missing(item,
                        "RecognitionEvidenceId does not reference evidence.");
                }

                evidence.Add(item.EvidenceId, item);
                evidenceOrder.Add(item);
                return Added(item);
            }
        }

        public bool TryGetEvidence(
            string evidenceId,
            out WorldMemoryEvidence item)
        {
            lock (gate)
                return evidence.TryGetValue(Id(evidenceId), out item);
        }

        public IReadOnlyList<WorldMemoryEvidence> GetEvidenceByFact(
            string factId)
        {
            string id = Id(factId);
            lock (gate)
            {
                return Select(evidenceOrder, item =>
                    string.Equals(item.FactId, id, StringComparison.Ordinal));
            }
        }

        public WorldMemoryWriteResult<WorldMemoryObservation> AddObservation(
            WorldMemoryObservation observation)
        {
            lock (gate)
            {
                if (observation == null || !observation.IsValid)
                    return Invalid(observation, "Observation is invalid.");
                if (observations.ContainsKey(observation.ObservationId))
                {
                    return Duplicate(observation,
                        "ObservationId is already stored.");
                }
                if (observation.SourceId.Length > 0 &&
                    !sources.ContainsKey(observation.SourceId))
                {
                    return Missing(observation,
                        "SourceId does not reference a Source.");
                }

                observations.Add(observation.ObservationId, observation);
                observationOrder.Add(observation);
                return Added(observation);
            }
        }

        public bool TryGetObservation(
            string observationId,
            out WorldMemoryObservation observation)
        {
            lock (gate)
                return observations.TryGetValue(Id(observationId),
                    out observation);
        }

        public IReadOnlyList<WorldMemoryObservation> GetObservations()
        {
            lock (gate)
                return Copy(observationOrder);
        }

        public WorldMemoryWriteResult<WorldMemoryRecognitionEvidence>
            AddRecognitionEvidence(WorldMemoryRecognitionEvidence item)
        {
            lock (gate)
            {
                if (item == null || !item.IsValid)
                    return Invalid(item, "RecognitionEvidence is invalid.");
                if (recognitionEvidence.ContainsKey(
                        item.RecognitionEvidenceId))
                {
                    return Duplicate(item,
                        "RecognitionEvidenceId is already stored.");
                }
                if (!observations.ContainsKey(item.ObservationId))
                {
                    return Missing(item,
                        "ObservationId does not reference an Observation.");
                }
                if (item.CandidateEntityId.Length > 0 &&
                    !entities.ContainsKey(item.CandidateEntityId))
                {
                    return Missing(item,
                        "CandidateEntityId does not reference an Entity.");
                }

                recognitionEvidence.Add(item.RecognitionEvidenceId, item);
                recognitionEvidenceOrder.Add(item);
                return Added(item);
            }
        }

        public bool TryGetRecognitionEvidence(
            string recognitionEvidenceId,
            out WorldMemoryRecognitionEvidence item)
        {
            lock (gate)
            {
                return recognitionEvidence.TryGetValue(
                    Id(recognitionEvidenceId), out item);
            }
        }

        public IReadOnlyList<WorldMemoryRecognitionEvidence>
            GetRecognitionEvidenceByObservation(string observationId)
        {
            string id = Id(observationId);
            lock (gate)
            {
                return Select(recognitionEvidenceOrder, item =>
                    string.Equals(item.ObservationId, id,
                        StringComparison.Ordinal));
            }
        }

        public WorldMemoryWriteResult<WorldMemoryEntityResolution>
            AddEntityResolution(WorldMemoryEntityResolution resolution)
        {
            lock (gate)
            {
                if (resolution == null || !resolution.IsValid)
                    return Invalid(resolution, "EntityResolution is invalid.");
                if (resolutions.ContainsKey(resolution.ResolutionId))
                    return Duplicate(resolution,
                        "ResolutionId is already stored.");
                if (!observations.ContainsKey(resolution.ObservationId))
                    return Missing(resolution,
                        "ObservationId does not reference an Observation.");
                if (resolution.EntityId.Length > 0 &&
                    !entities.ContainsKey(resolution.EntityId))
                {
                    return Missing(resolution,
                        "EntityId does not reference an Entity.");
                }

                for (int i = 0;
                     i < resolution.SupportingRecognitionEvidenceIds.Count;
                     i++)
                {
                    string id =
                        resolution.SupportingRecognitionEvidenceIds[i];
                    if (!recognitionEvidence.TryGetValue(
                            id,
                            out WorldMemoryRecognitionEvidence supporting))
                    {
                        return Missing(resolution,
                            "Supporting RecognitionEvidence is missing.");
                    }
                    if (!string.Equals(
                            supporting.ObservationId,
                            resolution.ObservationId,
                            StringComparison.Ordinal))
                    {
                        return Mismatch(resolution,
                            "Supporting RecognitionEvidence belongs to another Observation.");
                    }
                }

                resolutions.Add(resolution.ResolutionId, resolution);
                resolutionOrder.Add(resolution);
                return Added(resolution);
            }
        }

        public bool TryGetEntityResolution(
            string resolutionId,
            out WorldMemoryEntityResolution resolution)
        {
            lock (gate)
                return resolutions.TryGetValue(Id(resolutionId), out resolution);
        }

        public IReadOnlyList<WorldMemoryEntityResolution>
            GetEntityResolutionsByObservation(string observationId)
        {
            string id = Id(observationId);
            lock (gate)
            {
                return Select(resolutionOrder, item =>
                    string.Equals(item.ObservationId, id,
                        StringComparison.Ordinal));
            }
        }

        public bool TryGetCurrentEntityResolution(
            string observationId,
            out WorldMemoryEntityResolution resolution)
        {
            string id = Id(observationId);
            lock (gate)
            {
                for (int i = resolutionOrder.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(
                            resolutionOrder[i].ObservationId,
                            id,
                            StringComparison.Ordinal))
                    {
                        resolution = resolutionOrder[i];
                        return true;
                    }
                }

                resolution = null;
                return false;
            }
        }

        private static string Id(string value)
        {
            return value == null ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static string Text(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>(new List<T>(values));
        }

        private static IReadOnlyList<T> Select<T>(
            IEnumerable<T> values,
            Func<T, bool> predicate)
        {
            var result = new List<T>();
            foreach (T value in values)
            {
                if (predicate(value))
                    result.Add(value);
            }

            return new ReadOnlyCollection<T>(result);
        }

        private static WorldMemoryWriteResult<T> Added<T>(T record)
            where T : class
        {
            return new WorldMemoryWriteResult<T>(
                WorldMemoryWriteStatus.Added, record, string.Empty);
        }

        private static WorldMemoryWriteResult<T> Invalid<T>(
            T record,
            string error) where T : class
        {
            return new WorldMemoryWriteResult<T>(
                WorldMemoryWriteStatus.InvalidRecord, record, error);
        }

        private static WorldMemoryWriteResult<T> Duplicate<T>(
            T record,
            string error) where T : class
        {
            return new WorldMemoryWriteResult<T>(
                WorldMemoryWriteStatus.DuplicateId, record, error);
        }

        private static WorldMemoryWriteResult<T> Missing<T>(
            T record,
            string error) where T : class
        {
            return new WorldMemoryWriteResult<T>(
                WorldMemoryWriteStatus.MissingReference, record, error);
        }

        private static WorldMemoryWriteResult<T> Mismatch<T>(
            T record,
            string error) where T : class
        {
            return new WorldMemoryWriteResult<T>(
                WorldMemoryWriteStatus.ReferenceMismatch, record, error);
        }

        private WorldMemoryAtomicMutationResult ApplyToStagedStore(
            WorldMemoryAtomicMutation mutation)
        {
            for (int i = 0; i < mutation.Entities.Count; i++)
            {
                WorldMemoryWriteResult<WorldMemoryEntity> result =
                    AddEntity(mutation.Entities[i]);
                if (!result.Succeeded)
                {
                    return AtomicFailure(result.Status, mutation.Kind,
                        WorldMemoryAtomicMutationComponent.Entity,
                        i, result.Error);
                }
            }

            for (int i = 0; i < mutation.Sources.Count; i++)
            {
                WorldMemoryWriteResult<WorldMemorySource> result =
                    AddSource(mutation.Sources[i]);
                if (!result.Succeeded)
                {
                    return AtomicFailure(result.Status, mutation.Kind,
                        WorldMemoryAtomicMutationComponent.Source,
                        i, result.Error);
                }
            }

            for (int i = 0; i < mutation.Observations.Count; i++)
            {
                WorldMemoryWriteResult<WorldMemoryObservation> result =
                    AddObservation(mutation.Observations[i]);
                if (!result.Succeeded)
                {
                    return AtomicFailure(result.Status, mutation.Kind,
                        WorldMemoryAtomicMutationComponent.Observation,
                        i, result.Error);
                }
            }

            for (int i = 0;
                 i < mutation.RecognitionEvidence.Count;
                 i++)
            {
                WorldMemoryWriteResult<WorldMemoryRecognitionEvidence>
                    result = AddRecognitionEvidence(
                        mutation.RecognitionEvidence[i]);
                if (!result.Succeeded)
                {
                    return AtomicFailure(result.Status, mutation.Kind,
                        WorldMemoryAtomicMutationComponent
                            .RecognitionEvidence,
                        i, result.Error);
                }
            }

            for (int i = 0; i < mutation.Facts.Count; i++)
            {
                WorldMemoryWriteResult<WorldMemoryFact> result =
                    AddFact(mutation.Facts[i]);
                if (!result.Succeeded)
                {
                    return AtomicFailure(result.Status, mutation.Kind,
                        WorldMemoryAtomicMutationComponent.Fact,
                        i, result.Error);
                }
            }

            for (int i = 0; i < mutation.Evidence.Count; i++)
            {
                WorldMemoryWriteResult<WorldMemoryEvidence> result =
                    AddEvidence(mutation.Evidence[i]);
                if (!result.Succeeded)
                {
                    return AtomicFailure(result.Status, mutation.Kind,
                        WorldMemoryAtomicMutationComponent.Evidence,
                        i, result.Error);
                }
            }

            for (int i = 0;
                 i < mutation.EntityResolutions.Count;
                 i++)
            {
                WorldMemoryWriteResult<WorldMemoryEntityResolution> result =
                    AddEntityResolution(mutation.EntityResolutions[i]);
                if (!result.Succeeded)
                {
                    return AtomicFailure(result.Status, mutation.Kind,
                        WorldMemoryAtomicMutationComponent.EntityResolution,
                        i, result.Error);
                }
            }

            return WorldMemoryAtomicMutationResult.Success(mutation.Kind);
        }

        private WorldMemoryAtomicMutationResult ValidateExistingReferences(
            WorldMemoryAtomicMutation mutation)
        {
            for (int i = 0; i < mutation.ExistingReferences.Count; i++)
            {
                WorldMemoryAtomicExistingReference reference =
                    mutation.ExistingReferences[i];
                if (reference == null || reference.RecordId.Length == 0 ||
                    reference.Component ==
                        WorldMemoryAtomicMutationComponent.None)
                {
                    return AtomicFailure(
                        WorldMemoryWriteStatus.InvalidRecord,
                        mutation.Kind,
                        reference == null
                            ? WorldMemoryAtomicMutationComponent.None
                            : reference.Component,
                        i,
                        "Existing reference is invalid.");
                }

                if (!ExistingReferenceExists(reference))
                {
                    return AtomicFailure(
                        WorldMemoryWriteStatus.MissingReference,
                        mutation.Kind,
                        reference.Component,
                        i,
                        "Required existing record is missing.");
                }
            }
            return WorldMemoryAtomicMutationResult.Success(mutation.Kind);
        }

        private bool ExistingReferenceExists(
            WorldMemoryAtomicExistingReference reference)
        {
            switch (reference.Component)
            {
                case WorldMemoryAtomicMutationComponent.Entity:
                    return entities.ContainsKey(reference.RecordId);
                case WorldMemoryAtomicMutationComponent.Source:
                    return sources.ContainsKey(reference.RecordId);
                case WorldMemoryAtomicMutationComponent.Observation:
                    return observations.ContainsKey(reference.RecordId);
                case WorldMemoryAtomicMutationComponent.RecognitionEvidence:
                    return recognitionEvidence.ContainsKey(reference.RecordId);
                case WorldMemoryAtomicMutationComponent.Fact:
                    return facts.ContainsKey(reference.RecordId);
                case WorldMemoryAtomicMutationComponent.Evidence:
                    return evidence.ContainsKey(reference.RecordId);
                case WorldMemoryAtomicMutationComponent.EntityResolution:
                    return resolutions.ContainsKey(reference.RecordId);
                default:
                    return false;
            }
        }

        private static WorldMemoryAtomicMutationResult ValidateMutationShape(
            WorldMemoryAtomicMutation mutation)
        {
            bool valid;
            switch (mutation.Kind)
            {
                case WorldMemoryAtomicMutationKind.EntityPromotion:
                    valid = mutation.Entities.Count == 1 &&
                        mutation.Sources.Count == 0 &&
                        mutation.Observations.Count == 0 &&
                        mutation.RecognitionEvidence.Count == 0 &&
                        mutation.Facts.Count == 0 &&
                        mutation.Evidence.Count == 0 &&
                        mutation.EntityResolutions.Count == 1;
                    break;
                case WorldMemoryAtomicMutationKind.ExperienceProjection:
                    valid = mutation.Entities.Count == 0 &&
                        mutation.Sources.Count == 0 &&
                        mutation.Observations.Count == 0 &&
                        mutation.RecognitionEvidence.Count == 0 &&
                        mutation.Facts.Count <= 1 &&
                        mutation.Evidence.Count == 1 &&
                        mutation.EntityResolutions.Count == 0 &&
                        HasMatchingFactShape(mutation);
                    break;
                case WorldMemoryAtomicMutationKind.WebAssertion:
                    valid = mutation.Entities.Count == 0 &&
                        mutation.Sources.Count <= 1 &&
                        mutation.Observations.Count == 0 &&
                        mutation.RecognitionEvidence.Count == 0 &&
                        mutation.Facts.Count <= 1 &&
                        mutation.Evidence.Count == 1 &&
                        mutation.EntityResolutions.Count == 0 &&
                        HasMatchingFactShape(mutation) &&
                        HasMatchingSourceShape(mutation);
                    break;
                case WorldMemoryAtomicMutationKind.ObservationResolution:
                    valid = mutation.Entities.Count == 0 &&
                        mutation.Sources.Count == 0 &&
                        mutation.Observations.Count <= 1 &&
                        mutation.Facts.Count == 0 &&
                        mutation.Evidence.Count == 0 &&
                        mutation.EntityResolutions.Count == 1 &&
                        HasMatchingObservationShape(mutation);
                    break;
                default:
                    valid = false;
                    break;
            }

            return valid
                ? WorldMemoryAtomicMutationResult.Success(mutation.Kind)
                : AtomicFailure(
                    WorldMemoryWriteStatus.InvalidRecord,
                    mutation.Kind,
                    WorldMemoryAtomicMutationComponent.None,
                    -1,
                    "Atomic mutation shape is invalid for its kind.");
        }

        private static bool HasMatchingFactShape(
            WorldMemoryAtomicMutation mutation)
        {
            if (mutation.Evidence.Count != 1 ||
                mutation.Evidence[0] == null)
                return true;

            string factId = mutation.Evidence[0].FactId;
            if (mutation.Facts.Count == 1)
            {
                return mutation.Facts[0] == null ||
                    string.Equals(mutation.Facts[0].FactId, factId,
                        StringComparison.Ordinal);
            }
            return HasExistingReference(mutation,
                WorldMemoryAtomicMutationComponent.Fact, factId);
        }

        private static bool HasMatchingSourceShape(
            WorldMemoryAtomicMutation mutation)
        {
            if (mutation.Evidence.Count != 1 ||
                mutation.Evidence[0] == null)
                return true;

            WorldMemoryEvidence item = mutation.Evidence[0];
            string sourceId = item.SourceId.Length > 0
                ? item.SourceId : item.AttributedSourceId;
            if (sourceId.Length == 0)
                return false;
            if (mutation.Sources.Count == 1)
            {
                return mutation.Sources[0] == null ||
                    string.Equals(mutation.Sources[0].SourceId, sourceId,
                        StringComparison.Ordinal);
            }
            return HasExistingReference(mutation,
                WorldMemoryAtomicMutationComponent.Source, sourceId);
        }

        private static bool HasMatchingObservationShape(
            WorldMemoryAtomicMutation mutation)
        {
            if (mutation.EntityResolutions.Count != 1 ||
                mutation.EntityResolutions[0] == null)
                return true;

            string observationId =
                mutation.EntityResolutions[0].ObservationId;
            if (mutation.Observations.Count == 1)
            {
                if (mutation.Observations[0] != null &&
                    !string.Equals(
                        mutation.Observations[0].ObservationId,
                        observationId,
                        StringComparison.Ordinal))
                    return false;
            }
            else if (!HasExistingReference(mutation,
                WorldMemoryAtomicMutationComponent.Observation,
                observationId))
            {
                return false;
            }

            for (int i = 0; i < mutation.RecognitionEvidence.Count; i++)
            {
                WorldMemoryRecognitionEvidence item =
                    mutation.RecognitionEvidence[i];
                if (item != null && !string.Equals(
                    item.ObservationId, observationId,
                    StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static bool HasExistingReference(
            WorldMemoryAtomicMutation mutation,
            WorldMemoryAtomicMutationComponent component,
            string recordId)
        {
            for (int i = 0; i < mutation.ExistingReferences.Count; i++)
            {
                WorldMemoryAtomicExistingReference reference =
                    mutation.ExistingReferences[i];
                if (reference != null && reference.Component == component &&
                    string.Equals(reference.RecordId, recordId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private InMemoryWorldMemoryStore CloneUnsafe()
        {
            var copy = new InMemoryWorldMemoryStore();
            for (int i = 0; i < entityOrder.Count; i++)
                Require(copy.AddEntity(entityOrder[i]));
            for (int i = 0; i < sourceOrder.Count; i++)
                Require(copy.AddSource(sourceOrder[i]));
            for (int i = 0; i < observationOrder.Count; i++)
                Require(copy.AddObservation(observationOrder[i]));
            for (int i = 0; i < recognitionEvidenceOrder.Count; i++)
                Require(copy.AddRecognitionEvidence(
                    recognitionEvidenceOrder[i]));
            for (int i = 0; i < factOrder.Count; i++)
                Require(copy.AddFact(factOrder[i]));
            for (int i = 0; i < evidenceOrder.Count; i++)
                Require(copy.AddEvidence(evidenceOrder[i]));
            for (int i = 0; i < resolutionOrder.Count; i++)
                Require(copy.AddEntityResolution(resolutionOrder[i]));
            return copy;
        }

        private void ReplaceUnsafe(InMemoryWorldMemoryStore staged)
        {
            entities.Clear();
            entityOrder.Clear();
            foreach (KeyValuePair<string, WorldMemoryEntity> item
                     in staged.entities)
            {
                entities.Add(item.Key, item.Value);
            }
            entityOrder.AddRange(staged.entityOrder);

            facts.Clear();
            factOrder.Clear();
            foreach (KeyValuePair<string, WorldMemoryFact> item
                     in staged.facts)
            {
                facts.Add(item.Key, item.Value);
            }
            factOrder.AddRange(staged.factOrder);

            sources.Clear();
            sourceOrder.Clear();
            foreach (KeyValuePair<string, WorldMemorySource> item
                     in staged.sources)
            {
                sources.Add(item.Key, item.Value);
            }
            sourceOrder.AddRange(staged.sourceOrder);

            evidence.Clear();
            evidenceOrder.Clear();
            foreach (KeyValuePair<string, WorldMemoryEvidence> item
                     in staged.evidence)
            {
                evidence.Add(item.Key, item.Value);
            }
            evidenceOrder.AddRange(staged.evidenceOrder);

            observations.Clear();
            observationOrder.Clear();
            foreach (KeyValuePair<string, WorldMemoryObservation> item
                     in staged.observations)
            {
                observations.Add(item.Key, item.Value);
            }
            observationOrder.AddRange(staged.observationOrder);

            recognitionEvidence.Clear();
            recognitionEvidenceOrder.Clear();
            foreach (KeyValuePair<string, WorldMemoryRecognitionEvidence>
                     item in staged.recognitionEvidence)
            {
                recognitionEvidence.Add(item.Key, item.Value);
            }
            recognitionEvidenceOrder.AddRange(
                staged.recognitionEvidenceOrder);

            resolutions.Clear();
            resolutionOrder.Clear();
            foreach (KeyValuePair<string, WorldMemoryEntityResolution> item
                     in staged.resolutions)
            {
                resolutions.Add(item.Key, item.Value);
            }
            resolutionOrder.AddRange(staged.resolutionOrder);
        }

        private static void Require<T>(WorldMemoryWriteResult<T> result)
            where T : class
        {
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Existing InMemoryWorldMemoryStore state is invalid: " +
                    result.Error);
            }
        }

        private static WorldMemoryAtomicMutationResult AtomicFailure(
            WorldMemoryWriteStatus status,
            WorldMemoryAtomicMutationKind kind,
            WorldMemoryAtomicMutationComponent component,
            int componentIndex,
            string error)
        {
            return WorldMemoryAtomicMutationResult.RecordFailure(
                status, kind, component, componentIndex, error);
        }
    }
}
