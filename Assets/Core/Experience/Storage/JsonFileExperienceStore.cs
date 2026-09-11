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
using System.IO;
using System.Text;

using SalieriAI.Core.Behavior.FindPointAsk;
using SalieriAI.Core.Experience.Recall;
using SalieriAI.Core.Input;

using UnityEngine;

namespace SalieriAI.Core.Experience.Storage
{
    /// <summary>
    /// JsonUtilityを用いるv0 file store。RecordIdとvalid RK0 exact lookupだけを行う。
    /// </summary>
    public sealed class JsonFileExperienceStore : IExperienceStore
    {
        public const int CurrentSchemaVersion = 1;
        private static readonly object FileGate = new object();
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public string StoragePath { get; }

        public JsonFileExperienceStore(string storagePath)
        {
            StoragePath = string.IsNullOrWhiteSpace(storagePath)
                ? string.Empty
                : Path.GetFullPath(storagePath);
        }

        public ExperienceSaveResult Save(ExperienceRecord record)
        {
            if (record == null || !record.IsValid)
            {
                return new ExperienceSaveResult(
                    ExperienceSaveStatus.InvalidRecord,
                    record,
                    "ExperienceRecord is invalid.");
            }

            if (StoragePath.Length == 0)
            {
                return new ExperienceSaveResult(
                    ExperienceSaveStatus.StorageFailed,
                    record,
                    "Storage path is empty.");
            }

            lock (FileGate)
            {
                if (!TryLoadCollection(out ExperienceRecordCollectionDto data, out string error))
                {
                    return new ExperienceSaveResult(
                        ExperienceSaveStatus.StorageFailed,
                        record,
                        error);
                }

                for (int i = 0; i < data.records.Count; i++)
                {
                    ExperienceRecordDto existing = data.records[i];
                    if (existing == null)
                        continue;
                    if (string.Equals(
                            existing.questionId,
                            record.QuestionId,
                            StringComparison.Ordinal))
                    {
                        return new ExperienceSaveResult(
                            ExperienceSaveStatus.DuplicateQuestion,
                            record,
                            "QuestionId is already stored.");
                    }
                    if (string.Equals(
                            existing.recordId,
                            record.RecordId,
                            StringComparison.Ordinal))
                    {
                        return new ExperienceSaveResult(
                            ExperienceSaveStatus.DuplicateRecord,
                            record,
                            "RecordId is already stored.");
                    }
                }

                data.records.Add(ToDto(record));
                if (!TryWriteCollection(data, out error))
                {
                    return new ExperienceSaveResult(
                        ExperienceSaveStatus.StorageFailed,
                        record,
                        error);
                }

                return new ExperienceSaveResult(
                    ExperienceSaveStatus.Saved,
                    record,
                    string.Empty);
            }
        }

        public bool TryReadByRecordId(
            string recordId,
            out ExperienceRecord record,
            out string error)
        {
            record = null;
            error = string.Empty;
            string id = recordId == null ? string.Empty : recordId.Trim();
            if (id.Length == 0)
            {
                error = "RecordId is empty.";
                return false;
            }

            lock (FileGate)
            {
                if (!TryLoadCollection(out ExperienceRecordCollectionDto data, out error))
                    return false;

                for (int i = 0; i < data.records.Count; i++)
                {
                    ExperienceRecordDto dto = data.records[i];
                    if (dto == null || !string.Equals(
                            dto.recordId,
                            id,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!TryFromDto(dto, out record, out error))
                        return false;
                    return true;
                }

                error = "ExperienceRecord was not found.";
                return false;
            }
        }

        public bool TryReadByRecallKey(
            string recallKey,
            out IReadOnlyList<ExperienceRecord> records,
            out string error)
        {
            var matches = new List<ExperienceRecord>();
            records = matches;
            error = string.Empty;
            string key = recallKey == null ? string.Empty : recallKey.Trim();
            if (!RecallKeyV0.TryParse(key, out _))
            {
                error = "RecallKey is not a supported RK0 key.";
                return false;
            }

            lock (FileGate)
            {
                if (!TryLoadCollection(out ExperienceRecordCollectionDto data, out error))
                    return false;

                for (int i = 0; i < data.records.Count; i++)
                {
                    ExperienceRecordDto dto = data.records[i];
                    if (dto == null ||
                        !string.Equals(dto.recallKey, key, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!TryFromDto(dto, out ExperienceRecord record, out error))
                        return false;
                    matches.Add(record);
                }
            }

            return true;
        }

        private bool TryLoadCollection(
            out ExperienceRecordCollectionDto data,
            out string error)
        {
            data = null;
            error = string.Empty;
            try
            {
                if (!File.Exists(StoragePath))
                {
                    data = new ExperienceRecordCollectionDto();
                    return true;
                }

                string json = File.ReadAllText(StoragePath, Utf8NoBom);
                data = JsonUtility.FromJson<ExperienceRecordCollectionDto>(json);
                if (data == null ||
                    data.schemaVersion != CurrentSchemaVersion ||
                    data.records == null)
                {
                    data = null;
                    error = "Experience store is corrupt or has an unsupported schema.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                data = null;
                error = "Experience store read failed: " + exception.Message;
                return false;
            }
        }

        private bool TryWriteCollection(
            ExperienceRecordCollectionDto data,
            out string error)
        {
            error = string.Empty;
            string temporaryPath = StoragePath + ".tmp";
            string backupPath = StoragePath + ".bak";
            try
            {
                string directory = Path.GetDirectoryName(StoragePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(temporaryPath, json, Utf8NoBom);
                if (File.Exists(StoragePath))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Replace(temporaryPath, StoragePath, backupPath, true);
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, StoragePath);
                }
                return true;
            }
            catch (Exception exception)
            {
                error = "Experience store write failed: " + exception.Message;
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                    // The primary save result remains authoritative.
                }
            }
        }

        private static ExperienceRecordDto ToDto(ExperienceRecord record)
        {
            return new ExperienceRecordDto
            {
                recordId = record.RecordId,
                createdAtUtc = record.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                behaviorRunId = record.BehaviorRunId,
                questionId = record.QuestionId,
                targetKey = record.TargetKey,
                trackId = record.TrackId,
                observationReference = record.ObservationReference,
                recallKey = record.RecallKey,
                questionType = (int)record.QuestionType,
                rawAnswer = record.RawAnswer,
                normalizedAnswer = record.NormalizedAnswer,
                inputSource = (int)record.InputSource,
                hasConfidence = record.HasConfidence,
                confidence = record.Confidence,
                source = (int)record.Source
            };
        }

        private static bool TryFromDto(
            ExperienceRecordDto dto,
            out ExperienceRecord record,
            out string error)
        {
            record = null;
            error = string.Empty;
            if (!DateTime.TryParseExact(
                    dto.createdAtUtc,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime createdAt))
            {
                error = "ExperienceRecord CreatedAt is invalid.";
                return false;
            }

            record = new ExperienceRecord(
                dto.recordId,
                createdAt,
                dto.behaviorRunId,
                dto.questionId,
                dto.targetKey,
                dto.trackId,
                dto.observationReference,
                dto.recallKey,
                (QuestionType)dto.questionType,
                dto.rawAnswer,
                dto.normalizedAnswer,
                (UserInputSource)dto.inputSource,
                dto.hasConfidence,
                dto.confidence,
                (ExperienceRecordSource)dto.source);
            if (!record.IsValid)
            {
                record = null;
                error = "Stored ExperienceRecord is invalid.";
                return false;
            }
            return true;
        }

        [Serializable]
        private sealed class ExperienceRecordCollectionDto
        {
            public int schemaVersion = CurrentSchemaVersion;
            public List<ExperienceRecordDto> records =
                new List<ExperienceRecordDto>();
        }

        [Serializable]
        private sealed class ExperienceRecordDto
        {
            public string recordId;
            public string createdAtUtc;
            public string behaviorRunId;
            public string questionId;
            public string targetKey;
            public int trackId;
            public string observationReference;
            public string recallKey;
            public int questionType;
            public string rawAnswer;
            public string normalizedAnswer;
            public int inputSource;
            public bool hasConfidence;
            public float confidence;
            public int source;
        }
    }
}
