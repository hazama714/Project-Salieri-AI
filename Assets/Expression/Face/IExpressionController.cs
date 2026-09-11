// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/// <summary>
/// VRM0 / FBX などのモデル形式に依存しない表情出力インターフェース。
///
/// Runtime / Hub 側はモデル固有APIを知らず、統一された表情名と 0.0～1.0 の重みだけを渡す。
/// モデル固有の BlendShape / Morph 変換は各 Controller 側で吸収する。
/// </summary>
public interface IExpressionController
{
    /// <summary>
    /// 指定された表情を最大強度で適用する。
    /// デバッグ操作や旧経路との互換用。
    /// </summary>
    void SetExpression(string expressionName);

    /// <summary>
    /// 指定された表情を 0.0～1.0 の重みで適用する。
    /// フェード制御は ExpressionReactionHub 側が担当する。
    /// </summary>
    void SetExpressionWeight(string expressionName, float weight);

    /// <summary>
    /// この Controller が管理する全表情ウェイトを 0 に戻す。
    /// </summary>
    void ResetExpression();
}
