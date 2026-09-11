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

namespace SalieriAI.Body.SpatialTarget
{
    /// <summary>
    /// Scene 上に配置した固定 SpatialTarget を targetId で管理する。
    ///
    /// Phase 1 では固定 Transform の登録と取得だけを担当する。
    /// IK、サーボ送信、モーション生成は担当しない。
    /// </summary>
    public sealed class SpatialTargetRegistry : MonoBehaviour
    {
        [Header("Registered Fixed Targets")]

        [SerializeField]
        private List<SpatialTargetData> targets = new();

        [Header("Diagnostics")]

        [SerializeField]
        private bool logRegisteredTargets = true;

        private readonly Dictionary<string, SpatialTargetData> targetMap =
            new(StringComparer.OrdinalIgnoreCase);

        public int RegisteredTargetCount => targetMap.Count;

        private void Awake()
        {
            RebuildRegistry();
        }

        [ContextMenu("Rebuild Registry")]
        public void RebuildRegistry()
        {
            targetMap.Clear();

            if (targets == null)
            {
                Debug.LogWarning(
                    "[SpatialTargetRegistry] Target list is null.",
                    this
                );

                return;
            }

            foreach (SpatialTargetData target in targets)
            {
                if (target == null)
                {
                    Debug.LogWarning(
                        "[SpatialTargetRegistry] Null target entry skipped.",
                        this
                    );

                    continue;
                }

                if (!target.IsValid)
                {
                    Debug.LogWarning(
                        "[SpatialTargetRegistry] Invalid target skipped. " +
                        "targetId or Transform is missing.",
                        this
                    );

                    continue;
                }

                string normalizedId = NormalizeTargetId(target.TargetId);

                if (targetMap.ContainsKey(normalizedId))
                {
                    Debug.LogError(
                        $"[SpatialTargetRegistry] Duplicate targetId detected: {normalizedId}",
                        this
                    );

                    continue;
                }

                targetMap.Add(normalizedId, target);

                if (logRegisteredTargets)
                {
                    Debug.Log(
                        $"[SpatialTargetRegistry] Registered: {normalizedId} " +
                        $"position={target.Position}",
                        this
                    );
                }
            }

            Debug.Log(
                $"[SpatialTargetRegistry] Registry ready. count={targetMap.Count}",
                this
            );
        }

        public bool TryGetTarget(
            string targetId,
            out SpatialTargetData target
        )
        {
            target = null;

            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            string normalizedId = NormalizeTargetId(targetId);

            if (!targetMap.TryGetValue(normalizedId, out SpatialTargetData found))
            {
                return false;
            }

            if (!found.IsEnabled || !found.IsSafeTarget || !found.IsValid)
            {
                return false;
            }

            target = found;
            return true;
        }

        public bool TryGetTargetTransform(
            string targetId,
            out Transform targetTransform
        )
        {
            targetTransform = null;

            if (!TryGetTarget(targetId, out SpatialTargetData target))
            {
                return false;
            }

            targetTransform = target.TargetTransform;
            return targetTransform != null;
        }

        public IReadOnlyList<SpatialTargetData> GetRegisteredTargets()
        {
            return targets;
        }

        [ContextMenu("Log Registered Targets")]
        public void LogRegisteredTargets()
        {
            if (targetMap.Count == 0)
            {
                Debug.Log(
                    "[SpatialTargetRegistry] No registered targets.",
                    this
                );

                return;
            }

            foreach (KeyValuePair<string, SpatialTargetData> pair in targetMap)
            {
                SpatialTargetData target = pair.Value;

                Debug.Log(
                    $"[SpatialTargetRegistry] id={pair.Key}, " +
                    $"enabled={target.IsEnabled}, " +
                    $"safe={target.IsSafeTarget}, " +
                    $"position={target.Position}",
                    this
                );
            }
        }

        private static string NormalizeTargetId(string targetId)
        {
            return targetId.Trim().ToLowerInvariant();
        }
    }
}