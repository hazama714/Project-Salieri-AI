// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Perception.VisualSnapshots
{
    public enum VisualPixelFormat
    {
        Unknown = 0,
        Rgba32 = 1
    }

    /// <summary>
    /// Detection/Trackingと同じ未回転SourceFrame座標上の正規化BBox。
    /// X/Yは画像左上を原点とし、右/下を正方向とする。
    /// </summary>
    public sealed class VisualNormalizedBoundingBox
    {
        public float X1 { get; }
        public float Y1 { get; }
        public float X2 { get; }
        public float Y2 { get; }

        public VisualNormalizedBoundingBox(
            float x1,
            float y1,
            float x2,
            float y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public bool IsValid =>
            IsFinite(X1) &&
            IsFinite(Y1) &&
            IsFinite(X2) &&
            IsFinite(Y2) &&
            X1 >= 0f &&
            Y1 >= 0f &&
            X2 <= 1f &&
            Y2 <= 1f &&
            X2 > X1 &&
            Y2 > Y1;

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class VisualPixelBounds
    {
        public int Left { get; }
        public int Top { get; }
        public int RightExclusive { get; }
        public int BottomExclusive { get; }
        public int Width => RightExclusive - Left;
        public int Height => BottomExclusive - Top;

        public VisualPixelBounds(
            int left,
            int top,
            int rightExclusive,
            int bottomExclusive)
        {
            Left = left;
            Top = top;
            RightExclusive = rightExclusive;
            BottomExclusive = bottomExclusive;
        }

        public bool IsValid =>
            Left >= 0 &&
            Top >= 0 &&
            RightExclusive > Left &&
            BottomExclusive > Top;
    }

    /// <summary>
    /// YOLOへ渡したSourceFrameと同一のimmutable managed RGBA snapshot。
    /// ProducerのMat/Texture lifetimeには依存しない。
    /// </summary>
    public sealed class VisualFrameSnapshot
    {
        private const int RgbaChannels = 4;
        private readonly byte[] pixels;

        public string SessionId { get; }
        public long SourceFrameId { get; }
        public int Width { get; }
        public int Height { get; }
        public int Rotation { get; }
        public VisualPixelFormat PixelFormat { get; }
        public long CapturedAtUnixMilliseconds { get; }
        public int PixelByteCount => pixels.Length;

        public VisualFrameSnapshot(
            string sessionId,
            long sourceFrameId,
            int width,
            int height,
            int rotation,
            VisualPixelFormat pixelFormat,
            long capturedAtUnixMilliseconds,
            byte[] pixelData)
            : this(
                sessionId,
                sourceFrameId,
                width,
                height,
                rotation,
                pixelFormat,
                capturedAtUnixMilliseconds,
                pixelData,
                false)
        {
        }

        private VisualFrameSnapshot(
            string sessionId,
            long sourceFrameId,
            int width,
            int height,
            int rotation,
            VisualPixelFormat pixelFormat,
            long capturedAtUnixMilliseconds,
            byte[] pixelData,
            bool takeOwnership)
        {
            SessionId = Normalize(sessionId);
            SourceFrameId = sourceFrameId;
            Width = width;
            Height = height;
            Rotation = rotation;
            PixelFormat = pixelFormat;
            CapturedAtUnixMilliseconds = capturedAtUnixMilliseconds;
            pixels = pixelData == null
                ? Array.Empty<byte>()
                : takeOwnership
                    ? pixelData
                    : (byte[])pixelData.Clone();
        }

        internal static VisualFrameSnapshot FromOwnedRgba32(
            string sessionId,
            long sourceFrameId,
            int width,
            int height,
            int rotation,
            long capturedAtUnixMilliseconds,
            byte[] ownedPixels)
        {
            return new VisualFrameSnapshot(
                sessionId,
                sourceFrameId,
                width,
                height,
                rotation,
                VisualPixelFormat.Rgba32,
                capturedAtUnixMilliseconds,
                ownedPixels,
                true);
        }

        public bool IsValid
        {
            get
            {
                if (SessionId.Length == 0 ||
                    SourceFrameId <= 0 ||
                    Width <= 0 ||
                    Height <= 0 ||
                    PixelFormat != VisualPixelFormat.Rgba32 ||
                    CapturedAtUnixMilliseconds <= 0)
                {
                    return false;
                }

                try
                {
                    return pixels.Length == checked(Width * Height * RgbaChannels);
                }
                catch (OverflowException)
                {
                    return false;
                }
            }
        }

        public byte[] CopyPixels()
        {
            return (byte[])pixels.Clone();
        }

        public bool TryCreateCrop(
            VisualNormalizedBoundingBox boundingBox,
            out VisualCropSnapshot crop,
            out string error)
        {
            crop = null;
            error = string.Empty;
            if (!IsValid)
            {
                error = "VisualFrameSnapshot is invalid.";
                return false;
            }
            if (boundingBox == null || !boundingBox.IsValid)
            {
                error = "Normalized bounding box is invalid.";
                return false;
            }

            int left = Math.Max(0, Math.Min(
                Width - 1,
                (int)Math.Floor(boundingBox.X1 * Width)));
            int top = Math.Max(0, Math.Min(
                Height - 1,
                (int)Math.Floor(boundingBox.Y1 * Height)));
            int right = Math.Max(left + 1, Math.Min(
                Width,
                (int)Math.Ceiling(boundingBox.X2 * Width)));
            int bottom = Math.Max(top + 1, Math.Min(
                Height,
                (int)Math.Ceiling(boundingBox.Y2 * Height)));

            var bounds = new VisualPixelBounds(left, top, right, bottom);
            if (!bounds.IsValid)
            {
                error = "Bounding box could not be resolved to pixel bounds.";
                return false;
            }

            byte[] cropPixels = new byte[
                checked(bounds.Width * bounds.Height * RgbaChannels)];
            int sourceStride = Width * RgbaChannels;
            int cropStride = bounds.Width * RgbaChannels;
            for (int y = 0; y < bounds.Height; y++)
            {
                System.Buffer.BlockCopy(
                    pixels,
                    (bounds.Top + y) * sourceStride + bounds.Left * RgbaChannels,
                    cropPixels,
                    y * cropStride,
                    cropStride);
            }

            crop = VisualCropSnapshot.FromOwnedRgba32(
                SessionId,
                SourceFrameId,
                Width,
                Height,
                Rotation,
                CapturedAtUnixMilliseconds,
                boundingBox,
                bounds,
                cropPixels);
            return crop.IsValid;
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }

    /// <summary>
    /// exact SourceFrameから生成された、対象BBoxのimmutable crop。
    /// </summary>
    public sealed class VisualCropSnapshot
    {
        private const int RgbaChannels = 4;
        private readonly byte[] pixels;

        public string SessionId { get; }
        public long SourceFrameId { get; }
        public int SourceWidth { get; }
        public int SourceHeight { get; }
        public int Rotation { get; }
        public VisualPixelFormat PixelFormat { get; }
        public long CapturedAtUnixMilliseconds { get; }
        public VisualNormalizedBoundingBox BoundingBox { get; }
        public VisualPixelBounds PixelBounds { get; }
        public int CropWidth => PixelBounds != null ? PixelBounds.Width : 0;
        public int CropHeight => PixelBounds != null ? PixelBounds.Height : 0;
        public int PixelByteCount => pixels.Length;

        private VisualCropSnapshot(
            string sessionId,
            long sourceFrameId,
            int sourceWidth,
            int sourceHeight,
            int rotation,
            long capturedAtUnixMilliseconds,
            VisualNormalizedBoundingBox boundingBox,
            VisualPixelBounds pixelBounds,
            byte[] ownedPixels)
        {
            SessionId = sessionId == null ? string.Empty : sessionId.Trim();
            SourceFrameId = sourceFrameId;
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            Rotation = rotation;
            PixelFormat = VisualPixelFormat.Rgba32;
            CapturedAtUnixMilliseconds = capturedAtUnixMilliseconds;
            BoundingBox = boundingBox;
            PixelBounds = pixelBounds;
            pixels = ownedPixels ?? Array.Empty<byte>();
        }

        internal static VisualCropSnapshot FromOwnedRgba32(
            string sessionId,
            long sourceFrameId,
            int sourceWidth,
            int sourceHeight,
            int rotation,
            long capturedAtUnixMilliseconds,
            VisualNormalizedBoundingBox boundingBox,
            VisualPixelBounds pixelBounds,
            byte[] ownedPixels)
        {
            return new VisualCropSnapshot(
                sessionId,
                sourceFrameId,
                sourceWidth,
                sourceHeight,
                rotation,
                capturedAtUnixMilliseconds,
                boundingBox,
                pixelBounds,
                ownedPixels);
        }

        public bool IsValid
        {
            get
            {
                if (SessionId.Length == 0 ||
                    SourceFrameId <= 0 ||
                    SourceWidth <= 0 ||
                    SourceHeight <= 0 ||
                    CapturedAtUnixMilliseconds <= 0 ||
                    BoundingBox == null ||
                    !BoundingBox.IsValid ||
                    PixelBounds == null ||
                    !PixelBounds.IsValid ||
                    PixelBounds.RightExclusive > SourceWidth ||
                    PixelBounds.BottomExclusive > SourceHeight)
                {
                    return false;
                }

                try
                {
                    return pixels.Length == checked(CropWidth * CropHeight * RgbaChannels);
                }
                catch (OverflowException)
                {
                    return false;
                }
            }
        }

        public byte[] CopyPixels()
        {
            return (byte[])pixels.Clone();
        }
    }
}
