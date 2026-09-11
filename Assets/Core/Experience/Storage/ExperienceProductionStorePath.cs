// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.IO;

using UnityEngine;

namespace SalieriAI.Core.Experience.Storage
{
    /// <summary>
    /// Sole Production path contract for the M3 JSON Experience store.
    /// </summary>
    public static class ExperienceProductionStorePath
    {
        public static string Resolve()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "ProjectSalieri",
                "Experience",
                "experience_records_v0.json");
        }
    }
}
