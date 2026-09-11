// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.IO;

using UnityEngine;

namespace SalieriAI.Core.Memory.WorldModel.Storage.Sqlite
{
    public static class WorldMemorySqlitePathContract
    {
        public const string ProductDirectory = "ProjectSalieri";
        public const string StoreDirectory = "WorldMemory";
        public const string DatabaseFileName = "world_memory_v1.sqlite3";

        public static string ResolveProductionPath()
        {
            return FromPersistentDataRoot(Application.persistentDataPath);
        }

        public static string FromPersistentDataRoot(string persistentDataRoot)
        {
            if (string.IsNullOrWhiteSpace(persistentDataRoot))
            {
                throw new ArgumentException(
                    "A persistent data root is required.",
                    nameof(persistentDataRoot));
            }

            return Path.Combine(persistentDataRoot, ProductDirectory,
                StoreDirectory, DatabaseFileName);
        }
    }
}
