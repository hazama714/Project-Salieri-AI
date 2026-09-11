// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Perception.ObjectTracking
{
    /// <summary>
    /// BYTETrackerによる1回分の追跡結果。
    ///
    /// UnityEngine.Object、Texture、OpenCV Matなどは保持しない。
    /// 将来の対象選択、Skill、DBへ渡せる純粋データ。
    /// </summary>
    [Serializable]
    public sealed class TrackedObjectSet
    {
        public string SessionId;

        /// <summary>
        /// 元となったYOLO SnapshotのFrameId。
        /// </summary>
        public long SourceFrameId;

        /// <summary>
        /// BYTETrackerへ入力した通算回数。
        /// </summary>
        public long TrackerFrameSequence;

        public long CapturedAtUnixMilliseconds;

        public int FrameWidth;
        public int FrameHeight;
        public int Rotation;

        public TrackedObject[] Objects =
            new TrackedObject[0];

        public int Count =>
            Objects != null
                ? Objects.Length
                : 0;

        public int StableCount
        {
            get
            {
                if (Objects == null)
                    return 0;

                int count = 0;

                for (int i = 0; i < Objects.Length; i++)
                {
                    if (Objects[i] != null &&
                        Objects[i].IsStable)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }

    /// <summary>
    /// 同一対象として追跡されている物体。
    /// </summary>
    [Serializable]
    public sealed class TrackedObject
    {
        public int TrackId;

        /// <summary>
        /// SessionIdとTrackIdを組み合わせた実行中の一意キー。
        /// </summary>
        public string TrackKey;

        /// <summary>
        /// 直近で対応付けられたYOLO DetectionId。
        /// 対応がないフレームではnullの場合がある。
        /// </summary>
        public string LatestDetectionId;

        public int ClassId;
        public string ClassLabel;

        public float Confidence;

        public float X1;
        public float Y1;
        public float X2;
        public float Y2;

        public float NormalizedX1;
        public float NormalizedY1;
        public float NormalizedX2;
        public float NormalizedY2;

        public long FirstSeenSourceFrameId;
        public long LastSeenSourceFrameId;

        /// <summary>
        /// 連続してActive Trackとして確認されたSnapshot数。
        /// </summary>
        public int ConsecutiveVisibleSnapshots;

        /// <summary>
        /// Track生成後にActiveとして確認された累計Snapshot数。
        /// </summary>
        public int TotalVisibleSnapshots;

        public bool IsStable;

        public float PixelWidth =>
            Math.Max(0f, X2 - X1);

        public float PixelHeight =>
            Math.Max(0f, Y2 - Y1);

        public float PixelCenterX =>
            (X1 + X2) * 0.5f;

        public float PixelCenterY =>
            (Y1 + Y2) * 0.5f;

        public float NormalizedWidth =>
            Math.Max(
                0f,
                NormalizedX2 - NormalizedX1
            );

        public float NormalizedHeight =>
            Math.Max(
                0f,
                NormalizedY2 - NormalizedY1
            );

        public float NormalizedCenterX =>
            (NormalizedX1 + NormalizedX2) * 0.5f;

        public float NormalizedCenterY =>
            (NormalizedY1 + NormalizedY2) * 0.5f;

        public float NormalizedArea =>
            NormalizedWidth *
            NormalizedHeight;
    }
}