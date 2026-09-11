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
    /// 注視対象について外部から要求する方針。
    /// 実際にどの対象を採用するかはResolverが決定する。
    /// </summary>
    public enum OrientationPriorityDirective
    {
        Default = 0,
        PreferFace = 1,
        PreferObject = 2,
        HoldPrevious = 3,
        Neutral = 4,

        /// <summary>
        /// RobotBodyFrame基準のYaw/Pitchで表した空間注視要求。
        /// Physical neck angle / servo commandではない。
        /// </summary>
        TargetDirection = 5
    }

    /// <summary>
    /// 注視方針を要求した理由。
    /// Resolverのデバッグや優先順位調整に使用する。
    /// </summary>
    public enum OrientationPriorityReason
    {
        Default = 0,
        Conversation = 1,
        ObserveObject = 2,
        ManipulateObject = 3,
        SocialInterrupt = 4,
        Emergency = 5
    }

    /// <summary>
    /// Action / BehaviorからOrientation Resolverへ渡す空間Target。
    /// 値はRobotBodyFrame基準の視線方向であり、実機角ではない。
    /// </summary>
    [Serializable]
    public sealed class OrientationTargetDirectionRequest
    {
        public string TargetKey { get; }
        public float BodyRelativeYawDegrees { get; }
        public float BodyRelativePitchDegrees { get; }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(TargetKey) &&
            IsFinite(BodyRelativeYawDegrees) &&
            IsFinite(BodyRelativePitchDegrees);

        public OrientationTargetDirectionRequest(
            string targetKey,
            float bodyRelativeYawDegrees,
            float bodyRelativePitchDegrees)
        {
            TargetKey = targetKey;
            BodyRelativeYawDegrees = bodyRelativeYawDegrees;
            BodyRelativePitchDegrees = bodyRelativePitchDegrees;
        }

        public OrientationTargetDirectionRequest Clone()
        {
            return new OrientationTargetDirectionRequest(
                TargetKey,
                BodyRelativeYawDegrees,
                BodyRelativePitchDegrees);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Mode、Skill、会話制御などからResolverへ渡す注視方針要求。
    ///
    /// この型は要求を表すだけで、首・眼球・サーボを操作しない。
    /// </summary>
    [Serializable]
    public sealed class OrientationPriorityRequest
    {
        public string RequestId
        {
            get;
        }

        /// <summary>
        /// 要求元を識別するキー。
        /// 例：conversation、observe_object_mode。
        /// </summary>
        public string SourceKey
        {
            get;
        }

        public OrientationPriorityDirective Directive
        {
            get;
        }

        public OrientationPriorityReason Reason
        {
            get;
        }

        /// <summary>
        /// 0～100。数値が大きい要求を優先する。
        /// 実際の裁定規則は後段Resolverで決める。
        /// </summary>
        public int Priority
        {
            get;
        }

        public long CreatedAtUnixMilliseconds
        {
            get;
        }

        /// <summary>
        /// 0以下の場合は期限なし。
        /// </summary>
        public long ExpiresAtUnixMilliseconds
        {
            get;
        }

        /// <summary>
        /// TargetDirection directiveでのみ使用する空間Target。
        /// Servo ID、Servo Center、Physical calibrationは保持しない。
        /// </summary>
        public OrientationTargetDirectionRequest TargetDirection
        {
            get;
        }

        /// <summary>
        /// HoldPreviousを特定Targetへ相関させる任意キー。
        /// 空の場合は従来どおり、Resolverの直前Targetを保持する。
        /// </summary>
        public string HeldTargetKey
        {
            get;
        }

        public bool IsPersistent =>
            ExpiresAtUnixMilliseconds <= 0;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(RequestId) &&
            !string.IsNullOrWhiteSpace(SourceKey) &&
            CreatedAtUnixMilliseconds > 0 &&
            (Directive != OrientationPriorityDirective.TargetDirection ||
             (TargetDirection != null && TargetDirection.IsValid)) &&
            (string.IsNullOrEmpty(HeldTargetKey) ||
             Directive == OrientationPriorityDirective.HoldPrevious);

        public OrientationPriorityRequest(
            string requestId,
            string sourceKey,
            OrientationPriorityDirective directive,
            OrientationPriorityReason reason,
            int priority,
            long createdAtUnixMilliseconds,
            long expiresAtUnixMilliseconds,
            OrientationTargetDirectionRequest targetDirection = null,
            string heldTargetKey = null)
        {
            RequestId = requestId;
            SourceKey = sourceKey;
            Directive = directive;
            Reason = reason;
            Priority = ClampPriority(priority);
            CreatedAtUnixMilliseconds =
                createdAtUnixMilliseconds;
            ExpiresAtUnixMilliseconds =
                expiresAtUnixMilliseconds;
            TargetDirection =
                targetDirection != null
                    ? targetDirection.Clone()
                    : null;
            HeldTargetKey =
                heldTargetKey == null
                    ? string.Empty
                    : heldTargetKey.Trim();
        }

        public bool IsExpired(
            long currentUnixMilliseconds)
        {
            return !IsPersistent &&
                   currentUnixMilliseconds >=
                   ExpiresAtUnixMilliseconds;
        }

        public OrientationPriorityRequest Clone()
        {
            return new OrientationPriorityRequest(
                RequestId,
                SourceKey,
                Directive,
                Reason,
                Priority,
                CreatedAtUnixMilliseconds,
                ExpiresAtUnixMilliseconds,
                TargetDirection,
                HeldTargetKey
            );
        }

        private static int ClampPriority(int value)
        {
            if (value < 0)
                return 0;

            if (value > 100)
                return 100;

            return value;
        }
    }
}
