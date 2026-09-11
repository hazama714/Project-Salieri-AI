// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Core.Language.Contracts
{
    public static class ReactionRouteIds
    {
        public const string Conversation = "CONVERSATION";
        public const string VrmAction = "VRM_ACTION";
        public const string Clarification = "CLARIFICATION";
        public const string Unavailable = "UNAVAILABLE";
        public const string Rejected = "REJECTED";
        public const string ActivationAck = "ACTIVATION_ACK";
        public const string FixedBodyAction = "FIXED_BODY_ACTION";
        public const string ProgrammedAction = "PROGRAMMED_ACTION";
        public const string Ignored = "IGNORED";
    }

    /// <summary>
    /// Phase 1 record of one routing decision for an admitted input.
    /// It does not execute a Skill and does not publish output.
    /// </summary>
    [Serializable]
    public sealed class ReactionDecision
    {
        public string ReactionId { get; }

        public string InputId { get; }

        public string InteractionId { get; }

        public string SelectedRoute { get; }

        public string SelectedSkillId { get; }

        public string ActionId { get; }

        public string DirectionCandidate { get; }

        public string TargetCandidate { get; }

        public string Reason { get; }

        public string PolicyVersion { get; }

        public DateTime DecidedAtUtc { get; }

        public ReactionDecision(
            string reactionId,
            string inputId,
            string interactionId,
            string selectedRoute,
            string selectedSkillId,
            string reason,
            string policyVersion,
            DateTime decidedAtUtc)
            : this(
                reactionId,
                inputId,
                interactionId,
                selectedRoute,
                selectedSkillId,
                string.Empty,
                string.Empty,
                string.Empty,
                reason,
                policyVersion,
                decidedAtUtc)
        {
        }

        public ReactionDecision(
            string reactionId,
            string inputId,
            string interactionId,
            string selectedRoute,
            string selectedSkillId,
            string actionId,
            string directionCandidate,
            string targetCandidate,
            string reason,
            string policyVersion,
            DateTime decidedAtUtc)
        {
            ReactionId = string.IsNullOrWhiteSpace(reactionId)
                ? CommunicationIdGenerator.Create("reaction")
                : reactionId.Trim();
            InputId = inputId ?? string.Empty;
            InteractionId = interactionId ?? string.Empty;
            SelectedRoute = selectedRoute ?? string.Empty;
            SelectedSkillId = selectedSkillId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            DirectionCandidate = directionCandidate ?? string.Empty;
            TargetCandidate = targetCandidate ?? string.Empty;
            Reason = reason ?? string.Empty;
            PolicyVersion = policyVersion ?? string.Empty;

            DateTime safe = decidedAtUtc == default(DateTime)
                ? DateTime.UtcNow
                : decidedAtUtc;

            DecidedAtUtc = safe.Kind == DateTimeKind.Utc
                ? safe
                : safe.ToUniversalTime();
        }

        public static ReactionDecision Create(
            CommunicationInput input,
            string selectedRoute,
            string selectedSkillId,
            string reason,
            string policyVersion = "phase1-existing-route",
            string actionId = "",
            string directionCandidate = "",
            string targetCandidate = "")
        {
            return new ReactionDecision(
                CommunicationIdGenerator.Create("reaction"),
                input != null ? input.InputId : string.Empty,
                input != null ? input.InteractionId : string.Empty,
                selectedRoute,
                selectedSkillId,
                actionId,
                directionCandidate,
                targetCandidate,
                reason,
                policyVersion,
                DateTime.UtcNow
            );
        }
    }
}
