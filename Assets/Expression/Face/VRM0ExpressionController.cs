// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using VRM;

/// <summary>
/// VRM0用の表情出力アダプター。
/// 統一表情名とウェイトを、VRM0の BlendShapePreset へ変換する。
/// </summary>
[Preserve]
public sealed class VRM0ExpressionController : MonoBehaviour, IExpressionController
{
    [SerializeField] public VRMBlendShapeProxy blendShapeProxy;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = false;

    public string modelName = "default";

    private readonly Dictionary<BlendShapeKey, float> currentValues =
        new Dictionary<BlendShapeKey, float>();

    [Preserve]
    public void SetExpression(string expressionName)
    {
        ResetExpression();
        SetExpressionWeight(expressionName, 1.0f);
    }

    public void SetExpressionWeight(string expressionName, float weight)
    {
        if (blendShapeProxy == null)
        {
            Debug.LogWarning("[VRM0ExpressionController] blendShapeProxy is not assigned.");
            return;
        }

        if (string.IsNullOrEmpty(expressionName))
            expressionName = "Neutral";

        weight = Mathf.Clamp01(weight);

        BlendShapePreset preset = MapExpressionToPreset(expressionName);
        BlendShapeKey key = BlendShapeKey.CreateFromPreset(preset);

        currentValues[key] = weight;

        if (verboseLog)
        {
            Debug.Log(
                "[VRM0ExpressionController] SetExpressionWeight " +
                "expression=" + expressionName +
                " preset=" + preset +
                " weight=" + weight
            );
        }
    }

    public void ResetExpression()
    {
        currentValues.Clear();

        if (verboseLog)
            Debug.Log("[VRM0ExpressionController] ResetExpression");
    }

    private void LateUpdate()
    {
        if (blendShapeProxy == null)
            return;

        blendShapeProxy.SetValues(currentValues);
    }

    private static BlendShapePreset MapExpressionToPreset(string expressionName)
    {
        string lower = expressionName.ToLowerInvariant();

        if (lower.Contains("joy") || lower.Contains("happy"))
            return BlendShapePreset.Joy;

        if (lower.Contains("angry"))
            return BlendShapePreset.Angry;

        if (lower.Contains("sorrow") || lower.Contains("sad"))
            return BlendShapePreset.Sorrow;

        if (lower.Contains("fun"))
            return BlendShapePreset.Fun;

        return BlendShapePreset.Neutral;
    }
}
