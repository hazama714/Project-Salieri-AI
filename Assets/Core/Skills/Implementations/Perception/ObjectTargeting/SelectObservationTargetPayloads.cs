// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Perception.ObjectTargeting;
using SalieriAI.Core.Perception.ObjectTracking;

namespace SalieriAI.Core.Skills.Perception.ObjectTargeting
{
    [Serializable]
    public sealed class SelectObservationTargetRequest :
        ISkillPayload
    {
        /// <summary>
        /// 空の場合は現在の確定済み対象を返す。
        /// 値がある場合は、そのTargetKeyと現在対象が一致することを要求する。
        /// </summary>
        public string ExpectedTargetKey = string.Empty;

        public SelectObservationTargetRequest()
        {
        }

        public SelectObservationTargetRequest(
            string expectedTargetKey)
        {
            ExpectedTargetKey =
                expectedTargetKey ?? string.Empty;
        }
    }

    /// <summary>
    /// Skill境界を越えて渡す、UnityEngine.Objectを含まない観察対象スナップショット。
    /// UI、首、眼球、腕への命令は含まない。
    /// </summary>
    [Serializable]
    public sealed class SelectObservationTargetResult :
        ISkillPayload
    {
        public string SessionId = string.Empty;
        public string TargetKey = string.Empty;

        public long SourceFrameId;
        public long SelectedAtUnixMilliseconds;
        public long UpdatedAtUnixMilliseconds;
        public long CapturedBySkillAtUnixMilliseconds;

        public string SelectionReason = string.Empty;

        public float SelectionScore;
        public float CentralityScore;
        public float ConfidenceScore;
        public float AreaScore;
        public float PersistenceScore;

        public int TrackId;
        public int ClassId;
        public string ClassLabel = string.Empty;
        public float Confidence;

        public float NormalizedX1;
        public float NormalizedY1;
        public float NormalizedX2;
        public float NormalizedY2;

        public float NormalizedCenterX;
        public float NormalizedCenterY;
        public float NormalizedWidth;
        public float NormalizedHeight;
        public float NormalizedArea;

        public long FirstSeenSourceFrameId;
        public long LastSeenSourceFrameId;
        public int ConsecutiveVisibleSnapshots;
        public int TotalVisibleSnapshots;
        public bool IsStable;

        public static SelectObservationTargetResult
            FromTarget(
                ObservationTarget target)
        {
            if (target == null ||
                target.TrackSnapshot == null)
            {
                return null;
            }

            TrackedObject track =
                target.TrackSnapshot;

            float width = Math.Max(
                0f,
                track.NormalizedX2 -
                track.NormalizedX1
            );

            float height = Math.Max(
                0f,
                track.NormalizedY2 -
                track.NormalizedY1
            );

            return new SelectObservationTargetResult
            {
                SessionId =
                    target.SessionId ?? string.Empty,

                TargetKey =
                    target.TargetKey ?? string.Empty,

                SourceFrameId =
                    target.SourceFrameId,

                SelectedAtUnixMilliseconds =
                    target.SelectedAtUnixMilliseconds,

                UpdatedAtUnixMilliseconds =
                    target.UpdatedAtUnixMilliseconds,

                CapturedBySkillAtUnixMilliseconds =
                    DateTimeOffset.UtcNow
                        .ToUnixTimeMilliseconds(),

                SelectionReason =
                    target.SelectionReason.ToString(),

                SelectionScore =
                    target.SelectionScore,

                CentralityScore =
                    target.CentralityScore,

                ConfidenceScore =
                    target.ConfidenceScore,

                AreaScore =
                    target.AreaScore,

                PersistenceScore =
                    target.PersistenceScore,

                TrackId =
                    track.TrackId,

                ClassId =
                    track.ClassId,

                ClassLabel =
                    track.ClassLabel ?? string.Empty,

                Confidence =
                    track.Confidence,

                NormalizedX1 =
                    track.NormalizedX1,

                NormalizedY1 =
                    track.NormalizedY1,

                NormalizedX2 =
                    track.NormalizedX2,

                NormalizedY2 =
                    track.NormalizedY2,

                NormalizedCenterX =
                    track.NormalizedX1 +
                    width * 0.5f,

                NormalizedCenterY =
                    track.NormalizedY1 +
                    height * 0.5f,

                NormalizedWidth =
                    width,

                NormalizedHeight =
                    height,

                NormalizedArea =
                    width * height,

                FirstSeenSourceFrameId =
                    track.FirstSeenSourceFrameId,

                LastSeenSourceFrameId =
                    track.LastSeenSourceFrameId,

                ConsecutiveVisibleSnapshots =
                    track.ConsecutiveVisibleSnapshots,

                TotalVisibleSnapshots =
                    track.TotalVisibleSnapshots,

                IsStable =
                    track.IsStable
            };
        }
    }
}
