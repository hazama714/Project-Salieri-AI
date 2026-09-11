// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FBX用の表情出力アダプター。
/// 統一表情名とウェイトを、モデル固有のBlendShape名へ変換する。
///
/// Resources/ExpressionMappings/expression_mapping_{modelName}.json を読み込む。
/// JsonUtilityはDictionaryを直接復元できないため、JSONはentries配列形式を使用する。
/// </summary>
public sealed class FBXExpressionController : MonoBehaviour, IExpressionController
{
    [Header("Target Mesh")]
    [SerializeField] private SkinnedMeshRenderer targetRenderer;

    [Header("Model Name")]
    [Tooltip("例: necomaid → Resources/ExpressionMappings/expression_mapping_necomaid.json")]
    [SerializeField] private string modelName = "necomaid";

    [Header("Debug")]
    [SerializeField] private bool verboseLog = false;

    private readonly Dictionary<string, string> expressionMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> blendShapeIndexCache =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private void Start()
    {
        BuildDefaultMapping();
        RebuildBlendShapeIndexCache();
        LoadExpressionMapping(modelName);
    }

    public void SetExpression(string expressionName)
    {
        ResetExpression();
        SetExpressionWeight(expressionName, 1.0f);
    }

    public void SetExpressionWeight(string expressionName, float weight)
    {
        if (!TryGetMesh(out Mesh mesh))
            return;

        if (string.IsNullOrEmpty(expressionName))
            expressionName = "Neutral";

        weight = Mathf.Clamp01(weight);

        if (!TryResolveBlendShapeName(expressionName, out string blendShapeName))
        {
            Debug.LogWarning(
                "[FBXExpressionController] Mapping is not defined. expression=" + expressionName
            );
            return;
        }

        if (!TryGetBlendShapeIndex(mesh, blendShapeName, out int blendShapeIndex))
        {
            Debug.LogWarning(
                "[FBXExpressionController] BlendShape was not found. " +
                "expression=" + expressionName +
                " blendShape=" + blendShapeName
            );
            return;
        }

        targetRenderer.SetBlendShapeWeight(blendShapeIndex, weight * 100f);

        if (verboseLog)
        {
            Debug.Log(
                "[FBXExpressionController] SetExpressionWeight " +
                "expression=" + expressionName +
                " blendShape=" + blendShapeName +
                " weight=" + weight
            );
        }
    }

    public void ResetExpression()
    {
        if (!TryGetMesh(out Mesh mesh))
            return;

        for (int i = 0; i < mesh.blendShapeCount; i++)
            targetRenderer.SetBlendShapeWeight(i, 0f);

        if (verboseLog)
            Debug.Log("[FBXExpressionController] ResetExpression");
    }

    private void BuildDefaultMapping()
    {
        expressionMap.Clear();

        // モデル側のBlendShape名が同名ならJSONなしでも動く。
        // JSONがある場合は、この対応をモデル固有名で上書きする。
        expressionMap["joy"] = "Joy";
        expressionMap["happy"] = "Joy";
        expressionMap["fun"] = "Fun";
        expressionMap["sorrow"] = "Sorrow";
        expressionMap["sad"] = "Sorrow";
        expressionMap["angry"] = "Angry";
        expressionMap["neutral"] = "Neutral";
    }

    private void LoadExpressionMapping(string name)
    {
        if (string.IsNullOrEmpty(name))
            return;

        string resourcePath =
            "ExpressionMappings/expression_mapping_" + name.ToLowerInvariant();

        TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);

        if (jsonFile == null)
        {
            Debug.LogWarning(
                "[FBXExpressionController] Mapping JSON was not found. " +
                "Use default mapping. path=" + resourcePath
            );
            return;
        }

        try
        {
            ExpressionMappingFile loaded =
                JsonUtility.FromJson<ExpressionMappingFile>(jsonFile.text);

            if (loaded == null || loaded.entries == null)
            {
                Debug.LogWarning(
                    "[FBXExpressionController] Mapping JSON has no entries. " +
                    "Use default mapping. path=" + resourcePath
                );
                return;
            }

            int appliedCount = 0;

            for (int i = 0; i < loaded.entries.Length; i++)
            {
                ExpressionMappingEntry entry = loaded.entries[i];

                if (entry == null ||
                    string.IsNullOrEmpty(entry.expression) ||
                    string.IsNullOrEmpty(entry.blendShape))
                {
                    continue;
                }

                expressionMap[entry.expression.Trim()] = entry.blendShape.Trim();
                appliedCount++;
            }

            Debug.Log(
                "[FBXExpressionController] Mapping JSON loaded. " +
                "path=" + resourcePath +
                " entries=" + appliedCount
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[FBXExpressionController] Mapping JSON load failed. " +
                "path=" + resourcePath +
                " error=" + exception.Message
            );
        }
    }

    private void RebuildBlendShapeIndexCache()
    {
        blendShapeIndexCache.Clear();

        if (!TryGetMesh(out Mesh mesh))
            return;

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string blendShapeName = mesh.GetBlendShapeName(i);

            if (!string.IsNullOrEmpty(blendShapeName))
                blendShapeIndexCache[blendShapeName] = i;
        }
    }

    private bool TryResolveBlendShapeName(
        string expressionName,
        out string blendShapeName
    )
    {
        return expressionMap.TryGetValue(expressionName.Trim(), out blendShapeName);
    }

    private bool TryGetBlendShapeIndex(
        Mesh mesh,
        string blendShapeName,
        out int blendShapeIndex
    )
    {
        if (blendShapeIndexCache.TryGetValue(blendShapeName, out blendShapeIndex))
            return true;

        // Start以前に呼ばれた場合や、実行中にMeshが差し替わった場合の保険。
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (string.Equals(mesh.GetBlendShapeName(i), blendShapeName, StringComparison.Ordinal))
            {
                blendShapeIndex = i;
                blendShapeIndexCache[blendShapeName] = i;
                return true;
            }
        }

        blendShapeIndex = -1;
        return false;
    }

    private bool TryGetMesh(out Mesh mesh)
    {
        mesh = null;

        if (targetRenderer == null)
        {
            Debug.LogWarning("[FBXExpressionController] targetRenderer is not assigned.");
            return false;
        }

        mesh = targetRenderer.sharedMesh;

        if (mesh == null)
        {
            Debug.LogWarning("[FBXExpressionController] sharedMesh is null.");
            return false;
        }

        return true;
    }

    [Serializable]
    private sealed class ExpressionMappingFile
    {
        public ExpressionMappingEntry[] entries;
    }

    [Serializable]
    private sealed class ExpressionMappingEntry
    {
        public string expression;
        public string blendShape;
    }
}
