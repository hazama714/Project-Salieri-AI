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

namespace SalieriAI.Core.Language.Compiler
{
    internal static class SccContractUtility
    {
        public static string Text(string value)
        {
            return value ?? string.Empty;
        }

        public static DateTime Utc(DateTime value)
        {
            if (value == default(DateTime))
                return value;

            if (value.Kind == DateTimeKind.Utc)
                return value;

            if (value.Kind == DateTimeKind.Local)
                return value.ToUniversalTime();

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        public static IReadOnlyList<T> ReadOnlyCopy<T>(
            IEnumerable<T> source)
        {
            if (source == null)
                return new ReadOnlyCollection<T>(new List<T>());

            return new ReadOnlyCollection<T>(new List<T>(source));
        }
    }
}
