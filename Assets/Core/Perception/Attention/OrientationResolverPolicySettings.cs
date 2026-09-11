// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;

namespace SalieriAI.Core.Perception.Attention
{
    /// <summary>
    /// 外部から明示的な優先要求がない場合の
    /// デフォルト裁定方法。
    /// </summary>
    public enum OrientationDefaultSelectionPolicy
    {
        /// <summary>
        /// Resolver側では対象を選ばない。
        /// </summary>
        NoPreference = 0,

        PreferFace = 1,

        PreferObject = 2,

        /// <summary>
        /// 安定している候補を優先する。
        /// 両方同条件の場合は更新時刻で決定する。
        /// </summary>
        PreferStable = 3,

        /// <summary>
        /// 最も新しく更新された候補を優先する。
        /// </summary>
        PreferMostRecent = 4
    }

    /// <summary>
    /// OrientationTargetResolverの調整可能な裁定設定。
    ///
    /// 首・眼球・サーボ操作は行わない。
    /// </summary>
    [Serializable]
    public sealed class OrientationResolverPolicySettings
    {
        [Header("Default Selection")]
        [SerializeField]
        private OrientationDefaultSelectionPolicy
            defaultSelectionPolicy =
                OrientationDefaultSelectionPolicy.NoPreference;

        [Header("Candidate Freshness")]
        [SerializeField]
        [Min(0)]
        private int stableCandidateMaxAgeMilliseconds = 2000;

        [SerializeField]
        [Min(0)]
        private int unstableCandidateHoldMilliseconds = 1000;

        [Header("Switch Control")]
        [SerializeField]
        [Min(0)]
        private int switchCooldownMilliseconds = 500;

        [Header("Fallback")]
        [SerializeField]
        private bool fallbackToOtherTargetWhenPreferredMissing = true;

        public OrientationDefaultSelectionPolicy
            DefaultSelectionPolicy =>
                defaultSelectionPolicy;

        /// <summary>
        /// 安定候補を有効とみなす最大経過時間。
        ///
        /// 0の場合は時間制限なし。
        /// </summary>
        public int StableCandidateMaxAgeMilliseconds =>
            stableCandidateMaxAgeMilliseconds;

        /// <summary>
        /// 一時消失などでIsStable=falseになった候補を
        /// 保持できる最大時間。
        ///
        /// 0の場合は不安定候補を使用しない。
        /// </summary>
        public int UnstableCandidateHoldMilliseconds =>
            unstableCandidateHoldMilliseconds;

        /// <summary>
        /// 対象切替後、別対象への再切替を抑制する時間。
        /// </summary>
        public int SwitchCooldownMilliseconds =>
            switchCooldownMilliseconds;

        /// <summary>
        /// PreferFaceまたはPreferObjectの対象が存在しない場合、
        /// 反対側の候補へフォールバックするか。
        /// </summary>
        public bool FallbackToOtherTargetWhenPreferredMissing =>
            fallbackToOtherTargetWhenPreferredMissing;

        /// <summary>
        /// Unityのシリアライズおよびデフォルト生成用。
        /// </summary>
        public OrientationResolverPolicySettings()
        {
        }

        /// <summary>
        /// コードおよび単体試験から明示的な設定値で生成する。
        /// </summary>
        public OrientationResolverPolicySettings(
            OrientationDefaultSelectionPolicy defaultSelectionPolicy,
            int stableCandidateMaxAgeMilliseconds = 2000,
            int unstableCandidateHoldMilliseconds = 1000,
            int switchCooldownMilliseconds = 500,
            bool fallbackToOtherTargetWhenPreferredMissing = true)
        {
            this.defaultSelectionPolicy =
                defaultSelectionPolicy;

            this.stableCandidateMaxAgeMilliseconds =
                Math.Max(
                    0,
                    stableCandidateMaxAgeMilliseconds
                );

            this.unstableCandidateHoldMilliseconds =
                Math.Max(
                    0,
                    unstableCandidateHoldMilliseconds
                );

            this.switchCooldownMilliseconds =
                Math.Max(
                    0,
                    switchCooldownMilliseconds
                );

            this.fallbackToOtherTargetWhenPreferredMissing =
                fallbackToOtherTargetWhenPreferredMissing;
        }

        /// <summary>
        /// 現在の設定値を複製する。
        /// </summary>
        public OrientationResolverPolicySettings Clone()
        {
            return new OrientationResolverPolicySettings(
                defaultSelectionPolicy,
                stableCandidateMaxAgeMilliseconds,
                unstableCandidateHoldMilliseconds,
                switchCooldownMilliseconds,
                fallbackToOtherTargetWhenPreferredMissing
            );
        }
    }
}