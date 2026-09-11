// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SalieriAI.Core.Memory.WorldModel.Contracts
{
    internal static class WorldMemoryContractUtility
    {
        public static string Text(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        public static string Id(string value)
        {
            return Text(value).ToLowerInvariant();
        }

        public static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime) || value.Kind == DateTimeKind.Utc)
                return value;

            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        public static DateTime? Utc(DateTime? value)
        {
            return value.HasValue ? Utc(value.Value) : (DateTime?)null;
        }

        public static IReadOnlyList<string> IdCopy(IEnumerable<string> source)
        {
            var values = new List<string>();
            if (source != null)
            {
                foreach (string value in source)
                    values.Add(Id(value));
            }

            return new ReadOnlyCollection<string>(values);
        }

        public static bool IsCanonicalId(string value)
        {
            string text = Text(value);
            return text.Length == 32 &&
                string.Equals(text, text.ToLowerInvariant(),
                    StringComparison.Ordinal) &&
                Guid.TryParseExact(text, "N", out _);
        }

        public static bool IsUtc(DateTime value)
        {
            return value != default(DateTime) &&
                value.Kind == DateTimeKind.Utc;
        }

        public static bool IsUtc(DateTime? value)
        {
            return !value.HasValue || IsUtc(value.Value);
        }

        public static bool ValidOptionalScore(bool hasValue, float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                (!hasValue || (value >= 0f && value <= 1f));
        }
    }
}
