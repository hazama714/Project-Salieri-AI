// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Perception.ObjectDetection
{
    /// <summary>
    /// YOLOによる1回分の物体検出結果。
    ///
    /// Texture、Mat、GameObjectなどのUnity/OpenCV実体は保持しない。
    /// 将来のTracking、Skill、DBへ渡せる純粋なデータ構造。
    /// </summary>
    [Serializable]
    public sealed class VisibleObjectSet
    {
        public string SessionId;

        public long FrameId;

        public long CapturedAtUnixMilliseconds;

        public int FrameWidth;

        public int FrameHeight;

        public int Rotation;

        public VisibleObject[] Objects =
            new VisibleObject[0];

        public int Count =>
            Objects != null
                ? Objects.Length
                : 0;
    }

    /// <summary>
    /// 1つの検出物体。
    /// </summary>
    [Serializable]
    public sealed class VisibleObject
    {
        public string DetectionId;

        public int ClassId;

        public string ClassLabel;

        public float Confidence;

        /// <summary>
        /// 元画像上の左上・右下座標。
        /// </summary>
        public float X1;
        public float Y1;
        public float X2;
        public float Y2;

        /// <summary>
        /// 0～1へ正規化した座標。
        /// </summary>
        public float NormalizedX1;
        public float NormalizedY1;
        public float NormalizedX2;
        public float NormalizedY2;

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
    }
}