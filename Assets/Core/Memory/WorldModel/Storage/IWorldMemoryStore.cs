// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections.Generic;

using SalieriAI.Core.Memory.WorldModel.Contracts;

namespace SalieriAI.Core.Memory.WorldModel.Storage
{
    public enum WorldMemoryWriteStatus
    {
        Added = 0,
        InvalidRecord = 1,
        DuplicateId = 2,
        MissingReference = 3,
        ReferenceMismatch = 4
    }

    public sealed class WorldMemoryWriteResult<T> where T : class
    {
        public WorldMemoryWriteStatus Status { get; }
        public T Record { get; }
        public string Error { get; }
        public bool Succeeded => Status == WorldMemoryWriteStatus.Added;

        public WorldMemoryWriteResult(
            WorldMemoryWriteStatus status,
            T record,
            string error)
        {
            Status = status;
            Record = record;
            Error = error ?? string.Empty;
        }
    }

    /// <summary>
    /// Typed World Memory boundary. It owns no identity inference and creates
    /// no Entity implicitly. Callers must submit explicit immutable records.
    /// </summary>
    public interface IWorldMemoryStore
    {
        WorldMemoryWriteResult<WorldMemoryEntity> AddEntity(
            WorldMemoryEntity entity);
        bool TryGetEntity(string entityId, out WorldMemoryEntity entity);
        IReadOnlyList<WorldMemoryEntity> GetEntities();

        WorldMemoryWriteResult<WorldMemoryFact> AddFact(
            WorldMemoryFact fact);
        bool TryGetFact(string factId, out WorldMemoryFact fact);
        IReadOnlyList<WorldMemoryFact> GetFactsBySubject(
            string subjectEntityId);
        IReadOnlyList<WorldMemoryFact> GetFactsBySubjectAndPredicate(
            string subjectEntityId,
            string predicate);

        WorldMemoryWriteResult<WorldMemorySource> AddSource(
            WorldMemorySource source);
        bool TryGetSource(string sourceId, out WorldMemorySource source);
        IReadOnlyList<WorldMemorySource> GetSources();

        WorldMemoryWriteResult<WorldMemoryEvidence> AddEvidence(
            WorldMemoryEvidence evidence);
        bool TryGetEvidence(string evidenceId, out WorldMemoryEvidence evidence);
        IReadOnlyList<WorldMemoryEvidence> GetEvidenceByFact(string factId);

        WorldMemoryWriteResult<WorldMemoryObservation> AddObservation(
            WorldMemoryObservation observation);
        bool TryGetObservation(
            string observationId,
            out WorldMemoryObservation observation);
        IReadOnlyList<WorldMemoryObservation> GetObservations();

        WorldMemoryWriteResult<WorldMemoryRecognitionEvidence>
            AddRecognitionEvidence(WorldMemoryRecognitionEvidence evidence);
        bool TryGetRecognitionEvidence(
            string recognitionEvidenceId,
            out WorldMemoryRecognitionEvidence evidence);
        IReadOnlyList<WorldMemoryRecognitionEvidence>
            GetRecognitionEvidenceByObservation(string observationId);

        WorldMemoryWriteResult<WorldMemoryEntityResolution>
            AddEntityResolution(WorldMemoryEntityResolution resolution);
        bool TryGetEntityResolution(
            string resolutionId,
            out WorldMemoryEntityResolution resolution);
        IReadOnlyList<WorldMemoryEntityResolution>
            GetEntityResolutionsByObservation(string observationId);
        bool TryGetCurrentEntityResolution(
            string observationId,
            out WorldMemoryEntityResolution resolution);
    }
}

