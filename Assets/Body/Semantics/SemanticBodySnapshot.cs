// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Text;

namespace SalieriAI.Body.Semantics
{
    public enum SemanticBodyAvailability
    {
        Unknown = 0,
        Available = 1,
        Unavailable = 2
    }

    public enum SemanticArmPose
    {
        Unknown = 0,
        Neutral = 1,
        Chest = 2,
        Waist = 3,
        Front = 4,
        Side = 5,
        Up = 6,
        Hold = 7
    }

    public enum SemanticArmActivity
    {
        Unknown = 0,
        Idle = 1,
        ActiveRequest = 2,
        HeldCommand = 3
    }

    public enum SemanticBodyOwnership
    {
        None = 0,
        FreePose = 1,
        Point = 2,
        Manual = 3,
        Debug = 4,
        Other = 5
    }

    public enum SemanticBodyStateBasis
    {
        Unknown = 0,
        Requested = 1,
        Commanded = 2
    }

    public enum SemanticBodyStateSource
    {
        Unknown = 0,
        HandTargetAuthority = 1,
        FreePose = 2,
        OrientationResolver = 3,
        OrientationPriorityRequest = 4,
        AttentionContinuity = 5,
        NeckMotionController = 6
    }

    public enum SemanticHeadAttention
    {
        Unknown = 0,
        None = 1,
        Neutral = 2,
        Face = 3,
        Object = 4,
        Directed = 5,
        Hold = 6
    }

    public enum SemanticBodyAction
    {
        Unknown = 0,
        None = 1,
        FreePose = 2,
        Point = 3,
        Manual = 4,
        Debug = 5,
        Mixed = 6
    }

    /// <summary>
    /// Conversation-facing arm meaning. This is a runtime semantic projection
    /// of a request/command, never evidence that physical motion completed.
    /// </summary>
    public sealed class SemanticArmSnapshot
    {
        public SemanticBodyAvailability Availability { get; }
        public SemanticArmPose Pose { get; }
        public SemanticArmActivity Activity { get; }
        public SemanticBodyOwnership Ownership { get; }
        public SemanticBodyStateBasis StateBasis { get; }
        public SemanticBodyStateSource Source { get; }

        public bool HasActiveRequest =>
            Activity == SemanticArmActivity.ActiveRequest;
        public bool IsHoldingCommandedPose =>
            Activity == SemanticArmActivity.HeldCommand;

        public SemanticArmSnapshot(
            SemanticBodyAvailability availability,
            SemanticArmPose pose,
            SemanticArmActivity activity,
            SemanticBodyOwnership ownership,
            SemanticBodyStateBasis stateBasis,
            SemanticBodyStateSource source)
        {
            Availability = availability;
            Pose = pose;
            Activity = activity;
            Ownership = ownership;
            StateBasis = stateBasis;
            Source = source;
        }

        public static SemanticArmSnapshot Unknown()
        {
            return new SemanticArmSnapshot(
                SemanticBodyAvailability.Unknown,
                SemanticArmPose.Unknown,
                SemanticArmActivity.Unknown,
                SemanticBodyOwnership.None,
                SemanticBodyStateBasis.Unknown,
                SemanticBodyStateSource.Unknown);
        }
    }

    /// <summary>
    /// Current semantic attention meaning. StateBasis=Commanded describes the
    /// software orientation path only and must not be read as PhysicalReached.
    /// </summary>
    public sealed class SemanticHeadAttentionSnapshot
    {
        public SemanticHeadAttention Attention { get; }
        public SemanticBodyStateBasis StateBasis { get; }
        public SemanticBodyStateSource Source { get; }
        public bool CurrentAttentionTargetAvailable { get; }

        public SemanticHeadAttentionSnapshot(
            SemanticHeadAttention attention,
            SemanticBodyStateBasis stateBasis,
            SemanticBodyStateSource source,
            bool currentAttentionTargetAvailable = false)
        {
            Attention = attention;
            StateBasis = stateBasis;
            Source = source;
            CurrentAttentionTargetAvailable =
                currentAttentionTargetAvailable;
        }

        public static SemanticHeadAttentionSnapshot Unknown()
        {
            return new SemanticHeadAttentionSnapshot(
                SemanticHeadAttention.Unknown,
                SemanticBodyStateBasis.Unknown,
                SemanticBodyStateSource.Unknown);
        }
    }

    /// <summary>
    /// Immutable, runtime-local semantic body snapshot for Conversation and
    /// future Thinking consumers. It intentionally contains no Transform,
    /// coordinate, quaternion, joint, PWM, servo ID, or physical-reached flag.
    /// </summary>
    public sealed class SemanticBodySnapshot
    {
        public SemanticArmSnapshot RightArm { get; }
        public SemanticArmSnapshot LeftArm { get; }
        public SemanticHeadAttentionSnapshot HeadAttention { get; }
        public SemanticBodyAction ActiveBodyAction { get; }
        public DateTime SnapshotTimestampUtc { get; }

        public SemanticBodySnapshot(
            SemanticArmSnapshot rightArm,
            SemanticArmSnapshot leftArm,
            SemanticHeadAttentionSnapshot headAttention,
            SemanticBodyAction activeBodyAction,
            DateTime snapshotTimestampUtc)
        {
            RightArm = rightArm ?? SemanticArmSnapshot.Unknown();
            LeftArm = leftArm ?? SemanticArmSnapshot.Unknown();
            HeadAttention = headAttention ??
                SemanticHeadAttentionSnapshot.Unknown();
            ActiveBodyAction = activeBodyAction;
            SnapshotTimestampUtc = snapshotTimestampUtc.Kind ==
                DateTimeKind.Utc
                    ? snapshotTimestampUtc
                    : snapshotTimestampUtc.ToUniversalTime();
        }

        public static SemanticBodySnapshot Unknown(DateTime timestampUtc)
        {
            return new SemanticBodySnapshot(
                SemanticArmSnapshot.Unknown(),
                SemanticArmSnapshot.Unknown(),
                SemanticHeadAttentionSnapshot.Unknown(),
                SemanticBodyAction.Unknown,
                timestampUtc);
        }
    }

    /// <summary>
    /// Shared deterministic prompt projection used by Cloud and Local. Internal
    /// owner IDs and hardware data are deliberately excluded.
    /// </summary>
    public static class SemanticBodySnapshotPromptFormatter
    {
        public static string Build(SemanticBodySnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[CURRENT_BODY_STATE]");
            if (snapshot == null)
            {
                sb.AppendLine("available: false");
                sb.Append("[/CURRENT_BODY_STATE]");
                return sb.ToString();
            }

            AppendArm(sb, "right_arm", snapshot.RightArm);
            AppendArm(sb, "left_arm", snapshot.LeftArm);
            sb.AppendLine("attention: " + Token(snapshot.HeadAttention.Attention));
            sb.AppendLine("attention_basis: " +
                Token(snapshot.HeadAttention.StateBasis));
            sb.AppendLine("attention_source: " +
                Token(snapshot.HeadAttention.Source));
            sb.AppendLine("current_attention_target_available: " +
                (snapshot.HeadAttention.CurrentAttentionTargetAvailable
                    ? "true"
                    : "false"));
            sb.AppendLine("body_action: " + Token(snapshot.ActiveBodyAction));
            sb.Append("[/CURRENT_BODY_STATE]");
            return sb.ToString();
        }

        private static void AppendArm(
            StringBuilder sb,
            string prefix,
            SemanticArmSnapshot arm)
        {
            SemanticArmSnapshot safe = arm ?? SemanticArmSnapshot.Unknown();
            sb.AppendLine(prefix + "_availability: " +
                Token(safe.Availability));
            sb.AppendLine(prefix + "_pose: " + Token(safe.Pose));
            sb.AppendLine(prefix + "_activity: " + Token(safe.Activity));
            sb.AppendLine(prefix + "_owner: " + Token(safe.Ownership));
            sb.AppendLine(prefix + "_basis: " + Token(safe.StateBasis));
            sb.AppendLine(prefix + "_source: " + Token(safe.Source));
        }

        private static string Token<T>(T value)
        {
            string text = value != null ? value.ToString() : "Unknown";
            var sb = new StringBuilder(text.Length + 4);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsUpper(c) && i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
