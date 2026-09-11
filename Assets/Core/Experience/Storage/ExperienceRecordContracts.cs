// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Behavior.FindPointAsk;
using SalieriAI.Core.Experience.AnswerBinding;
using SalieriAI.Core.Input;

namespace SalieriAI.Core.Experience.Storage
{
    public enum ExperienceRecordSource
    {
        Unknown = 0,
        AnswerBinding = 1
    }

    public sealed class ExperienceRecord
    {
        public string RecordId { get; }
        public DateTime CreatedAt { get; }
        public string BehaviorRunId { get; }
        public string QuestionId { get; }
        public string TargetKey { get; }
        public int TrackId { get; }
        public string ObservationReference { get; }
        public string RecallKey { get; }
        public QuestionType QuestionType { get; }
        public string RawAnswer { get; }
        public string NormalizedAnswer { get; }
        public UserInputSource InputSource { get; }
        public bool HasConfidence { get; }
        public float Confidence { get; }
        public ExperienceRecordSource Source { get; }

        public ExperienceRecord(
            string recordId,
            DateTime createdAt,
            string behaviorRunId,
            string questionId,
            string targetKey,
            int trackId,
            string observationReference,
            string recallKey,
            QuestionType questionType,
            string rawAnswer,
            string normalizedAnswer,
            UserInputSource inputSource,
            bool hasConfidence,
            float confidence,
            ExperienceRecordSource source)
        {
            RecordId = Text(recordId);
            CreatedAt = Utc(createdAt);
            BehaviorRunId = Text(behaviorRunId);
            QuestionId = Text(questionId);
            TargetKey = Text(targetKey);
            TrackId = trackId;
            ObservationReference = Text(observationReference);
            RecallKey = string.IsNullOrWhiteSpace(recallKey)
                ? null
                : recallKey.Trim();
            QuestionType = questionType;
            RawAnswer = rawAnswer ?? string.Empty;
            NormalizedAnswer = Text(normalizedAnswer);
            InputSource = inputSource;
            HasConfidence = hasConfidence;
            Confidence = confidence;
            Source = source;
        }

        public bool IsValid =>
            RecordId.Length > 0 &&
            CreatedAt != default(DateTime) &&
            BehaviorRunId.Length > 0 &&
            QuestionId.Length > 0 &&
            TargetKey.Length > 0 &&
            TrackId >= 0 &&
            ObservationReference.Length > 0 &&
            QuestionType != QuestionType.Unknown &&
            NormalizedAnswer.Length > 0 &&
            InputSource != UserInputSource.Unknown &&
            Source != ExperienceRecordSource.Unknown &&
            !float.IsNaN(Confidence) &&
            !float.IsInfinity(Confidence);

        public static ExperienceRecord FromMatchedAnswer(
            string recordId,
            DateTime createdAt,
            AnswerContext answer)
        {
            if (answer == null || !answer.IsMatched)
                return null;

            TargetContext target = answer.TargetContext;
            return new ExperienceRecord(
                recordId,
                createdAt,
                answer.BehaviorRunId,
                answer.QuestionId,
                target.TargetKey,
                target.TrackId,
                target.ObservationReference,
                target.RecallKeyCandidate,
                target.QuestionType,
                answer.RawTranscript,
                answer.NormalizedAnswer,
                answer.InputSource,
                false,
                0f,
                ExperienceRecordSource.AnswerBinding);
        }

        private static string Text(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime))
                return value;
            return value.Kind == DateTimeKind.Utc
                ? value
                : value.ToUniversalTime();
        }
    }

    public enum ExperienceSaveStatus
    {
        Saved = 0,
        Ineligible = 1,
        DuplicateQuestion = 2,
        DuplicateRecord = 3,
        InvalidRecord = 4,
        StorageFailed = 5
    }

    public sealed class ExperienceSaveResult
    {
        public ExperienceSaveStatus Status { get; }
        public ExperienceRecord Record { get; }
        public string Error { get; }
        public bool Succeeded => Status == ExperienceSaveStatus.Saved;

        public ExperienceSaveResult(
            ExperienceSaveStatus status,
            ExperienceRecord record,
            string error)
        {
            Status = status;
            Record = record;
            Error = error ?? string.Empty;
        }
    }
}
