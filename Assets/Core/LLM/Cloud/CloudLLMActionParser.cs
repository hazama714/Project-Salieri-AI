// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Text.RegularExpressions;

namespace SalieriAI.CloudLLM
{
    public static class CloudLLMActionParser
    {
        public static int ParseActionDigit(
            string text,
            int fallback = 0
        )
        {
            if (string.IsNullOrWhiteSpace(text))
                return fallback;

            Match match = Regex.Match(text, "[0-3]");

            if (!match.Success)
                return fallback;

            return int.Parse(match.Value);
        }
    }
}