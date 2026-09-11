// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

using SalieriAI.Core.Experience.Storage;

namespace SalieriAI.Core.Experience.Recall
{
    public enum RecallStatus
    {
        Known = 0,
        NoKey = 1,
        NoMatch = 2,
        Ambiguous = 3,
        StoreError = 4,
        UnsupportedKey = 5
    }

    public sealed class ExperienceRecallResult
    {
        public RecallStatus Status { get; }
        public string RecallKey { get; }
        public string NormalizedAnswer { get; }
        public IReadOnlyList<string> MatchedRecordIds { get; }
        public string Error { get; }
        public bool IsKnown => Status == RecallStatus.Known;

        public ExperienceRecallResult(
            RecallStatus status,
            string recallKey,
            string normalizedAnswer,
            IReadOnlyList<string> matchedRecordIds,
            string error)
        {
            Status = status;
            RecallKey = recallKey == null ? string.Empty : recallKey.Trim();
            NormalizedAnswer = normalizedAnswer == null
                ? string.Empty
                : normalizedAnswer.Trim();
            MatchedRecordIds = matchedRecordIds ?? Array.Empty<string>();
            Error = error ?? string.Empty;
        }
    }

    public sealed class ExperienceRecallResolver
    {
        private readonly IExperienceStore store;

        public ExperienceRecallResolver(IExperienceStore store)
        {
            this.store = store;
        }

        public ExperienceRecallResult Resolve(string recallKey)
        {
            string keyText = recallKey == null ? string.Empty : recallKey.Trim();
            if (keyText.Length == 0)
            {
                return Result(RecallStatus.NoKey, keyText, string.Empty, null,
                    "RecallKey is unavailable.");
            }
            if (!RecallKeyV0.TryParse(keyText, out _))
            {
                return Result(RecallStatus.UnsupportedKey, keyText, string.Empty,
                    null, "RecallKey is legacy, malformed, or unsupported.");
            }
            if (store == null)
            {
                return Result(RecallStatus.StoreError, keyText, string.Empty,
                    null, "Experience store is unavailable.");
            }
            if (!store.TryReadByRecallKey(
                    keyText,
                    out IReadOnlyList<ExperienceRecord> records,
                    out string error))
            {
                return Result(RecallStatus.StoreError, keyText, string.Empty,
                    null, error);
            }
            if (records == null || records.Count == 0)
            {
                return Result(RecallStatus.NoMatch, keyText, string.Empty,
                    null, string.Empty);
            }

            var recordIds = new List<string>(records.Count);
            string answer = string.Empty;
            for (int i = 0; i < records.Count; i++)
            {
                ExperienceRecord record = records[i];
                if (record == null || !record.IsValid ||
                    !string.Equals(record.RecallKey, keyText, StringComparison.Ordinal))
                {
                    return Result(RecallStatus.StoreError, keyText, string.Empty,
                        recordIds, "Store returned an invalid recall record.");
                }

                recordIds.Add(record.RecordId);
                if (i == 0)
                    answer = record.NormalizedAnswer;
                else if (!string.Equals(
                             answer,
                             record.NormalizedAnswer,
                             StringComparison.Ordinal))
                {
                    return Result(RecallStatus.Ambiguous, keyText, string.Empty,
                        recordIds, "Matched experiences contain conflicting answers.");
                }
            }

            return Result(RecallStatus.Known, keyText, answer, recordIds,
                string.Empty);
        }

        private static ExperienceRecallResult Result(
            RecallStatus status,
            string key,
            string answer,
            IReadOnlyList<string> ids,
            string error)
        {
            return new ExperienceRecallResult(status, key, answer, ids, error);
        }
    }
}
