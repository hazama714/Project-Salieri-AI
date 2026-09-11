// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.IO;

using SQLite;

namespace SalieriAI.Core.Memory.WorldModel.Storage.Sqlite
{
    internal static class WorldMemorySqliteFailureMapper
    {
        public static WorldMemorySqliteLifecycleStatus ToLifecycle(
            Exception exception,
            bool openingExisting)
        {
            if (IsProviderUnavailable(exception))
                return WorldMemorySqliteLifecycleStatus
                    .NativeBindingUnavailable;
            if (exception is WorldMemorySqliteStoreException typed)
            {
                switch (typed.FailureKind)
                {
                    case WorldMemorySqliteFailureKind.UnsupportedSchema:
                        return WorldMemorySqliteLifecycleStatus
                            .UnsupportedSchema;
                    case WorldMemorySqliteFailureKind.Corrupt:
                        return WorldMemorySqliteLifecycleStatus.Corrupt;
                    case WorldMemorySqliteFailureKind.BusyTimedOut:
                        return WorldMemorySqliteLifecycleStatus.BusyTimedOut;
                    case WorldMemorySqliteFailureKind.DiskOrIoFailure:
                        return WorldMemorySqliteLifecycleStatus
                            .DiskOrIoFailure;
                }
            }
            if (exception is SQLiteException sqlite)
            {
                if (sqlite.Result == SQLite3.Result.Corrupt ||
                    sqlite.Result == SQLite3.Result.NonDBFile ||
                    sqlite.Result == SQLite3.Result.Format)
                {
                    return WorldMemorySqliteLifecycleStatus.Corrupt;
                }
                if (sqlite.Result == SQLite3.Result.Busy ||
                    sqlite.Result == SQLite3.Result.Locked)
                {
                    return WorldMemorySqliteLifecycleStatus.BusyTimedOut;
                }
                if (IsIo(sqlite.Result))
                {
                    return WorldMemorySqliteLifecycleStatus
                        .DiskOrIoFailure;
                }
            }
            if (exception is IOException || exception is UnauthorizedAccessException)
                return WorldMemorySqliteLifecycleStatus.DiskOrIoFailure;
            return openingExisting
                ? WorldMemorySqliteLifecycleStatus.OpenFailed
                : WorldMemorySqliteLifecycleStatus.OpenFailed;
        }

        public static WorldMemorySqliteFailureKind ToFailureKind(
            Exception exception)
        {
            if (exception is WorldMemorySqliteStoreException typed)
                return typed.FailureKind;
            if (IsProviderUnavailable(exception))
                return WorldMemorySqliteFailureKind.ProviderUnavailable;
            if (exception is SQLiteException sqlite)
            {
                if (sqlite.Result == SQLite3.Result.Busy ||
                    sqlite.Result == SQLite3.Result.Locked)
                    return WorldMemorySqliteFailureKind.BusyTimedOut;
                if (sqlite.Result == SQLite3.Result.Constraint)
                    return WorldMemorySqliteFailureKind.ConstraintViolation;
                if (sqlite.Result == SQLite3.Result.Corrupt ||
                    sqlite.Result == SQLite3.Result.NonDBFile ||
                    sqlite.Result == SQLite3.Result.Format)
                    return WorldMemorySqliteFailureKind.Corrupt;
                if (IsIo(sqlite.Result))
                    return WorldMemorySqliteFailureKind.DiskOrIoFailure;
            }
            if (exception is IOException || exception is UnauthorizedAccessException)
                return WorldMemorySqliteFailureKind.DiskOrIoFailure;
            return WorldMemorySqliteFailureKind.DiskOrIoFailure;
        }

        public static string BoundedDiagnostic(Exception exception)
        {
            if (exception == null)
                return string.Empty;
            string text = exception.GetType().Name + ": " + exception.Message;
            return text.Length <= 512 ? text : text.Substring(0, 512);
        }

        private static bool IsProviderUnavailable(Exception exception)
        {
            return exception is DllNotFoundException ||
                exception is EntryPointNotFoundException ||
                exception is BadImageFormatException;
        }

        private static bool IsIo(SQLite3.Result result)
        {
            return result == SQLite3.Result.IOError ||
                result == SQLite3.Result.Full ||
                result == SQLite3.Result.ReadOnly ||
                result == SQLite3.Result.CannotOpen ||
                result == SQLite3.Result.Perm ||
                result == SQLite3.Result.AccessDenied;
        }
    }
}
