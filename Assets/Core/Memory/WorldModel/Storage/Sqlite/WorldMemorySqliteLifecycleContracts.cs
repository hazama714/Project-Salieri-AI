// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Memory.WorldModel.Storage.Sqlite
{
    public enum WorldMemorySqliteLifecycleStatus
    {
        None = 0,
        FreshDatabaseCreated = 1,
        OpenedExisting = 2,
        UnsupportedSchema = 3,
        NativeBindingUnavailable = 4,
        OpenFailed = 5,
        Corrupt = 6,
        BusyTimedOut = 7,
        DiskOrIoFailure = 8,
        Shutdown = 9,
        Released = 10
    }

    public enum WorldMemorySqliteOwnerState
    {
        Created = 0,
        Opening = 1,
        Ready = 2,
        ShuttingDown = 3,
        Released = 4,
        Failed = 5
    }

    public enum WorldMemorySqliteFailureKind
    {
        None = 0,
        ProviderUnavailable = 1,
        BusyTimedOut = 2,
        DiskOrIoFailure = 3,
        ConstraintViolation = 4,
        Corrupt = 5,
        UnsupportedSchema = 6,
        OpenFailed = 7,
        TransactionBeginFailure = 8,
        CommitFailure = 9,
        RollbackFailure = 10
    }

    public sealed class WorldMemorySqliteLifecycleResult
    {
        public WorldMemorySqliteLifecycleStatus Status { get; }
        public string DatabasePath { get; }
        public string SqliteVersion { get; }
        public int SchemaVersion { get; }
        public string JournalMode { get; }
        public int SynchronousMode { get; }
        public bool ForeignKeysEnabled { get; }
        public int BusyTimeoutMilliseconds { get; }
        public int WorkerThreadId { get; }
        public string Error { get; }
        public bool Succeeded =>
            Status == WorldMemorySqliteLifecycleStatus.FreshDatabaseCreated ||
            Status == WorldMemorySqliteLifecycleStatus.OpenedExisting;

        public WorldMemorySqliteLifecycleResult(
            WorldMemorySqliteLifecycleStatus status,
            string databasePath,
            string sqliteVersion,
            int schemaVersion,
            string journalMode,
            int synchronousMode,
            bool foreignKeysEnabled,
            int busyTimeoutMilliseconds,
            int workerThreadId,
            string error)
        {
            Status = status;
            DatabasePath = databasePath ?? string.Empty;
            SqliteVersion = sqliteVersion ?? string.Empty;
            SchemaVersion = schemaVersion;
            JournalMode = journalMode ?? string.Empty;
            SynchronousMode = synchronousMode;
            ForeignKeysEnabled = foreignKeysEnabled;
            BusyTimeoutMilliseconds = busyTimeoutMilliseconds;
            WorkerThreadId = workerThreadId;
            Error = error ?? string.Empty;
        }

        internal static WorldMemorySqliteLifecycleResult Failure(
            WorldMemorySqliteLifecycleStatus status,
            string path,
            int workerThreadId,
            string error)
        {
            return new WorldMemorySqliteLifecycleResult(status, path,
                string.Empty, 0, string.Empty, 0, false, 0,
                workerThreadId, error);
        }
    }

    public sealed class WorldMemorySqliteStoreException : Exception
    {
        public WorldMemorySqliteFailureKind FailureKind { get; }

        public WorldMemorySqliteStoreException(
            WorldMemorySqliteFailureKind failureKind,
            string message,
            Exception innerException = null)
            : base(message ?? string.Empty, innerException)
        {
            FailureKind = failureKind;
        }
    }

    internal sealed class WorldMemorySqliteFaultInjection
    {
        public bool FailTransactionBegin;
        public bool FailBeforeFirstWrite;
        public bool FailCommit;
        public bool FailRollback;
    }
}

