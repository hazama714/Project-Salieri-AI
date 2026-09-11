// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Input
{
    /// <summary>
    /// Phase 10-D:
    /// STT / TextInput / DebugInput の入口差を吸収するための最小正規化。
    ///
    /// ここではコマンド変換や意味解釈は行わない。
    /// あくまで Runtime 本流へ安全に流すための軽い整形だけを担当する。
    /// </summary>
    public static class InputNormalizer
    {
        public static string NormalizeUserInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string normalized = text.Trim();

            // 全角スペースを半角スペースへ寄せる。
            normalized = normalized.Replace('　', ' ');

            // 連続空白を軽く圧縮。
            while (normalized.Contains("  "))
            {
                normalized = normalized.Replace("  ", " ");
            }

            return normalized;
        }
    }
}