// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/// <summary>
/// 通常運用時の Body Command 送信用インターフェース。
///
/// ICommandSender の Servo 単体送信に加えて、
/// Crawler などの Raw Command 送信も扱う。
///
/// PC メンテナンス専用の SerialSender_PC は、
/// 引き続き ICommandSender のみを実装すればよい。
/// </summary>
public interface IBodyCommandSender : ICommandSender
{
    void SendRawCommand(string command);
}