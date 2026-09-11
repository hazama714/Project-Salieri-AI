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
using System.Security.Cryptography;
using System.Text;

using SQLite;

namespace SalieriAI.Core.Memory.WorldModel.Storage.Sqlite
{
    internal static class WorldMemorySqliteSchemaV1
    {
        public const int Version = 1;
        public const string MigrationName = "world_memory_schema_v1";

        private const string IdCheck =
            "length({0})=32 AND {0}=lower({0}) AND " +
            "{0} NOT GLOB '*[^0-9a-f]*'";

        private static readonly string[] TableStatements =
        {
            "CREATE TABLE world_memory_meta (" +
                "singleton_id INTEGER PRIMARY KEY CHECK(singleton_id=1)," +
                "database_id TEXT NOT NULL CHECK(" +
                    "length(database_id)=32 AND database_id=lower(database_id) AND " +
                    "database_id NOT GLOB '*[^0-9a-f]*')," +
                "created_at_utc TEXT NOT NULL," +
                "application_version TEXT NOT NULL," +
                "schema_version INTEGER NOT NULL CHECK(schema_version=1))",

            "CREATE TABLE entities (" +
                "entity_id TEXT PRIMARY KEY NOT NULL CHECK(" + Id("entity_id") + ")," +
                "schema_version INTEGER NOT NULL," +
                "insertion_order INTEGER NOT NULL UNIQUE," +
                "entity_type INTEGER NOT NULL," +
                "canonical_name TEXT NOT NULL," +
                "status INTEGER NOT NULL," +
                "created_at_utc TEXT NOT NULL," +
                "updated_at_utc TEXT NOT NULL," +
                "merged_into_entity_id TEXT NULL CHECK(merged_into_entity_id IS NULL OR (" + Id("merged_into_entity_id") + "))," +
                "FOREIGN KEY(merged_into_entity_id) REFERENCES entities(entity_id) ON UPDATE RESTRICT ON DELETE RESTRICT)",

            "CREATE TABLE sources (" +
                "source_id TEXT PRIMARY KEY NOT NULL CHECK(" + Id("source_id") + ")," +
                "schema_version INTEGER NOT NULL," +
                "insertion_order INTEGER NOT NULL UNIQUE," +
                "source_type INTEGER NOT NULL," +
                "display_name TEXT NOT NULL," +
                "uri TEXT NOT NULL," +
                "publisher TEXT NOT NULL," +
                "external_reference TEXT NOT NULL," +
                "component_id TEXT NOT NULL," +
                "created_at_utc TEXT NOT NULL," +
                "status INTEGER NOT NULL)",

            "CREATE TABLE observations (" +
                "observation_id TEXT PRIMARY KEY NOT NULL CHECK(" + Id("observation_id") + ")," +
                "schema_version INTEGER NOT NULL," +
                "insertion_order INTEGER NOT NULL UNIQUE," +
                "modality INTEGER NOT NULL," +
                "observed_at_utc TEXT NOT NULL," +
                "session_id TEXT NOT NULL," +
                "source_frame_id INTEGER NULL," +
                "target_key TEXT NOT NULL," +
                "track_id INTEGER NULL," +
                "source_id TEXT NULL CHECK(source_id IS NULL OR (" + Id("source_id") + "))," +
                "created_at_utc TEXT NOT NULL," +
                "FOREIGN KEY(source_id) REFERENCES sources(source_id) ON UPDATE RESTRICT ON DELETE RESTRICT)",

            "CREATE TABLE recognition_evidence (" +
                "recognition_evidence_id TEXT PRIMARY KEY NOT NULL CHECK(" + Id("recognition_evidence_id") + ")," +
                "schema_version INTEGER NOT NULL," +
                "insertion_order INTEGER NOT NULL UNIQUE," +
                "observation_id TEXT NOT NULL CHECK(" + Id("observation_id") + ")," +
                "candidate_entity_id TEXT NULL CHECK(candidate_entity_id IS NULL OR (" + Id("candidate_entity_id") + "))," +
                "recognition_type INTEGER NOT NULL," +
                "feature_reference TEXT NOT NULL," +
                "model_name TEXT NOT NULL," +
                "model_version TEXT NOT NULL," +
                "has_score INTEGER NOT NULL CHECK(has_score IN (0,1))," +
                "score REAL NOT NULL," +
                "status INTEGER NOT NULL," +
                "created_at_utc TEXT NOT NULL," +
                "observed_at_utc TEXT NULL," +
                "UNIQUE(recognition_evidence_id, observation_id)," +
                "FOREIGN KEY(observation_id) REFERENCES observations(observation_id) ON UPDATE RESTRICT ON DELETE RESTRICT," +
                "FOREIGN KEY(candidate_entity_id) REFERENCES entities(entity_id) ON UPDATE RESTRICT ON DELETE RESTRICT)",

            "CREATE TABLE facts (" +
                "fact_id TEXT PRIMARY KEY NOT NULL CHECK(" + Id("fact_id") + ")," +
                "schema_version INTEGER NOT NULL," +
                "insertion_order INTEGER NOT NULL UNIQUE," +
                "subject_entity_id TEXT NOT NULL CHECK(" + Id("subject_entity_id") + ")," +
                "predicate TEXT NOT NULL," +
                "value_kind INTEGER NOT NULL," +
                "object_entity_id TEXT NULL CHECK(object_entity_id IS NULL OR (" + Id("object_entity_id") + "))," +
                "literal_value TEXT NOT NULL," +
                "value_type INTEGER NOT NULL," +
                "status INTEGER NOT NULL," +
                "created_at_utc TEXT NOT NULL," +
                "updated_at_utc TEXT NOT NULL," +
                "valid_from_utc TEXT NULL," +
                "valid_until_utc TEXT NULL," +
                "last_verified_at_utc TEXT NULL," +
                "freshness_policy_id TEXT NOT NULL," +
                "superseded_by_fact_id TEXT NULL CHECK(superseded_by_fact_id IS NULL OR (" + Id("superseded_by_fact_id") + "))," +
                "CHECK((value_kind=1 AND object_entity_id IS NOT NULL AND literal_value='') OR " +
                    "(value_kind=2 AND object_entity_id IS NULL))," +
                "FOREIGN KEY(subject_entity_id) REFERENCES entities(entity_id) ON UPDATE RESTRICT ON DELETE RESTRICT," +
                "FOREIGN KEY(object_entity_id) REFERENCES entities(entity_id) ON UPDATE RESTRICT ON DELETE RESTRICT," +
                "FOREIGN KEY(superseded_by_fact_id) REFERENCES facts(fact_id) ON UPDATE RESTRICT ON DELETE RESTRICT)",

            "CREATE TABLE evidence (" +
                "evidence_id TEXT PRIMARY KEY NOT NULL CHECK(" + Id("evidence_id") + ")," +
                "schema_version INTEGER NOT NULL," +
                "insertion_order INTEGER NOT NULL UNIQUE," +
                "fact_id TEXT NOT NULL CHECK(" + Id("fact_id") + ")," +
                "evidence_type INTEGER NOT NULL," +
                "stance INTEGER NOT NULL," +
                "source_id TEXT NULL CHECK(source_id IS NULL OR (" + Id("source_id") + "))," +
                "attributed_source_id TEXT NULL CHECK(attributed_source_id IS NULL OR (" + Id("attributed_source_id") + "))," +
                "has_confidence INTEGER NOT NULL CHECK(has_confidence IN (0,1))," +
                "confidence REAL NOT NULL," +
                "created_at_utc TEXT NOT NULL," +
                "observed_at_utc TEXT NULL," +
                "asserted_at_utc TEXT NULL," +
                "retrieved_at_utc TEXT NULL," +
                "published_at_utc TEXT NULL," +
                "valid_from_utc TEXT NULL," +
                "valid_until_utc TEXT NULL," +
                "last_verified_at_utc TEXT NULL," +
                "observation_id TEXT NULL CHECK(observation_id IS NULL OR (" + Id("observation_id") + "))," +
                "recognition_evidence_id TEXT NULL CHECK(recognition_evidence_id IS NULL OR (" + Id("recognition_evidence_id") + "))," +
                "experience_record_id TEXT NOT NULL," +
                "inference_process_id TEXT NOT NULL," +
                "status INTEGER NOT NULL," +
                "diagnostic_note TEXT NOT NULL," +
                "FOREIGN KEY(fact_id) REFERENCES facts(fact_id) ON UPDATE RESTRICT ON DELETE RESTRICT," +
                "FOREIGN KEY(source_id) REFERENCES sources(source_id) ON UPDATE RESTRICT ON DELETE RESTRICT," +
                "FOREIGN KEY(attributed_source_id) REFERENCES sources(source_id) ON UPDATE RESTRICT ON DELETE RESTRICT," +
                "FOREIGN KEY(observation_id) REFERENCES observations(observation_id) ON UPDATE RESTRICT ON DELETE RESTRICT," +
                "FOREIGN KEY(recognition_evidence_id) REFERENCES recognition_evidence(recognition_evidence_id) ON UPDATE RESTRICT ON DELETE RESTRICT)",

            "CREATE TABLE entity_resolutions (" +
                "resolution_id TEXT PRIMARY KEY NOT NULL CHECK(" + Id("resolution_id") + ")," +
                "schema_version INTEGER NOT NULL," +
                "insertion_order INTEGER NOT NULL UNIQUE," +
                "observation_id TEXT NOT NULL CHECK(" + Id("observation_id") + ")," +
                "entity_id TEXT NULL CHECK(entity_id IS NULL OR (" + Id("entity_id") + "))," +
                "resolution_status INTEGER NOT NULL," +
                "resolution_method INTEGER NOT NULL," +
                "created_at_utc TEXT NOT NULL," +
                "has_confidence INTEGER NOT NULL CHECK(has_confidence IN (0,1))," +
                "confidence REAL NOT NULL," +
                "UNIQUE(resolution_id, observation_id)," +
                "FOREIGN KEY(observation_id) REFERENCES observations(observation_id) ON UPDATE RESTRICT ON DELETE RESTRICT," +
                "FOREIGN KEY(entity_id) REFERENCES entities(entity_id) ON UPDATE RESTRICT ON DELETE RESTRICT)",

            "CREATE TABLE entity_resolution_recognition_evidence (" +
                "resolution_id TEXT NOT NULL CHECK(" + Id("resolution_id") + ")," +
                "recognition_evidence_id TEXT NOT NULL CHECK(" + Id("recognition_evidence_id") + ")," +
                "observation_id TEXT NOT NULL CHECK(" + Id("observation_id") + ")," +
                "support_order INTEGER NOT NULL CHECK(support_order>=0)," +
                "PRIMARY KEY(resolution_id, recognition_evidence_id)," +
                "UNIQUE(resolution_id, support_order)," +
                "FOREIGN KEY(resolution_id, observation_id) REFERENCES entity_resolutions(resolution_id, observation_id) ON UPDATE RESTRICT ON DELETE RESTRICT," +
                "FOREIGN KEY(recognition_evidence_id, observation_id) REFERENCES recognition_evidence(recognition_evidence_id, observation_id) ON UPDATE RESTRICT ON DELETE RESTRICT)",

            "CREATE TABLE migration_history (" +
                "schema_version INTEGER PRIMARY KEY," +
                "migration_name TEXT NOT NULL," +
                "application_version TEXT NOT NULL," +
                "applied_at_utc TEXT NOT NULL," +
                "script_sha256 TEXT NOT NULL CHECK(length(script_sha256)=64))"
        };

        private static readonly string[] IndexStatements =
        {
            "CREATE INDEX idx_facts_subject_predicate_status ON facts(subject_entity_id,predicate,status)",
            "CREATE INDEX idx_facts_subject_status ON facts(subject_entity_id,status)",
            "CREATE INDEX idx_facts_predicate_status ON facts(predicate,status)",
            "CREATE INDEX idx_facts_valid_until_status ON facts(valid_until_utc,status)",
            "CREATE INDEX idx_facts_verified_status ON facts(last_verified_at_utc,status)",
            "CREATE INDEX idx_evidence_fact_status ON evidence(fact_id,status)",
            "CREATE INDEX idx_evidence_source ON evidence(source_id)",
            "CREATE INDEX idx_evidence_attributed_source ON evidence(attributed_source_id)",
            "CREATE INDEX idx_evidence_experience ON evidence(experience_record_id) WHERE experience_record_id<>''",
            "CREATE INDEX idx_evidence_observation ON evidence(observation_id)",
            "CREATE INDEX idx_observations_observed_at ON observations(observed_at_utc)",
            "CREATE INDEX idx_recognition_observation_status ON recognition_evidence(observation_id,status)",
            "CREATE INDEX idx_recognition_candidate_status ON recognition_evidence(candidate_entity_id,status)",
            "CREATE INDEX idx_resolutions_observation_order ON entity_resolutions(observation_id,insertion_order)",
            "CREATE INDEX idx_resolutions_entity_created ON entity_resolutions(entity_id,created_at_utc)",
            "CREATE INDEX idx_resolution_support_reverse ON entity_resolution_recognition_evidence(recognition_evidence_id,resolution_id)"
        };

        private static readonly string[] ExpectedTables =
        {
            "entities", "entity_resolution_recognition_evidence",
            "entity_resolutions", "evidence", "facts", "migration_history",
            "observations", "recognition_evidence", "sources",
            "world_memory_meta"
        };

        public static string ScriptSha256 => ComputeScriptSha256();

        public static void Create(
            SQLiteConnection connection,
            string applicationVersion,
            DateTime createdAtUtc)
        {
            connection.BeginTransaction();
            try
            {
                for (int i = 0; i < TableStatements.Length; i++)
                    connection.Execute(TableStatements[i]);
                for (int i = 0; i < IndexStatements.Length; i++)
                    connection.Execute(IndexStatements[i]);

                string utc = WorldMemorySqliteRecordMapper.Utc(createdAtUtc);
                string app = applicationVersion ?? string.Empty;
                connection.Execute(
                    "INSERT INTO world_memory_meta(" +
                    "singleton_id,database_id,created_at_utc," +
                    "application_version,schema_version) VALUES (1,?,?,?,?)",
                    Guid.NewGuid().ToString("N"), utc, app, Version);
                connection.Execute(
                    "INSERT INTO migration_history(" +
                    "schema_version,migration_name,application_version," +
                    "applied_at_utc,script_sha256) VALUES (?,?,?,?,?)",
                    Version, MigrationName, app, utc, ScriptSha256);
                connection.Execute("PRAGMA user_version=" + Version);
                connection.Commit();
            }
            catch
            {
                connection.Rollback();
                throw;
            }
        }

        public static void Verify(SQLiteConnection connection)
        {
            int version = connection.ExecuteScalar<int>("PRAGMA user_version");
            if (version != Version)
            {
                throw new WorldMemorySqliteStoreException(
                    WorldMemorySqliteFailureKind.UnsupportedSchema,
                    "Unsupported World Memory schema version: " + version);
            }

            List<TableNameRow> rows = connection.Query<TableNameRow>(
                "SELECT name AS Name FROM sqlite_master " +
                "WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
            if (rows.Count != ExpectedTables.Length)
                throw Incompatible("Unexpected World Memory table count.");
            for (int i = 0; i < ExpectedTables.Length; i++)
            {
                if (!string.Equals(rows[i].Name, ExpectedTables[i],
                    StringComparison.Ordinal))
                {
                    throw Incompatible("World Memory schema table mismatch.");
                }
            }

            int meta = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM world_memory_meta " +
                "WHERE singleton_id=1 AND schema_version=?", Version);
            int history = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM migration_history " +
                "WHERE schema_version=?", Version);
            if (meta != 1 || history != 1)
                throw Incompatible("World Memory schema metadata is invalid.");

            string quick = connection.ExecuteScalar<string>(
                "PRAGMA quick_check");
            if (!string.Equals(quick, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new WorldMemorySqliteStoreException(
                    WorldMemorySqliteFailureKind.Corrupt,
                    "World Memory database quick_check failed.");
            }
        }

        private static WorldMemorySqliteStoreException Incompatible(
            string message)
        {
            return new WorldMemorySqliteStoreException(
                WorldMemorySqliteFailureKind.UnsupportedSchema, message);
        }

        private static string Id(string column)
        {
            return string.Format(CultureInfo.InvariantCulture, IdCheck,
                column);
        }

        private static string ComputeScriptSha256()
        {
            string script = string.Join("\n", TableStatements) + "\n" +
                string.Join("\n", IndexStatements);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(script));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("X2",
                        CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private sealed class TableNameRow
        {
            public string Name { get; set; }
        }
    }
}
