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

using SQLite;

using SalieriAI.Core.Memory.WorldModel.Contracts;

namespace SalieriAI.Core.Memory.WorldModel.Storage.Sqlite
{
    public sealed class SqliteWorldMemoryStore :
        IWorldMemoryAtomicMutationStore, IDisposable
    {
        private readonly WorldMemorySqliteConnectionOwner owner;
        private readonly WorldMemorySqliteFaultInjection faults;
        private bool disposed;

        private SqliteWorldMemoryStore(
            WorldMemorySqliteConnectionOwner owner,
            WorldMemorySqliteFaultInjection faults)
        {
            this.owner = owner;
            this.faults = faults ?? new WorldMemorySqliteFaultInjection();
        }

        public WorldMemorySqliteLifecycleResult Lifecycle =>
            owner.LifecycleResult;
        public int WorkerThreadId => owner.WorkerThreadId;

        public static WorldMemorySqliteLifecycleResult TryOpen(
            string databasePath,
            string applicationVersion,
            DateTime createdAtUtc,
            out SqliteWorldMemoryStore store)
        {
            return TryOpenInternal(databasePath, applicationVersion,
                createdAtUtc, null, out store);
        }

        internal static WorldMemorySqliteLifecycleResult TryOpenInternal(
            string databasePath,
            string applicationVersion,
            DateTime createdAtUtc,
            WorldMemorySqliteFaultInjection faults,
            out SqliteWorldMemoryStore store)
        {
            WorldMemorySqliteConnectionOwner connectionOwner;
            WorldMemorySqliteLifecycleResult result =
                WorldMemorySqliteConnectionOwner.TryOpen(databasePath,
                    applicationVersion, createdAtUtc, out connectionOwner);
            store = result.Succeeded
                ? new SqliteWorldMemoryStore(connectionOwner, faults)
                : null;
            return result;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            owner.Dispose();
        }

        public WorldMemoryWriteResult<WorldMemoryEntity> AddEntity(
            WorldMemoryEntity entity)
        {
            return ExecuteSingle(
                snapshot => snapshot.AddEntity(entity),
                connection => InsertEntity(connection, entity));
        }

        public bool TryGetEntity(string entityId, out WorldMemoryEntity entity)
        {
            WorldMemoryEntity found = owner.Execute(connection =>
                FirstOrNull(ReadEntities(connection,
                    " WHERE entity_id=?", Id(entityId))));
            entity = found;
            return found != null;
        }

        public IReadOnlyList<WorldMemoryEntity> GetEntities()
        {
            return owner.Execute(connection => ReadEntities(connection));
        }

        public WorldMemoryWriteResult<WorldMemoryFact> AddFact(
            WorldMemoryFact fact)
        {
            return ExecuteSingle(
                snapshot => snapshot.AddFact(fact),
                connection => InsertFact(connection, fact));
        }

        public bool TryGetFact(string factId, out WorldMemoryFact fact)
        {
            WorldMemoryFact found = owner.Execute(connection =>
                FirstOrNull(ReadFacts(connection,
                    " WHERE fact_id=?", Id(factId))));
            fact = found;
            return found != null;
        }

        public IReadOnlyList<WorldMemoryFact> GetFactsBySubject(
            string subjectEntityId)
        {
            return owner.Execute(connection => ReadFacts(connection,
                " WHERE subject_entity_id=?", Id(subjectEntityId)));
        }

        public IReadOnlyList<WorldMemoryFact> GetFactsBySubjectAndPredicate(
            string subjectEntityId,
            string predicate)
        {
            return owner.Execute(connection => ReadFacts(connection,
                " WHERE subject_entity_id=? AND predicate=?",
                Id(subjectEntityId), Text(predicate)));
        }

        public WorldMemoryWriteResult<WorldMemorySource> AddSource(
            WorldMemorySource source)
        {
            return ExecuteSingle(
                snapshot => snapshot.AddSource(source),
                connection => InsertSource(connection, source));
        }

        public bool TryGetSource(string sourceId, out WorldMemorySource source)
        {
            WorldMemorySource found = owner.Execute(connection =>
                FirstOrNull(ReadSources(connection,
                    " WHERE source_id=?", Id(sourceId))));
            source = found;
            return found != null;
        }

        public IReadOnlyList<WorldMemorySource> GetSources()
        {
            return owner.Execute(connection => ReadSources(connection));
        }

        public WorldMemoryWriteResult<WorldMemoryEvidence> AddEvidence(
            WorldMemoryEvidence evidence)
        {
            return ExecuteSingle(
                snapshot => snapshot.AddEvidence(evidence),
                connection => InsertEvidence(connection, evidence));
        }

        public bool TryGetEvidence(
            string evidenceId,
            out WorldMemoryEvidence evidence)
        {
            WorldMemoryEvidence found = owner.Execute(connection =>
                FirstOrNull(ReadEvidence(connection,
                    " WHERE evidence_id=?", Id(evidenceId))));
            evidence = found;
            return found != null;
        }

        public IReadOnlyList<WorldMemoryEvidence> GetEvidenceByFact(
            string factId)
        {
            return owner.Execute(connection => ReadEvidence(connection,
                " WHERE fact_id=?", Id(factId)));
        }

        public WorldMemoryWriteResult<WorldMemoryObservation> AddObservation(
            WorldMemoryObservation observation)
        {
            return ExecuteSingle(
                snapshot => snapshot.AddObservation(observation),
                connection => InsertObservation(connection, observation));
        }

        public bool TryGetObservation(
            string observationId,
            out WorldMemoryObservation observation)
        {
            WorldMemoryObservation found = owner.Execute(connection =>
                FirstOrNull(ReadObservations(connection,
                    " WHERE observation_id=?", Id(observationId))));
            observation = found;
            return found != null;
        }

        public IReadOnlyList<WorldMemoryObservation> GetObservations()
        {
            return owner.Execute(connection => ReadObservations(connection));
        }

        public WorldMemoryWriteResult<WorldMemoryRecognitionEvidence>
            AddRecognitionEvidence(WorldMemoryRecognitionEvidence evidence)
        {
            return ExecuteSingle(
                snapshot => snapshot.AddRecognitionEvidence(evidence),
                connection => InsertRecognition(connection, evidence));
        }

        public bool TryGetRecognitionEvidence(
            string recognitionEvidenceId,
            out WorldMemoryRecognitionEvidence evidence)
        {
            WorldMemoryRecognitionEvidence found = owner.Execute(connection =>
                FirstOrNull(ReadRecognition(connection,
                    " WHERE recognition_evidence_id=?",
                    Id(recognitionEvidenceId))));
            evidence = found;
            return found != null;
        }

        public IReadOnlyList<WorldMemoryRecognitionEvidence>
            GetRecognitionEvidenceByObservation(string observationId)
        {
            return owner.Execute(connection => ReadRecognition(connection,
                " WHERE observation_id=?", Id(observationId)));
        }

        public WorldMemoryWriteResult<WorldMemoryEntityResolution>
            AddEntityResolution(WorldMemoryEntityResolution resolution)
        {
            return ExecuteSingle(
                snapshot => snapshot.AddEntityResolution(resolution),
                connection => InsertResolution(connection, resolution));
        }

        public bool TryGetEntityResolution(
            string resolutionId,
            out WorldMemoryEntityResolution resolution)
        {
            WorldMemoryEntityResolution found = owner.Execute(connection =>
                FirstOrNull(ReadResolutions(connection,
                    " WHERE resolution_id=?", Id(resolutionId))));
            resolution = found;
            return found != null;
        }

        public IReadOnlyList<WorldMemoryEntityResolution>
            GetEntityResolutionsByObservation(string observationId)
        {
            return owner.Execute(connection => ReadResolutions(connection,
                " WHERE observation_id=?", Id(observationId)));
        }

        public bool TryGetCurrentEntityResolution(
            string observationId,
            out WorldMemoryEntityResolution resolution)
        {
            WorldMemoryEntityResolution found = owner.Execute(connection =>
                FirstOrNull(ReadResolutions(connection,
                    " WHERE observation_id=? ORDER BY insertion_order DESC LIMIT 1",
                    Id(observationId))));
            resolution = found;
            return found != null;
        }

        public WorldMemoryAtomicMutationResult ApplyAtomicMutation(
            WorldMemoryAtomicMutation mutation)
        {
            try
            {
                return owner.Execute(connection =>
                    ApplyAtomicOnWorker(connection, mutation));
            }
            catch (Exception exception)
            {
                return WorldMemoryAtomicMutationResult.StorageFailure(
                    ToAtomicStatus(exception),
                    mutation == null
                        ? WorldMemoryAtomicMutationKind.Unknown
                        : mutation.Kind,
                    WorldMemorySqliteFailureMapper.BoundedDiagnostic(
                        exception));
            }
        }

        internal int ExecuteScalarIntForTest(string sql, params object[] args)
        {
            return owner.Execute(connection =>
                connection.ExecuteScalar<int>(sql, args));
        }

        internal string ExecuteScalarStringForTest(
            string sql,
            params object[] args)
        {
            return owner.Execute(connection =>
                connection.ExecuteScalar<string>(sql, args));
        }

        internal string QueryPlanDetailForTest(
            string query,
            params object[] args)
        {
            return owner.Execute(connection =>
            {
                List<QueryPlanRow> rows = connection.Query<QueryPlanRow>(
                    "EXPLAIN QUERY PLAN " + query, args);
                return rows.Count == 0 ? string.Empty : rows[0].Detail;
            });
        }

        private WorldMemoryAtomicMutationResult ApplyAtomicOnWorker(
            SQLiteConnection connection,
            WorldMemoryAtomicMutation mutation)
        {
            InMemoryWorldMemoryStore snapshot = LoadSnapshot(connection);
            WorldMemoryAtomicMutationResult validation =
                snapshot.ApplyAtomicMutation(mutation);
            if (!validation.Succeeded)
                return validation;

            bool began = false;
            AtomicStage stage = AtomicStage.Begin;
            try
            {
                if (faults.FailTransactionBegin)
                    throw Injected("transaction begin");
                connection.BeginTransaction();
                began = true;
                stage = AtomicStage.Write;
                if (faults.FailBeforeFirstWrite)
                    throw Injected("write");

                InsertMutationRows(connection, mutation);
                stage = AtomicStage.Commit;
                if (faults.FailCommit)
                    throw Injected("commit");
                connection.Commit();
                return WorldMemoryAtomicMutationResult.Success(mutation.Kind);
            }
            catch (Exception primary)
            {
                WorldMemoryAtomicMutationStatus primaryStatus =
                    ToAtomicStatus(primary, stage);
                if (!began)
                {
                    return WorldMemoryAtomicMutationResult.StorageFailure(
                        primaryStatus, mutation.Kind,
                        WorldMemorySqliteFailureMapper.BoundedDiagnostic(
                            primary));
                }

                try
                {
                    if (faults.FailRollback)
                        throw Injected("rollback");
                    connection.Rollback();
                    return WorldMemoryAtomicMutationResult.StorageFailure(
                        primaryStatus, mutation.Kind,
                        WorldMemorySqliteFailureMapper.BoundedDiagnostic(
                            primary),
                        WorldMemoryAtomicRollbackStatus.Succeeded);
                }
                catch (Exception rollback)
                {
                    return WorldMemoryAtomicMutationResult.StorageFailure(
                        primaryStatus, mutation.Kind,
                        WorldMemorySqliteFailureMapper.BoundedDiagnostic(
                            primary),
                        WorldMemoryAtomicRollbackStatus.Failed,
                        WorldMemorySqliteFailureMapper.BoundedDiagnostic(
                            rollback));
                }
            }
        }

        private WorldMemoryWriteResult<T> ExecuteSingle<T>(
            Func<InMemoryWorldMemoryStore, WorldMemoryWriteResult<T>> validate,
            Action<SQLiteConnection> insert) where T : class
        {
            try
            {
                return owner.Execute(connection =>
                {
                    InMemoryWorldMemoryStore snapshot = LoadSnapshot(connection);
                    WorldMemoryWriteResult<T> result = validate(snapshot);
                    if (!result.Succeeded)
                        return result;
                    ExecuteSingleTransaction(connection, insert);
                    return result;
                });
            }
            catch (WorldMemorySqliteStoreException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new WorldMemorySqliteStoreException(
                    WorldMemorySqliteFailureMapper.ToFailureKind(exception),
                    WorldMemorySqliteFailureMapper.BoundedDiagnostic(
                        exception), exception);
            }
        }

        private static void ExecuteSingleTransaction(
            SQLiteConnection connection,
            Action<SQLiteConnection> insert)
        {
            bool began = false;
            try
            {
                connection.BeginTransaction();
                began = true;
                insert(connection);
                connection.Commit();
            }
            catch (Exception exception)
            {
                if (began)
                {
                    try { connection.Rollback(); }
                    catch (Exception rollback)
                    {
                        throw new WorldMemorySqliteStoreException(
                            WorldMemorySqliteFailureKind.RollbackFailure,
                            "Single-record rollback failed after: " +
                            WorldMemorySqliteFailureMapper.BoundedDiagnostic(
                                exception), rollback);
                    }
                }
                throw;
            }
        }

        private static void InsertMutationRows(
            SQLiteConnection connection,
            WorldMemoryAtomicMutation mutation)
        {
            for (int i = 0; i < mutation.Entities.Count; i++)
                InsertEntity(connection, mutation.Entities[i]);
            for (int i = 0; i < mutation.Sources.Count; i++)
                InsertSource(connection, mutation.Sources[i]);
            for (int i = 0; i < mutation.Observations.Count; i++)
                InsertObservation(connection, mutation.Observations[i]);
            for (int i = 0; i < mutation.RecognitionEvidence.Count; i++)
                InsertRecognition(connection,
                    mutation.RecognitionEvidence[i]);
            for (int i = 0; i < mutation.Facts.Count; i++)
                InsertFact(connection, mutation.Facts[i]);
            for (int i = 0; i < mutation.Evidence.Count; i++)
                InsertEvidence(connection, mutation.Evidence[i]);
            for (int i = 0; i < mutation.EntityResolutions.Count; i++)
                InsertResolution(connection,
                    mutation.EntityResolutions[i]);
        }

        private static void InsertEntity(
            SQLiteConnection connection,
            WorldMemoryEntity value)
        {
            connection.Execute(
                "INSERT INTO entities(entity_id,schema_version," +
                "insertion_order,entity_type,canonical_name,status," +
                "created_at_utc,updated_at_utc,merged_into_entity_id) " +
                "VALUES (?,?,(SELECT COALESCE(MAX(insertion_order),0)+1 " +
                "FROM entities),?,?,?,?,?,?)",
                value.EntityId, WorldMemoryEntity.SchemaVersion,
                (int)value.EntityType, value.CanonicalName,
                (int)value.Status,
                WorldMemorySqliteRecordMapper.Utc(value.CreatedAtUtc),
                WorldMemorySqliteRecordMapper.Utc(value.UpdatedAtUtc),
                WorldMemorySqliteRecordMapper.NullId(
                    value.MergedIntoEntityId));
        }

        private static void InsertSource(
            SQLiteConnection connection,
            WorldMemorySource value)
        {
            connection.Execute(
                "INSERT INTO sources(source_id,schema_version," +
                "insertion_order,source_type,display_name,uri,publisher," +
                "external_reference,component_id,created_at_utc,status) " +
                "VALUES (?,?,(SELECT COALESCE(MAX(insertion_order),0)+1 " +
                "FROM sources),?,?,?,?,?,?,?,?)",
                value.SourceId, WorldMemorySource.SchemaVersion,
                (int)value.SourceType, value.DisplayName, value.Uri,
                value.Publisher, value.ExternalReference, value.ComponentId,
                WorldMemorySqliteRecordMapper.Utc(value.CreatedAtUtc),
                (int)value.Status);
        }

        private static void InsertObservation(
            SQLiteConnection connection,
            WorldMemoryObservation value)
        {
            connection.Execute(
                "INSERT INTO observations(observation_id,schema_version," +
                "insertion_order,modality,observed_at_utc,session_id," +
                "source_frame_id,target_key,track_id,source_id," +
                "created_at_utc) VALUES (?,?,(SELECT COALESCE(" +
                "MAX(insertion_order),0)+1 FROM observations),?,?,?,?,?,?,?,?)",
                value.ObservationId, WorldMemoryObservation.SchemaVersion,
                (int)value.Modality,
                WorldMemorySqliteRecordMapper.Utc(value.ObservedAtUtc),
                value.SessionId, value.SourceFrameId, value.TargetKey,
                value.TrackId,
                WorldMemorySqliteRecordMapper.NullId(value.SourceId),
                WorldMemorySqliteRecordMapper.Utc(value.CreatedAtUtc));
        }

        private static void InsertRecognition(
            SQLiteConnection connection,
            WorldMemoryRecognitionEvidence value)
        {
            connection.Execute(
                "INSERT INTO recognition_evidence(" +
                "recognition_evidence_id,schema_version,insertion_order," +
                "observation_id,candidate_entity_id,recognition_type," +
                "feature_reference,model_name,model_version,has_score," +
                "score,status,created_at_utc,observed_at_utc) VALUES " +
                "(?,?,(SELECT COALESCE(MAX(insertion_order),0)+1 FROM " +
                "recognition_evidence),?,?,?,?,?,?,?,?,?,?,?)",
                value.RecognitionEvidenceId,
                WorldMemoryRecognitionEvidence.SchemaVersion,
                value.ObservationId,
                WorldMemorySqliteRecordMapper.NullId(
                    value.CandidateEntityId),
                (int)value.RecognitionType, value.FeatureReference,
                value.ModelName, value.ModelVersion,
                value.HasScore ? 1 : 0, value.Score, (int)value.Status,
                WorldMemorySqliteRecordMapper.Utc(value.CreatedAtUtc),
                WorldMemorySqliteRecordMapper.Utc(value.ObservedAtUtc));
        }

        private static void InsertFact(
            SQLiteConnection connection,
            WorldMemoryFact value)
        {
            connection.Execute(
                "INSERT INTO facts(fact_id,schema_version,insertion_order," +
                "subject_entity_id,predicate,value_kind,object_entity_id," +
                "literal_value,value_type,status,created_at_utc," +
                "updated_at_utc,valid_from_utc,valid_until_utc," +
                "last_verified_at_utc,freshness_policy_id," +
                "superseded_by_fact_id) VALUES (?,?,(SELECT COALESCE(" +
                "MAX(insertion_order),0)+1 FROM facts),?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                value.FactId, WorldMemoryFact.SchemaVersion,
                value.SubjectEntityId, value.Predicate, (int)value.ValueKind,
                WorldMemorySqliteRecordMapper.NullId(value.ObjectEntityId),
                value.LiteralValue, (int)value.ValueType, (int)value.Status,
                WorldMemorySqliteRecordMapper.Utc(value.CreatedAtUtc),
                WorldMemorySqliteRecordMapper.Utc(value.UpdatedAtUtc),
                WorldMemorySqliteRecordMapper.Utc(value.ValidFromUtc),
                WorldMemorySqliteRecordMapper.Utc(value.ValidUntilUtc),
                WorldMemorySqliteRecordMapper.Utc(value.LastVerifiedAtUtc),
                value.FreshnessPolicyId,
                WorldMemorySqliteRecordMapper.NullId(
                    value.SupersededByFactId));
        }

        private static void InsertEvidence(
            SQLiteConnection connection,
            WorldMemoryEvidence value)
        {
            connection.Execute(
                "INSERT INTO evidence(evidence_id,schema_version," +
                "insertion_order,fact_id,evidence_type,stance,source_id," +
                "attributed_source_id,has_confidence,confidence," +
                "created_at_utc,observed_at_utc,asserted_at_utc," +
                "retrieved_at_utc,published_at_utc,valid_from_utc," +
                "valid_until_utc,last_verified_at_utc,observation_id," +
                "recognition_evidence_id,experience_record_id," +
                "inference_process_id,status,diagnostic_note) VALUES " +
                "(?,?,(SELECT COALESCE(MAX(insertion_order),0)+1 FROM " +
                "evidence),?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                value.EvidenceId, WorldMemoryEvidence.SchemaVersion,
                value.FactId, (int)value.EvidenceType, (int)value.Stance,
                WorldMemorySqliteRecordMapper.NullId(value.SourceId),
                WorldMemorySqliteRecordMapper.NullId(
                    value.AttributedSourceId),
                value.HasConfidence ? 1 : 0, value.Confidence,
                WorldMemorySqliteRecordMapper.Utc(value.CreatedAtUtc),
                WorldMemorySqliteRecordMapper.Utc(value.ObservedAtUtc),
                WorldMemorySqliteRecordMapper.Utc(value.AssertedAtUtc),
                WorldMemorySqliteRecordMapper.Utc(value.RetrievedAtUtc),
                WorldMemorySqliteRecordMapper.Utc(value.PublishedAtUtc),
                WorldMemorySqliteRecordMapper.Utc(value.ValidFromUtc),
                WorldMemorySqliteRecordMapper.Utc(value.ValidUntilUtc),
                WorldMemorySqliteRecordMapper.Utc(value.LastVerifiedAtUtc),
                WorldMemorySqliteRecordMapper.NullId(value.ObservationId),
                WorldMemorySqliteRecordMapper.NullId(
                    value.RecognitionEvidenceId),
                value.ExperienceRecordId, value.InferenceProcessId,
                (int)value.Status, value.DiagnosticNote);
        }

        private static void InsertResolution(
            SQLiteConnection connection,
            WorldMemoryEntityResolution value)
        {
            connection.Execute(
                "INSERT INTO entity_resolutions(resolution_id," +
                "schema_version,insertion_order,observation_id,entity_id," +
                "resolution_status,resolution_method,created_at_utc," +
                "has_confidence,confidence) VALUES (?,?,(SELECT COALESCE(" +
                "MAX(insertion_order),0)+1 FROM entity_resolutions),?,?,?,?,?,?,?)",
                value.ResolutionId,
                WorldMemoryEntityResolution.SchemaVersion,
                value.ObservationId,
                WorldMemorySqliteRecordMapper.NullId(value.EntityId),
                (int)value.ResolutionStatus, (int)value.ResolutionMethod,
                WorldMemorySqliteRecordMapper.Utc(value.CreatedAtUtc),
                value.HasConfidence ? 1 : 0, value.Confidence);
            for (int i = 0;
                 i < value.SupportingRecognitionEvidenceIds.Count;
                 i++)
            {
                connection.Execute(
                    "INSERT INTO entity_resolution_recognition_evidence(" +
                    "resolution_id,recognition_evidence_id,observation_id," +
                    "support_order) VALUES (?,?,?,?)",
                    value.ResolutionId,
                    value.SupportingRecognitionEvidenceIds[i],
                    value.ObservationId, i);
            }
        }

        private static InMemoryWorldMemoryStore LoadSnapshot(
            SQLiteConnection connection)
        {
            var snapshot = new InMemoryWorldMemoryStore();
            AddAll(ReadEntities(connection), snapshot.AddEntity);
            AddAll(ReadSources(connection), snapshot.AddSource);
            AddAll(ReadObservations(connection), snapshot.AddObservation);
            AddAll(ReadRecognition(connection),
                snapshot.AddRecognitionEvidence);
            AddAll(ReadFacts(connection), snapshot.AddFact);
            AddAll(ReadEvidence(connection), snapshot.AddEvidence);
            AddAll(ReadResolutions(connection),
                snapshot.AddEntityResolution);
            return snapshot;
        }

        private static void AddAll<T>(
            IReadOnlyList<T> values,
            Func<T, WorldMemoryWriteResult<T>> add) where T : class
        {
            for (int i = 0; i < values.Count; i++)
            {
                WorldMemoryWriteResult<T> result = add(values[i]);
                if (!result.Succeeded)
                {
                    throw new WorldMemorySqliteStoreException(
                        WorldMemorySqliteFailureKind.Corrupt,
                        "Persisted World Memory state violates the frozen " +
                        "domain contract: " + result.Error);
                }
            }
        }

        private static IReadOnlyList<WorldMemoryEntity> ReadEntities(
            SQLiteConnection connection,
            string condition = "",
            params object[] args)
        {
            var result = new List<WorldMemoryEntity>();
            List<WorldMemorySqliteRecordMapper.EntityRow> rows =
                connection.Query<WorldMemorySqliteRecordMapper.EntityRow>(
                    "SELECT * FROM entities" + condition +
                    (condition.IndexOf("ORDER BY", StringComparison.Ordinal) >= 0
                        ? string.Empty : " ORDER BY insertion_order"), args);
            for (int i = 0; i < rows.Count; i++)
                result.Add(WorldMemorySqliteRecordMapper.Entity(rows[i]));
            return Copy(result);
        }

        private static IReadOnlyList<WorldMemorySource> ReadSources(
            SQLiteConnection connection,
            string condition = "",
            params object[] args)
        {
            var result = new List<WorldMemorySource>();
            List<WorldMemorySqliteRecordMapper.SourceRow> rows =
                connection.Query<WorldMemorySqliteRecordMapper.SourceRow>(
                    "SELECT * FROM sources" + condition +
                    (condition.IndexOf("ORDER BY", StringComparison.Ordinal) >= 0
                        ? string.Empty : " ORDER BY insertion_order"), args);
            for (int i = 0; i < rows.Count; i++)
                result.Add(WorldMemorySqliteRecordMapper.Source(rows[i]));
            return Copy(result);
        }

        private static IReadOnlyList<WorldMemoryObservation> ReadObservations(
            SQLiteConnection connection,
            string condition = "",
            params object[] args)
        {
            var result = new List<WorldMemoryObservation>();
            List<WorldMemorySqliteRecordMapper.ObservationRow> rows =
                connection.Query<WorldMemorySqliteRecordMapper.ObservationRow>(
                    "SELECT * FROM observations" + condition +
                    (condition.IndexOf("ORDER BY", StringComparison.Ordinal) >= 0
                        ? string.Empty : " ORDER BY insertion_order"), args);
            for (int i = 0; i < rows.Count; i++)
                result.Add(WorldMemorySqliteRecordMapper.Observation(rows[i]));
            return Copy(result);
        }

        private static IReadOnlyList<WorldMemoryRecognitionEvidence>
            ReadRecognition(
                SQLiteConnection connection,
                string condition = "",
                params object[] args)
        {
            var result = new List<WorldMemoryRecognitionEvidence>();
            List<WorldMemorySqliteRecordMapper.RecognitionRow> rows =
                connection.Query<WorldMemorySqliteRecordMapper.RecognitionRow>(
                    "SELECT * FROM recognition_evidence" + condition +
                    (condition.IndexOf("ORDER BY", StringComparison.Ordinal) >= 0
                        ? string.Empty : " ORDER BY insertion_order"), args);
            for (int i = 0; i < rows.Count; i++)
                result.Add(WorldMemorySqliteRecordMapper.Recognition(rows[i]));
            return Copy(result);
        }

        private static IReadOnlyList<WorldMemoryFact> ReadFacts(
            SQLiteConnection connection,
            string condition = "",
            params object[] args)
        {
            var result = new List<WorldMemoryFact>();
            List<WorldMemorySqliteRecordMapper.FactRow> rows =
                connection.Query<WorldMemorySqliteRecordMapper.FactRow>(
                    "SELECT * FROM facts" + condition +
                    (condition.IndexOf("ORDER BY", StringComparison.Ordinal) >= 0
                        ? string.Empty : " ORDER BY insertion_order"), args);
            for (int i = 0; i < rows.Count; i++)
                result.Add(WorldMemorySqliteRecordMapper.Fact(rows[i]));
            return Copy(result);
        }

        private static IReadOnlyList<WorldMemoryEvidence> ReadEvidence(
            SQLiteConnection connection,
            string condition = "",
            params object[] args)
        {
            var result = new List<WorldMemoryEvidence>();
            List<WorldMemorySqliteRecordMapper.EvidenceRow> rows =
                connection.Query<WorldMemorySqliteRecordMapper.EvidenceRow>(
                    "SELECT * FROM evidence" + condition +
                    (condition.IndexOf("ORDER BY", StringComparison.Ordinal) >= 0
                        ? string.Empty : " ORDER BY insertion_order"), args);
            for (int i = 0; i < rows.Count; i++)
                result.Add(WorldMemorySqliteRecordMapper.Evidence(rows[i]));
            return Copy(result);
        }

        private static IReadOnlyList<WorldMemoryEntityResolution>
            ReadResolutions(
                SQLiteConnection connection,
                string condition = "",
                params object[] args)
        {
            var result = new List<WorldMemoryEntityResolution>();
            string sql = "SELECT * FROM entity_resolutions" + condition;
            if (condition.IndexOf("ORDER BY",
                    StringComparison.Ordinal) < 0)
                sql += " ORDER BY insertion_order";
            List<WorldMemorySqliteRecordMapper.ResolutionRow> rows =
                connection.Query<WorldMemorySqliteRecordMapper.ResolutionRow>(
                    sql, args);
            for (int i = 0; i < rows.Count; i++)
            {
                List<WorldMemorySqliteRecordMapper.SupportRow> support =
                    connection.Query<WorldMemorySqliteRecordMapper.SupportRow>(
                        "SELECT recognition_evidence_id FROM " +
                        "entity_resolution_recognition_evidence " +
                        "WHERE resolution_id=? ORDER BY support_order",
                        rows[i].ResolutionId);
                var ids = new List<string>();
                for (int j = 0; j < support.Count; j++)
                    ids.Add(support[j].RecognitionEvidenceId);
                result.Add(WorldMemorySqliteRecordMapper.Resolution(
                    rows[i], ids));
            }
            return Copy(result);
        }

        private static T FirstOrNull<T>(IReadOnlyList<T> values)
            where T : class
        {
            return values.Count == 0 ? null : values[0];
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>(new List<T>(values));
        }

        private static string Id(string value)
        {
            return value == null
                ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static string Text(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static Exception Injected(string stage)
        {
            return new WorldMemorySqliteStoreException(
                WorldMemorySqliteFailureKind.DiskOrIoFailure,
                "Injected SQLite " + stage + " failure.");
        }

        private static WorldMemoryAtomicMutationStatus ToAtomicStatus(
            Exception exception,
            AtomicStage stage = AtomicStage.Write)
        {
            if (stage == AtomicStage.Begin)
                return WorldMemoryAtomicMutationStatus
                    .TransactionBeginFailure;
            if (stage == AtomicStage.Commit)
                return WorldMemoryAtomicMutationStatus.CommitFailure;
            WorldMemorySqliteFailureKind kind =
                WorldMemorySqliteFailureMapper.ToFailureKind(exception);
            switch (kind)
            {
                case WorldMemorySqliteFailureKind.ProviderUnavailable:
                    return WorldMemoryAtomicMutationStatus
                        .ProviderUnavailable;
                case WorldMemorySqliteFailureKind.BusyTimedOut:
                    return WorldMemoryAtomicMutationStatus
                        .TimeoutOrContention;
                default:
                    return WorldMemoryAtomicMutationStatus
                        .PersistenceIOFailure;
            }
        }

        private enum AtomicStage
        {
            Begin,
            Write,
            Commit
        }

        private sealed class QueryPlanRow
        {
            [Column("detail")]
            public string Detail { get; set; }
        }
    }
}
