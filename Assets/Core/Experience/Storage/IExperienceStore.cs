// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections.Generic;

namespace SalieriAI.Core.Experience.Storage
{
    public interface IExperienceStore
    {
        string StoragePath { get; }

        ExperienceSaveResult Save(ExperienceRecord record);

        bool TryReadByRecordId(
            string recordId,
            out ExperienceRecord record,
            out string error);

        bool TryReadByRecallKey(
            string recallKey,
            out IReadOnlyList<ExperienceRecord> records,
            out string error);
    }
}
