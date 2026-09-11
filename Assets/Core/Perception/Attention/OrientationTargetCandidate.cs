// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Perception.Attention
{
    /// <summary>
    /// 注視候補の発生元。
    /// NeutralやHoldPreviousは知覚候補ではなく、
    /// 後段Resolverの出力なので、ここには含めない。
    /// </summary>
    public enum OrientationTargetCandidateKind
    {
        Unknown = 0,
        Face = 1,
        Object = 2
    }

    /// <summary>
    /// 顔・物体の知覚結果を、
    /// OrientationTargetResolverへ渡すための共通形式。
    ///
    /// この型は候補情報だけを保持し、
    /// 優先順位の決定や首・眼球制御は行わない。
    /// </summary>
    [Serializable]
    public sealed class OrientationTargetCandidate
    {
        public OrientationTargetCandidateKind Kind
        {
            get;
        }

        public string TargetKey
        {
            get;
        }

        /// <summary>
        /// Unity形式の正規化画面座標。
        /// 左下=(0,0)、右上=(1,1)。
        /// </summary>
        public float CenterX
        {
            get;
        }

        public float CenterY
        {
            get;
        }

        /// <summary>
        /// 信頼度を取得できる知覚方式か。
        /// Haar顔検出にはYOLOのような信頼度がないため、
        /// 顔候補ではfalseにできる。
        /// </summary>
        public bool HasConfidence
        {
            get;
        }

        public float Confidence
        {
            get;
        }

        public bool IsStable
        {
            get;
        }

        public long UpdatedAtUnixMilliseconds
        {
            get;
        }

        /// <summary>
        /// Source perception frame for one bounded Object correction.
        /// This is runtime observation provenance, not persistent identity.
        /// Face candidates and legacy callers may leave it unavailable (0).
        /// </summary>
        public long SourceFrameId
        {
            get;
        }

        /// <summary>
        /// デバッグ表示用。
        /// 例：face、bottle、cell phone。
        /// Resolverの優先判定には原則使用しない。
        /// </summary>
        public string Label
        {
            get;
        }

        public bool IsValid =>
            Kind != OrientationTargetCandidateKind.Unknown &&
            !string.IsNullOrWhiteSpace(TargetKey) &&
            CenterX >= 0f &&
            CenterX <= 1f &&
            CenterY >= 0f &&
            CenterY <= 1f &&
            UpdatedAtUnixMilliseconds > 0;

        public OrientationTargetCandidate(
            OrientationTargetCandidateKind kind,
            string targetKey,
            float centerX,
            float centerY,
            bool hasConfidence,
            float confidence,
            bool isStable,
            long updatedAtUnixMilliseconds,
            string label,
            long sourceFrameId = 0)
        {
            Kind = kind;
            TargetKey = targetKey;
            CenterX = centerX;
            CenterY = centerY;
            HasConfidence = hasConfidence;
            Confidence = confidence;
            IsStable = isStable;
            UpdatedAtUnixMilliseconds =
                updatedAtUnixMilliseconds;
            Label = label;
            SourceFrameId = sourceFrameId;
        }

        public OrientationTargetCandidate Clone()
        {
            return new OrientationTargetCandidate(
                Kind,
                TargetKey,
                CenterX,
                CenterY,
                HasConfidence,
                Confidence,
                IsStable,
                UpdatedAtUnixMilliseconds,
                Label,
                SourceFrameId
            );
        }
    }
}
