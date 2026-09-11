// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections.Generic;

namespace SalieriAI.Body.FreePose
{
    /// <summary>
    /// Semantic output channel for a body target. Only hand channels have
    /// definitions in v0; the remaining values reserve the catalog boundary
    /// for future head, face, and orientation targets without implementing
    /// those target systems here.
    /// </summary>
    public enum SemanticBodyTargetKind
    {
        RightHand = 0,
        LeftHand = 1,
        Head = 2,
        Face = 3,
        Orientation = 4
    }

    /// <summary>
    /// Distinguishes fixed body-relative targets from semantic targets that
    /// must be resolved against current perception when execution starts.
    /// </summary>
    public enum SemanticBodyTargetSpace
    {
        StaticBodyRelative = 0,
        DynamicPerception = 1
    }

    public static class SemanticBodyDynamicTargetIds
    {
        public const string CurrentAttentionTarget =
            "current_attention_target";
    }

    /// <summary>
    /// Exact executable target ID plus its LLM-facing physical meaning.
    /// It contains no Transform, coordinate, joint, servo, or execution state.
    /// </summary>
    public readonly struct SemanticBodyTargetDefinition
    {
        public string Id { get; }
        public SemanticBodyTargetKind Kind { get; }
        public SemanticBodyTargetSpace Space { get; }
        public string SemanticDescription { get; }

        internal SemanticBodyTargetDefinition(
            string id,
            SemanticBodyTargetKind kind,
            string semanticDescription,
            SemanticBodyTargetSpace space =
                SemanticBodyTargetSpace.StaticBodyRelative)
        {
            Id = id;
            Kind = kind;
            Space = space;
            SemanticDescription = semanticDescription;
        }
    }

    /// <summary>
    /// Shared v0 source of truth for Cloud-visible semantic body targets and
    /// exact runtime validation. Adding an executable hand target requires a
    /// definition here in addition to its SpatialTarget ID and Scene entry.
    /// </summary>
    public static class SemanticBodyTargetCatalogV0
    {
        public static IReadOnlyList<SemanticBodyTargetDefinition>
            GetDefinitions(SemanticBodyTargetKind kind)
        {
            if (kind == SemanticBodyTargetKind.RightHand)
                return RightHandDefinitions;

            if (kind == SemanticBodyTargetKind.LeftHand)
                return LeftHandDefinitions;

            return EmptyDefinitions;
        }

        public static bool TryGetExact(
            string id,
            SemanticBodyTargetKind requiredKind,
            out SemanticBodyTargetDefinition definition)
        {
            definition = default;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            IReadOnlyList<SemanticBodyTargetDefinition> definitions =
                GetDefinitions(requiredKind);
            for (int i = 0; i < definitions.Count; i++)
            {
                if (string.Equals(
                        id,
                        definitions[i].Id,
                        StringComparison.Ordinal))
                {
                    definition = definitions[i];
                    return true;
                }
            }

            return false;
        }

        private static SemanticBodyTargetDefinition Hand(
            string id,
            SemanticBodyTargetKind kind,
            string description)
        {
            return new SemanticBodyTargetDefinition(id, kind, description);
        }

        private static SemanticBodyTargetDefinition DynamicHand(
            SemanticBodyTargetKind kind)
        {
            return new SemanticBodyTargetDefinition(
                SemanticBodyDynamicTargetIds.CurrentAttentionTarget,
                kind,
                "Salieriが現在実際に見ている、またはAttention対象として" +
                "選択している人・物・対象。POINTによる非接触の指示にのみ" +
                "使用し、有効なcurrent attention targetがない場合は使用しない。",
                SemanticBodyTargetSpace.DynamicPerception);
        }

        private static readonly IReadOnlyList<SemanticBodyTargetDefinition>
            EmptyDefinitions = Array.AsReadOnly(
                Array.Empty<SemanticBodyTargetDefinition>());

        private static readonly IReadOnlyList<SemanticBodyTargetDefinition>
            RightHandDefinitions = Array.AsReadOnly(new[]
            {
                Hand(
                    FreePoseSpatialTargetIds.RightNeutral,
                    SemanticBodyTargetKind.RightHand,
                    "右腕を身体の横へ自然に下ろした、力を抜いた休止位置" +
                    "（relaxed/resting position beside the body）。"),
                Hand(
                    FreePoseSpatialTargetIds.RightChest,
                    SemanticBodyTargetKind.RightHand,
                    "右手を胸付近へ意図的に置く位置。"),
                Hand(
                    FreePoseSpatialTargetIds.RightWaist,
                    SemanticBodyTargetKind.RightHand,
                    "右手を腰・hip付近へ意図的に置く位置。" +
                    "休止位置や『手を下ろす』意味ではない。"),
                Hand(
                    FreePoseSpatialTargetIds.RightFront,
                    SemanticBodyTargetKind.RightHand,
                    "右手を身体の前方へ出した位置。"),
                Hand(
                    FreePoseSpatialTargetIds.RightSide,
                    SemanticBodyTargetKind.RightHand,
                    "右腕・右手を身体の横方向へ広げた位置。"),
                Hand(
                    FreePoseSpatialTargetIds.RightUp,
                    SemanticBodyTargetKind.RightHand,
                    "右腕・右手を上方へ上げた位置。"),
                DynamicHand(SemanticBodyTargetKind.RightHand)
            });

        private static readonly IReadOnlyList<SemanticBodyTargetDefinition>
            LeftHandDefinitions = Array.AsReadOnly(new[]
            {
                Hand(
                    FreePoseSpatialTargetIds.LeftNeutral,
                    SemanticBodyTargetKind.LeftHand,
                    "左腕を身体の横へ自然に下ろした、力を抜いた休止位置" +
                    "（relaxed/resting position beside the body）。"),
                Hand(
                    FreePoseSpatialTargetIds.LeftChest,
                    SemanticBodyTargetKind.LeftHand,
                    "左手を胸付近へ意図的に置く位置。"),
                Hand(
                    FreePoseSpatialTargetIds.LeftWaist,
                    SemanticBodyTargetKind.LeftHand,
                    "左手を腰・hip付近へ意図的に置く位置。" +
                    "休止位置や『手を下ろす』意味ではない。"),
                Hand(
                    FreePoseSpatialTargetIds.LeftFront,
                    SemanticBodyTargetKind.LeftHand,
                    "左手を身体の前方へ出した位置。"),
                Hand(
                    FreePoseSpatialTargetIds.LeftSide,
                    SemanticBodyTargetKind.LeftHand,
                    "左腕・左手を身体の横方向へ広げた位置。"),
                Hand(
                    FreePoseSpatialTargetIds.LeftUp,
                    SemanticBodyTargetKind.LeftHand,
                    "左腕・左手を上方へ上げた位置。"),
                DynamicHand(SemanticBodyTargetKind.LeftHand)
            });
    }
}
