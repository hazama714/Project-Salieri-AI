// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Core.Opening
{
    /// <summary>
    /// StartupLockPanelController など、旧構成が OpeningCeremonyManager を参照している場合の互換クラス。
    /// 実処理は OpeningCeremony に集約する。
    /// </summary>
    public sealed class OpeningCeremonyManager : OpeningCeremony
    {
    }
}
