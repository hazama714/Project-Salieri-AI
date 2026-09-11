// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

using SQLite;

namespace SalieriAI.Core.Memory.WorldModel.Storage.Sqlite
{
    /// <summary>
    /// Owns the sole SQLite connection and its serialized worker. It is not a
    /// generic scheduler and it never exposes the connection to callers.
    /// </summary>
    public sealed class WorldMemorySqliteConnectionOwner : IDisposable
    {
        private const int PendingCapacity = 1;
        private const int BusyTimeoutMilliseconds = 2000;

        private readonly object stateGate = new object();
        private readonly BlockingCollection<IWorkItem> pending =
            new BlockingCollection<IWorkItem>(PendingCapacity);
        private readonly Thread worker;
        private SQLiteConnection connection;
        private WorldMemorySqliteOwnerState state;
        private int workerThreadId;
        private WorldMemorySqliteLifecycleResult lifecycleResult;

        private WorldMemorySqliteConnectionOwner()
        {
            state = WorldMemorySqliteOwnerState.Opening;
            worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "ProjectSalieri.WorldMemory.SQLite"
            };
            worker.Start();
        }

        public WorldMemorySqliteOwnerState State
        {
            get { lock (stateGate) return state; }
        }

        public int WorkerThreadId => workerThreadId;
        public WorldMemorySqliteLifecycleResult LifecycleResult =>
            lifecycleResult;

        public static WorldMemorySqliteLifecycleResult TryOpen(
            string databasePath,
            string applicationVersion,
            DateTime createdAtUtc,
            out WorldMemorySqliteConnectionOwner owner)
        {
            owner = null;
            var candidate = new WorldMemorySqliteConnectionOwner();
            WorldMemorySqliteLifecycleResult result;
            try
            {
                result = candidate.InvokeOpening(() =>
                    candidate.OpenOnWorker(databasePath,
                        applicationVersion, createdAtUtc));
            }
            catch (Exception exception)
            {
                result = WorldMemorySqliteLifecycleResult.Failure(
                    WorldMemorySqliteFailureMapper.ToLifecycle(exception,
                        File.Exists(databasePath ?? string.Empty)),
                    databasePath, candidate.workerThreadId,
                    WorldMemorySqliteFailureMapper.BoundedDiagnostic(
                        exception));
            }

            candidate.lifecycleResult = result;
            lock (candidate.stateGate)
            {
                candidate.state = result.Succeeded
                    ? WorldMemorySqliteOwnerState.Ready
                    : WorldMemorySqliteOwnerState.Failed;
            }

            if (!result.Succeeded)
            {
                candidate.Dispose();
                return result;
            }

            owner = candidate;
            return result;
        }

        internal T Execute<T>(Func<SQLiteConnection, T> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            lock (stateGate)
            {
                if (state != WorldMemorySqliteOwnerState.Ready)
                {
                    throw new WorldMemorySqliteStoreException(
                        WorldMemorySqliteFailureKind.ProviderUnavailable,
                        "World Memory SQLite owner is not Ready.");
                }
            }
            return Enqueue(new WorkItem<T>(() => operation(connection), false));
        }

        internal void Execute(Action<SQLiteConnection> operation)
        {
            Execute(connectionValue =>
            {
                operation(connectionValue);
                return true;
            });
        }

        public void Dispose()
        {
            lock (stateGate)
            {
                if (state == WorldMemorySqliteOwnerState.Released ||
                    state == WorldMemorySqliteOwnerState.ShuttingDown)
                    return;
                state = WorldMemorySqliteOwnerState.ShuttingDown;
            }

            try
            {
                var release = new WorkItem<bool>(() =>
                {
                    if (connection != null)
                    {
                        connection.Dispose();
                        connection = null;
                    }
                    return true;
                }, true);
                pending.Add(release);
                release.Wait();
            }
            finally
            {
                pending.CompleteAdding();
                if (Thread.CurrentThread.ManagedThreadId != workerThreadId)
                    worker.Join();
                lock (stateGate)
                    state = WorldMemorySqliteOwnerState.Released;
            }
        }

        private WorldMemorySqliteLifecycleResult OpenOnWorker(
            string databasePath,
            string applicationVersion,
            DateTime createdAtUtc)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path is required.");
            string fullPath = Path.GetFullPath(databasePath);
            bool existed = File.Exists(fullPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new IOException("Database directory is unavailable.");
            Directory.CreateDirectory(directory);

            connection = new SQLiteConnection(fullPath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create |
                SQLiteOpenFlags.FullMutex, true);
            connection.BusyTimeout =
                TimeSpan.FromMilliseconds(BusyTimeoutMilliseconds);
            string journal = connection.ExecuteScalar<string>(
                "PRAGMA journal_mode=DELETE");
            connection.Execute("PRAGMA synchronous=FULL");
            connection.Execute("PRAGMA foreign_keys=ON");
            int synchronous = connection.ExecuteScalar<int>(
                "PRAGMA synchronous");
            bool foreignKeys = connection.ExecuteScalar<int>(
                "PRAGMA foreign_keys") == 1;
            int busy = connection.ExecuteScalar<int>("PRAGMA busy_timeout");
            if (!string.Equals(journal, "delete",
                    StringComparison.OrdinalIgnoreCase) ||
                synchronous != 2 || !foreignKeys ||
                busy != BusyTimeoutMilliseconds)
            {
                throw new WorldMemorySqliteStoreException(
                    WorldMemorySqliteFailureKind.OpenFailed,
                    "Required SQLite PRAGMA policy was not established.");
            }

            int version = connection.ExecuteScalar<int>(
                "PRAGMA user_version");
            WorldMemorySqliteLifecycleStatus status;
            if (!existed)
            {
                if (version != 0)
                {
                    throw new WorldMemorySqliteStoreException(
                        WorldMemorySqliteFailureKind.UnsupportedSchema,
                        "New database did not start at schema version 0.");
                }
                WorldMemorySqliteSchemaV1.Create(connection,
                    applicationVersion, createdAtUtc);
                status = WorldMemorySqliteLifecycleStatus
                    .FreshDatabaseCreated;
            }
            else
            {
                if (version != WorldMemorySqliteSchemaV1.Version)
                {
                    throw new WorldMemorySqliteStoreException(
                        WorldMemorySqliteFailureKind.UnsupportedSchema,
                        "Existing World Memory schema is unsupported: " +
                        version);
                }
                status = WorldMemorySqliteLifecycleStatus.OpenedExisting;
            }

            WorldMemorySqliteSchemaV1.Verify(connection);
            string sqliteVersion = connection.ExecuteScalar<string>(
                "SELECT sqlite_version()");
            return new WorldMemorySqliteLifecycleResult(status, fullPath,
                sqliteVersion, WorldMemorySqliteSchemaV1.Version, journal,
                synchronous, foreignKeys, busy, workerThreadId,
                string.Empty);
        }

        private T InvokeOpening<T>(Func<T> operation)
        {
            return Enqueue(new WorkItem<T>(operation, true));
        }

        private T Enqueue<T>(WorkItem<T> item)
        {
            if (!pending.TryAdd(item))
            {
                throw new WorldMemorySqliteStoreException(
                    WorldMemorySqliteFailureKind.BusyTimedOut,
                    "World Memory SQLite pending capacity is full.");
            }
            return item.Wait();
        }

        private void WorkerLoop()
        {
            workerThreadId = Thread.CurrentThread.ManagedThreadId;
            foreach (IWorkItem item in pending.GetConsumingEnumerable())
            {
                bool shuttingDown;
                lock (stateGate)
                    shuttingDown =
                        state == WorldMemorySqliteOwnerState.ShuttingDown;
                if (shuttingDown && !item.AllowDuringShutdown)
                    item.Cancel();
                else
                    item.Run();
            }
        }

        private interface IWorkItem
        {
            bool AllowDuringShutdown { get; }
            void Run();
            void Cancel();
        }

        private sealed class WorkItem<T> : IWorkItem
        {
            private readonly Func<T> operation;
            private readonly ManualResetEventSlim completed =
                new ManualResetEventSlim(false);
            private T result;
            private Exception exception;

            public bool AllowDuringShutdown { get; }

            public WorkItem(Func<T> operation, bool allowDuringShutdown)
            {
                this.operation = operation;
                AllowDuringShutdown = allowDuringShutdown;
            }

            public void Run()
            {
                try { result = operation(); }
                catch (Exception caught) { exception = caught; }
                finally { completed.Set(); }
            }

            public void Cancel()
            {
                exception = new WorldMemorySqliteStoreException(
                    WorldMemorySqliteFailureKind.ProviderUnavailable,
                    "World Memory SQLite operation was cancelled by shutdown.");
                completed.Set();
            }

            public T Wait()
            {
                completed.Wait();
                completed.Dispose();
                if (exception != null)
                    throw exception;
                return result;
            }
        }
    }
}
