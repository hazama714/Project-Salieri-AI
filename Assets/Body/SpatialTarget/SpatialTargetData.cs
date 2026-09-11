// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;

namespace SalieriAI.Body.SpatialTarget
{
    public enum SpatialTargetHandScope
    {
        Both,
        RightOnly,
        LeftOnly
    }

    /// <summary>
    /// 空間上に配置された固定 Target の定義。
    ///
    /// AI や上位 Runtime はサーボ角度を直接指定せず、
    /// targetId を指定して空間上の目標位置を選択する。
    /// </summary>
    [Serializable]
    public sealed class SpatialTargetData
    {
        [Header("Identity")]

        [Tooltip("Runtime から参照する一意な Target ID")]
        [SerializeField]
        private string targetId = string.Empty;

        [Tooltip("Inspector 上で確認しやすくするための説明")]
        [SerializeField]
        private string description = string.Empty;

        [Header("Scene Reference")]

        [Tooltip("Scene 上に配置した固定 Target Transform")]
        [SerializeField]
        private Transform targetTransform;

        [Header("Hand Scope")]

        [Tooltip("この Target を使用できる手。共通 Target は Both")]
        [SerializeField]
        private SpatialTargetHandScope handScope = SpatialTargetHandScope.Both;
        
        [Header("Availability")]

        [Tooltip("現在この Target を選択候補として使用できるか")]
        [SerializeField]
        private bool isEnabled = true;

        [Header("Safety")]

        [Tooltip("安全な固定 Target として使用可能か")]
        [SerializeField]
        private bool isSafeTarget = true;

        public string TargetId => targetId;

        public string Description => description;

        public Transform TargetTransform => targetTransform;

        public bool IsEnabled => isEnabled;

        public bool IsSafeTarget => isSafeTarget;

        public SpatialTargetHandScope HandScope => handScope;

        public bool CanApplyToRightHand()
        {
            return handScope == SpatialTargetHandScope.Both ||
                   handScope == SpatialTargetHandScope.RightOnly;
        }

        public bool CanApplyToLeftHand()
        {
            return handScope == SpatialTargetHandScope.Both ||
                   handScope == SpatialTargetHandScope.LeftOnly;
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(targetId) &&
            targetTransform != null;

        public Vector3 Position =>
            targetTransform != null
                ? targetTransform.position
                : Vector3.zero;

        public Quaternion Rotation =>
            targetTransform != null
                ? targetTransform.rotation
                : Quaternion.identity;
    }
}