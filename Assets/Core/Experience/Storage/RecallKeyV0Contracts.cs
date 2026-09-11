// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Globalization;

using SalieriAI.Core.Perception.VisualSnapshots;

namespace SalieriAI.Core.Experience.Recall
{
    /// <summary>
    /// M4 Recall v0 key. This is visual evidence, not persistent object identity.
    /// </summary>
    public sealed class RecallKeyV0
    {
        public const int SchemaVersion = 0;
        public const string Prefix = "RK0";

        public int DetectorClassId { get; }
        public string VisualSignature { get; }
        public int AspectRatioBucket { get; }
        public string Serialized { get; }

        internal RecallKeyV0(
            int detectorClassId,
            string visualSignature,
            int aspectRatioBucket)
        {
            DetectorClassId = detectorClassId;
            VisualSignature = visualSignature ?? string.Empty;
            AspectRatioBucket = aspectRatioBucket;
            Serialized = Prefix + ":C" +
                detectorClassId.ToString(CultureInfo.InvariantCulture) +
                ":V" + VisualSignature +
                ":A" + aspectRatioBucket.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryParse(
            string value,
            out RecallKeyV0 key)
        {
            key = null;
            string text = value == null ? string.Empty : value.Trim();
            string[] parts = text.Split(':');
            if (parts.Length != 4 ||
                !string.Equals(parts[0], Prefix, StringComparison.Ordinal) ||
                parts[1].Length < 2 || parts[1][0] != 'C' ||
                parts[2].Length != 17 || parts[2][0] != 'V' ||
                parts[3].Length != 2 || parts[3][0] != 'A')
            {
                return false;
            }

            if (!int.TryParse(
                    parts[1].Substring(1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int classId) ||
                classId < 0)
            {
                return false;
            }

            string signature = parts[2].Substring(1);
            if (!ulong.TryParse(
                    signature,
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out _))
            {
                return false;
            }

            if (!int.TryParse(
                    parts[3].Substring(1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int bucket) ||
                bucket < 0 || bucket > 5)
            {
                return false;
            }

            key = new RecallKeyV0(
                classId,
                signature.ToUpperInvariant(),
                bucket);
            return string.Equals(key.Serialized, text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Deterministic managed RGBA32 dHash builder.
    /// Resize: 9x8 nearest sample at each destination pixel center.
    /// Luma: (77*R + 150*G + 29*B) / 256.
    /// Bits: row-major; the first comparison becomes bit 63.
    /// </summary>
    public static class RecallKeyV0Builder
    {
        private const int SampleWidth = 9;
        private const int SampleHeight = 8;

        public static bool TryBuild(
            int detectorClassId,
            VisualCropSnapshot crop,
            out RecallKeyV0 key,
            out string error)
        {
            key = null;
            error = string.Empty;
            if (detectorClassId < 0)
            {
                error = "DetectorClassId is unavailable.";
                return false;
            }
            if (crop == null || !crop.IsValid ||
                crop.PixelFormat != VisualPixelFormat.Rgba32 ||
                crop.CropWidth <= 0 || crop.CropHeight <= 0)
            {
                error = "VisualCropSnapshot is invalid or unsupported.";
                return false;
            }

            if (!TryBuildVisualSignature(crop, out string signature, out error))
                return false;

            int aspectBucket = ResolveAspectRatioBucket(
                crop.CropWidth,
                crop.CropHeight);
            if (aspectBucket < 0)
            {
                error = "Crop aspect ratio is invalid.";
                return false;
            }

            key = new RecallKeyV0(detectorClassId, signature, aspectBucket);
            return true;
        }

        public static bool TryBuildVisualSignature(
            VisualCropSnapshot crop,
            out string signature,
            out string error)
        {
            signature = string.Empty;
            error = string.Empty;
            if (crop == null || !crop.IsValid ||
                crop.PixelFormat != VisualPixelFormat.Rgba32)
            {
                error = "VisualCropSnapshot is invalid or unsupported.";
                return false;
            }

            byte[] rgba = crop.CopyPixels();
            int expected;
            try
            {
                expected = checked(crop.CropWidth * crop.CropHeight * 4);
            }
            catch (OverflowException)
            {
                error = "Visual crop dimensions overflow.";
                return false;
            }
            if (rgba.Length != expected)
            {
                error = "Visual crop RGBA32 length is inconsistent.";
                return false;
            }

            int[] luma = new int[SampleWidth * SampleHeight];
            for (int y = 0; y < SampleHeight; y++)
            {
                int sourceY = SampleAtCenter(y, crop.CropHeight, SampleHeight);
                for (int x = 0; x < SampleWidth; x++)
                {
                    int sourceX = SampleAtCenter(x, crop.CropWidth, SampleWidth);
                    int offset = (sourceY * crop.CropWidth + sourceX) * 4;
                    luma[y * SampleWidth + x] =
                        (77 * rgba[offset] +
                         150 * rgba[offset + 1] +
                         29 * rgba[offset + 2]) >> 8;
                }
            }

            ulong bits = 0UL;
            for (int y = 0; y < SampleHeight; y++)
            for (int x = 0; x < SampleWidth - 1; x++)
            {
                bits <<= 1;
                if (luma[y * SampleWidth + x] >
                    luma[y * SampleWidth + x + 1])
                {
                    bits |= 1UL;
                }
            }

            signature = bits.ToString("X16", CultureInfo.InvariantCulture);
            return true;
        }

        public static int ResolveAspectRatioBucket(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return -1;

            long w = width;
            long h = height;
            if (w * 5 < h * 3) return 0; // ratio < 0.60
            if (w * 5 < h * 4) return 1; // ratio < 0.80
            if (w < h) return 2;         // ratio < 1.00
            if (w * 4 < h * 5) return 3; // ratio < 1.25
            if (w * 5 < h * 8) return 4; // ratio < 1.60
            return 5;
        }

        private static int SampleAtCenter(
            int destinationIndex,
            int sourceSize,
            int destinationSize)
        {
            long numerator = (2L * destinationIndex + 1L) * sourceSize;
            int sample = (int)(numerator / (2L * destinationSize));
            return Math.Max(0, Math.Min(sourceSize - 1, sample));
        }
    }
}
