// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Experience.AnswerBinding;

namespace SalieriAI.Core.Experience.Storage
{
    public sealed class ExperiencePersistenceService
    {
        private readonly IExperienceStore store;

        public ExperiencePersistenceService(IExperienceStore store)
        {
            this.store = store;
        }

        public ExperienceSaveResult SaveMatched(
            AnswerBindingResult binding,
            DateTime createdAtUtc)
        {
            if (binding == null ||
                binding.Status != CorrelationStatus.Matched ||
                !binding.CanProceedToExperience)
            {
                return new ExperienceSaveResult(
                    ExperienceSaveStatus.Ineligible,
                    null,
                    "Only a Matched AnswerContext is eligible for save.");
            }

            if (store == null)
            {
                return new ExperienceSaveResult(
                    ExperienceSaveStatus.StorageFailed,
                    null,
                    "Experience store is unavailable.");
            }

            ExperienceRecord record = ExperienceRecord.FromMatchedAnswer(
                Guid.NewGuid().ToString("N"),
                createdAtUtc,
                binding.AnswerContext);
            if (record == null || !record.IsValid)
            {
                return new ExperienceSaveResult(
                    ExperienceSaveStatus.InvalidRecord,
                    record,
                    "Matched AnswerContext could not produce a valid record.");
            }

            return store.Save(record);
        }
    }
}
