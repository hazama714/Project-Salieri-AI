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
    public enum WorldMemoryAtomicMutationKind
    {
        Unknown = 0,
        EntityPromotion = 1,
        ExperienceProjection = 2,
        WebAssertion = 3,
        ObservationResolution = 4
    }

    public enum WorldMemoryAtomicMutationComponent
    {
        None = 0,
        Entity = 1,
        Source = 2,
        Observation = 3,
        RecognitionEvidence = 4,
        Fact = 5,
        Evidence = 6,
        EntityResolution = 7
    }

    public enum WorldMemoryAtomicMutationStatus
    {
        Succeeded = 0,
        DomainValidationFailure = 1,
        DuplicateConflict = 2,
        MissingReference = 3,
        ReferenceMismatch = 4,
        PersistenceIOFailure = 5,
        TransactionBeginFailure = 6,
        CommitFailure = 7,
        RollbackFailure = 8,
        TimeoutOrContention = 9,
        ProviderUnavailable = 10
    }

    public enum WorldMemoryAtomicRollbackStatus
    {
        NotRequired = 0,
        Succeeded = 1,
        Failed = 2,
        NotAttempted = 3
    }

    public sealed class WorldMemoryAtomicExistingReference
    {
        public WorldMemoryAtomicMutationComponent Component { get; }
        public string RecordId { get; }

        public WorldMemoryAtomicExistingReference(
            WorldMemoryAtomicMutationComponent component,
            string recordId)
        {
            Component = component;
            RecordId = (recordId ?? string.Empty).Trim();
        }
    }

    /// <summary>
    /// Storage-independent aggregate for the four compound write units frozen
    /// by DB4. ExistingReferences identify pre-committed parents required by
    /// partial-new aggregates; no provider transaction mechanics are exposed.
    /// </summary>
    public sealed class WorldMemoryAtomicMutation
    {
        public WorldMemoryAtomicMutationKind Kind { get; }
        public IReadOnlyList<WorldMemoryAtomicExistingReference>
            ExistingReferences { get; }
        public IReadOnlyList<WorldMemoryEntity> Entities { get; }
        public IReadOnlyList<WorldMemorySource> Sources { get; }
        public IReadOnlyList<WorldMemoryObservation> Observations { get; }
        public IReadOnlyList<WorldMemoryRecognitionEvidence>
            RecognitionEvidence { get; }
        public IReadOnlyList<WorldMemoryFact> Facts { get; }
        public IReadOnlyList<WorldMemoryEvidence> Evidence { get; }
        public IReadOnlyList<WorldMemoryEntityResolution> EntityResolutions
        { get; }

        private WorldMemoryAtomicMutation(
            WorldMemoryAtomicMutationKind kind,
            IEnumerable<WorldMemoryAtomicExistingReference> references,
            IEnumerable<WorldMemoryEntity> entities,
            IEnumerable<WorldMemorySource> sources,
            IEnumerable<WorldMemoryObservation> observations,
            IEnumerable<WorldMemoryRecognitionEvidence> recognition,
            IEnumerable<WorldMemoryFact> facts,
            IEnumerable<WorldMemoryEvidence> evidence,
            IEnumerable<WorldMemoryEntityResolution> resolutions)
        {
            Kind = kind;
            ExistingReferences = Copy(references);
            Entities = Copy(entities);
            Sources = Copy(sources);
            Observations = Copy(observations);
            RecognitionEvidence = Copy(recognition);
            Facts = Copy(facts);
            Evidence = Copy(evidence);
            EntityResolutions = Copy(resolutions);
        }

        public static WorldMemoryAtomicMutation ForEntityPromotion(
            WorldMemoryEntity entity,
            WorldMemoryEntityResolution resolution)
        {
            return Create(WorldMemoryAtomicMutationKind.EntityPromotion,
                Empty<WorldMemoryAtomicExistingReference>(), One(entity),
                Empty<WorldMemorySource>(), Empty<WorldMemoryObservation>(),
                Empty<WorldMemoryRecognitionEvidence>(),
                Empty<WorldMemoryFact>(), Empty<WorldMemoryEvidence>(),
                One(resolution));
        }

        public static WorldMemoryAtomicMutation ForExperienceProjection(
            WorldMemoryFact fact,
            WorldMemoryEvidence evidence)
        {
            return Create(WorldMemoryAtomicMutationKind.ExperienceProjection,
                Empty<WorldMemoryAtomicExistingReference>(),
                Empty<WorldMemoryEntity>(), Empty<WorldMemorySource>(),
                Empty<WorldMemoryObservation>(),
                Empty<WorldMemoryRecognitionEvidence>(), One(fact),
                One(evidence), Empty<WorldMemoryEntityResolution>());
        }

        public static WorldMemoryAtomicMutation
            ForExperienceProjectionWithExistingFact(
                string existingFactId,
                WorldMemoryEvidence evidence)
        {
            return Create(WorldMemoryAtomicMutationKind.ExperienceProjection,
                One(Reference(WorldMemoryAtomicMutationComponent.Fact,
                    existingFactId)), Empty<WorldMemoryEntity>(),
                Empty<WorldMemorySource>(), Empty<WorldMemoryObservation>(),
                Empty<WorldMemoryRecognitionEvidence>(),
                Empty<WorldMemoryFact>(), One(evidence),
                Empty<WorldMemoryEntityResolution>());
        }

        public static WorldMemoryAtomicMutation ForWebAssertion(
            WorldMemorySource source,
            WorldMemoryFact fact,
            WorldMemoryEvidence evidence)
        {
            return Create(WorldMemoryAtomicMutationKind.WebAssertion,
                Empty<WorldMemoryAtomicExistingReference>(),
                Empty<WorldMemoryEntity>(), One(source),
                Empty<WorldMemoryObservation>(),
                Empty<WorldMemoryRecognitionEvidence>(), One(fact),
                One(evidence), Empty<WorldMemoryEntityResolution>());
        }

        public static WorldMemoryAtomicMutation
            ForWebAssertionWithExistingFact(
                string existingFactId,
                WorldMemorySource source,
                WorldMemoryEvidence evidence)
        {
            return Create(WorldMemoryAtomicMutationKind.WebAssertion,
                One(Reference(WorldMemoryAtomicMutationComponent.Fact,
                    existingFactId)), Empty<WorldMemoryEntity>(), One(source),
                Empty<WorldMemoryObservation>(),
                Empty<WorldMemoryRecognitionEvidence>(),
                Empty<WorldMemoryFact>(), One(evidence),
                Empty<WorldMemoryEntityResolution>());
        }

        public static WorldMemoryAtomicMutation
            ForWebAssertionWithExistingSource(
                string existingSourceId,
                WorldMemoryFact fact,
                WorldMemoryEvidence evidence)
        {
            return Create(WorldMemoryAtomicMutationKind.WebAssertion,
                One(Reference(WorldMemoryAtomicMutationComponent.Source,
                    existingSourceId)), Empty<WorldMemoryEntity>(),
                Empty<WorldMemorySource>(), Empty<WorldMemoryObservation>(),
                Empty<WorldMemoryRecognitionEvidence>(), One(fact),
                One(evidence), Empty<WorldMemoryEntityResolution>());
        }

        public static WorldMemoryAtomicMutation
            ForWebAssertionWithExistingSourceAndFact(
                string existingSourceId,
                string existingFactId,
                WorldMemoryEvidence evidence)
        {
            return Create(WorldMemoryAtomicMutationKind.WebAssertion,
                new[]
                {
                    Reference(WorldMemoryAtomicMutationComponent.Source,
                        existingSourceId),
                    Reference(WorldMemoryAtomicMutationComponent.Fact,
                        existingFactId)
                }, Empty<WorldMemoryEntity>(), Empty<WorldMemorySource>(),
                Empty<WorldMemoryObservation>(),
                Empty<WorldMemoryRecognitionEvidence>(),
                Empty<WorldMemoryFact>(), One(evidence),
                Empty<WorldMemoryEntityResolution>());
        }

        public static WorldMemoryAtomicMutation ForObservationResolution(
            WorldMemoryObservation observation,
            IEnumerable<WorldMemoryRecognitionEvidence> recognition,
            WorldMemoryEntityResolution resolution)
        {
            return Create(WorldMemoryAtomicMutationKind.ObservationResolution,
                ExistingSupportingReferences(recognition, resolution),
                Empty<WorldMemoryEntity>(), Empty<WorldMemorySource>(),
                One(observation), recognition, Empty<WorldMemoryFact>(),
                Empty<WorldMemoryEvidence>(), One(resolution));
        }

        public static WorldMemoryAtomicMutation
            ForObservationResolutionWithExistingObservation(
                string existingObservationId,
                IEnumerable<WorldMemoryRecognitionEvidence> recognition,
                WorldMemoryEntityResolution resolution)
        {
            var references = new List<WorldMemoryAtomicExistingReference>
            {
                Reference(WorldMemoryAtomicMutationComponent.Observation,
                    existingObservationId)
            };
            references.AddRange(ExistingSupportingReferences(
                recognition, resolution));
            return Create(WorldMemoryAtomicMutationKind.ObservationResolution,
                references, Empty<WorldMemoryEntity>(),
                Empty<WorldMemorySource>(), Empty<WorldMemoryObservation>(),
                recognition, Empty<WorldMemoryFact>(),
                Empty<WorldMemoryEvidence>(), One(resolution));
        }

        private static WorldMemoryAtomicMutation Create(
            WorldMemoryAtomicMutationKind kind,
            IEnumerable<WorldMemoryAtomicExistingReference> references,
            IEnumerable<WorldMemoryEntity> entities,
            IEnumerable<WorldMemorySource> sources,
            IEnumerable<WorldMemoryObservation> observations,
            IEnumerable<WorldMemoryRecognitionEvidence> recognition,
            IEnumerable<WorldMemoryFact> facts,
            IEnumerable<WorldMemoryEvidence> evidence,
            IEnumerable<WorldMemoryEntityResolution> resolutions)
        {
            return new WorldMemoryAtomicMutation(kind, references, entities,
                sources, observations, recognition, facts, evidence,
                resolutions);
        }

        private static IEnumerable<WorldMemoryAtomicExistingReference>
            ExistingSupportingReferences(
                IEnumerable<WorldMemoryRecognitionEvidence> newEvidence,
                WorldMemoryEntityResolution resolution)
        {
            var newIds = new HashSet<string>(StringComparer.Ordinal);
            if (newEvidence != null)
            {
                foreach (WorldMemoryRecognitionEvidence item in newEvidence)
                {
                    if (item != null)
                        newIds.Add(item.RecognitionEvidenceId);
                }
            }

            var result = new List<WorldMemoryAtomicExistingReference>();
            if (resolution == null)
                return result;
            for (int i = 0;
                 i < resolution.SupportingRecognitionEvidenceIds.Count;
                 i++)
            {
                string id = resolution.SupportingRecognitionEvidenceIds[i];
                if (!newIds.Contains(id))
                {
                    result.Add(Reference(
                        WorldMemoryAtomicMutationComponent
                            .RecognitionEvidence, id));
                }
            }
            return result;
        }

        private static WorldMemoryAtomicExistingReference Reference(
            WorldMemoryAtomicMutationComponent component,
            string recordId)
        {
            return new WorldMemoryAtomicExistingReference(component, recordId);
        }

        private static IEnumerable<T> One<T>(T value) => new[] { value };
        private static IEnumerable<T> Empty<T>() => Array.Empty<T>();

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>(values == null
                ? new List<T>() : new List<T>(values));
        }
    }

    /// <summary>
    /// The effective Status follows deterministic precedence. If rollback
    /// fails it is RollbackFailure, while PrimaryFailureStatus retains the
    /// original write/commit failure and StateIntegrityCertain becomes false.
    /// </summary>
    public sealed class WorldMemoryAtomicMutationResult
    {
        public WorldMemoryAtomicMutationStatus Status { get; }
        public WorldMemoryAtomicMutationStatus PrimaryFailureStatus { get; }
        public WorldMemoryWriteStatus? WriteStatus { get; }
        public WorldMemoryAtomicRollbackStatus RollbackStatus { get; }
        public WorldMemoryAtomicMutationKind MutationKind { get; }
        public WorldMemoryAtomicMutationComponent FailedComponent { get; }
        public int FailedComponentIndex { get; }
        public string Error { get; }
        public string RollbackError { get; }
        public bool Succeeded =>
            Status == WorldMemoryAtomicMutationStatus.Succeeded;
        public bool StateIntegrityCertain =>
            RollbackStatus != WorldMemoryAtomicRollbackStatus.Failed &&
            RollbackStatus != WorldMemoryAtomicRollbackStatus.NotAttempted;

        private WorldMemoryAtomicMutationResult(
            WorldMemoryAtomicMutationStatus status,
            WorldMemoryAtomicMutationStatus primaryFailureStatus,
            WorldMemoryWriteStatus? writeStatus,
            WorldMemoryAtomicRollbackStatus rollbackStatus,
            WorldMemoryAtomicMutationKind mutationKind,
            WorldMemoryAtomicMutationComponent failedComponent,
            int failedComponentIndex,
            string error,
            string rollbackError)
        {
            Status = status;
            PrimaryFailureStatus = primaryFailureStatus;
            WriteStatus = writeStatus;
            RollbackStatus = rollbackStatus;
            MutationKind = mutationKind;
            FailedComponent = failedComponent;
            FailedComponentIndex = failedComponentIndex;
            Error = error ?? string.Empty;
            RollbackError = rollbackError ?? string.Empty;
        }

        public static WorldMemoryAtomicMutationResult Success(
            WorldMemoryAtomicMutationKind mutationKind)
        {
            return new WorldMemoryAtomicMutationResult(
                WorldMemoryAtomicMutationStatus.Succeeded,
                WorldMemoryAtomicMutationStatus.Succeeded,
                WorldMemoryWriteStatus.Added,
                WorldMemoryAtomicRollbackStatus.NotRequired,
                mutationKind, WorldMemoryAtomicMutationComponent.None, -1,
                string.Empty, string.Empty);
        }

        public static WorldMemoryAtomicMutationResult RecordFailure(
            WorldMemoryWriteStatus writeStatus,
            WorldMemoryAtomicMutationKind mutationKind,
            WorldMemoryAtomicMutationComponent failedComponent,
            int failedComponentIndex,
            string error)
        {
            if (writeStatus == WorldMemoryWriteStatus.Added)
            {
                throw new ArgumentOutOfRangeException(nameof(writeStatus),
                    "A record failure status is required.");
            }
            WorldMemoryAtomicMutationStatus status = Map(writeStatus);
            return new WorldMemoryAtomicMutationResult(status, status,
                writeStatus, WorldMemoryAtomicRollbackStatus.NotRequired,
                mutationKind, failedComponent, failedComponentIndex, error,
                string.Empty);
        }

        public static WorldMemoryAtomicMutationResult StorageFailure(
            WorldMemoryAtomicMutationStatus primaryFailureStatus,
            WorldMemoryAtomicMutationKind mutationKind,
            string error,
            WorldMemoryAtomicRollbackStatus rollbackStatus =
                WorldMemoryAtomicRollbackStatus.NotRequired,
            string rollbackError = "")
        {
            if (!IsStorageFailure(primaryFailureStatus))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(primaryFailureStatus),
                    "A provider-neutral storage failure is required.");
            }
            if (primaryFailureStatus ==
                    WorldMemoryAtomicMutationStatus.CommitFailure &&
                rollbackStatus ==
                    WorldMemoryAtomicRollbackStatus.NotRequired)
            {
                throw new ArgumentException(
                    "Commit failure requires an explicit rollback outcome.",
                    nameof(rollbackStatus));
            }

            WorldMemoryAtomicMutationStatus status =
                rollbackStatus == WorldMemoryAtomicRollbackStatus.Failed
                    ? WorldMemoryAtomicMutationStatus.RollbackFailure
                    : primaryFailureStatus;
            return new WorldMemoryAtomicMutationResult(status,
                primaryFailureStatus, null, rollbackStatus, mutationKind,
                WorldMemoryAtomicMutationComponent.None, -1, error,
                rollbackError);
        }

        private static WorldMemoryAtomicMutationStatus Map(
            WorldMemoryWriteStatus status)
        {
            switch (status)
            {
                case WorldMemoryWriteStatus.Added:
                    return WorldMemoryAtomicMutationStatus.Succeeded;
                case WorldMemoryWriteStatus.DuplicateId:
                    return WorldMemoryAtomicMutationStatus.DuplicateConflict;
                case WorldMemoryWriteStatus.MissingReference:
                    return WorldMemoryAtomicMutationStatus.MissingReference;
                case WorldMemoryWriteStatus.ReferenceMismatch:
                    return WorldMemoryAtomicMutationStatus.ReferenceMismatch;
                default:
                    return WorldMemoryAtomicMutationStatus
                        .DomainValidationFailure;
            }
        }

        private static bool IsStorageFailure(
            WorldMemoryAtomicMutationStatus status)
        {
            return status ==
                    WorldMemoryAtomicMutationStatus.PersistenceIOFailure ||
                status ==
                    WorldMemoryAtomicMutationStatus.TransactionBeginFailure ||
                status == WorldMemoryAtomicMutationStatus.CommitFailure ||
                status ==
                    WorldMemoryAtomicMutationStatus.TimeoutOrContention ||
                status ==
                    WorldMemoryAtomicMutationStatus.ProviderUnavailable;
        }
    }

    public interface IWorldMemoryAtomicMutationStore : IWorldMemoryStore
    {
        WorldMemoryAtomicMutationResult ApplyAtomicMutation(
            WorldMemoryAtomicMutation mutation);
    }
}
