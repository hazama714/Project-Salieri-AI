// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Perception.ObjectTracking;

namespace SalieriAI.Core.Perception.ObjectTargeting
{
    /// <summary>
    /// 対象が選ばれた理由。
    /// </summary>
    public enum ObservationTargetSelectionReason
    {
        InitialSelection = 0,

        MaintainedCurrentTarget = 1,

        SwitchedForHigherScore = 2,

        ReplacedUnavailableTarget = 3
    }

    /// <summary>
    /// 現在の観察対象。
    ///
    /// Texture、Mat、Transformなどは保持しない。
    /// TrackedObjectの値をコピーしたSnapshotだけを保持する。
    /// </summary>
    [Serializable]
    public sealed class ObservationTarget
    {
        public string SessionId;

        public string TargetKey;

        public long SourceFrameId;

        public long SelectedAtUnixMilliseconds;

        public long UpdatedAtUnixMilliseconds;

        public ObservationTargetSelectionReason
            SelectionReason;

        /// <summary>
        /// 0～1へ正規化された総合選択スコア。
        /// </summary>
        public float SelectionScore;

        public float CentralityScore;

        public float ConfidenceScore;

        public float AreaScore;

        public float PersistenceScore;

        /// <summary>
        /// 選択時点のTrackedObjectコピー。
        /// </summary>
        public TrackedObject TrackSnapshot;

        public int TrackId =>
            TrackSnapshot != null
                ? TrackSnapshot.TrackId
                : -1;

        public int ClassId =>
            TrackSnapshot != null
                ? TrackSnapshot.ClassId
                : -1;

        public string ClassLabel =>
            TrackSnapshot != null
                ? TrackSnapshot.ClassLabel
                : string.Empty;

        public float Confidence =>
            TrackSnapshot != null
                ? TrackSnapshot.Confidence
                : 0f;

        public bool IsStable =>
            TrackSnapshot != null &&
            TrackSnapshot.IsStable;

        public float NormalizedCenterX =>
            TrackSnapshot != null
                ? TrackSnapshot.NormalizedCenterX
                : 0.5f;

        public float NormalizedCenterY =>
            TrackSnapshot != null
                ? TrackSnapshot.NormalizedCenterY
                : 0.5f;

        public float NormalizedWidth =>
            TrackSnapshot != null
                ? TrackSnapshot.NormalizedWidth
                : 0f;

        public float NormalizedHeight =>
            TrackSnapshot != null
                ? TrackSnapshot.NormalizedHeight
                : 0f;
    }
}