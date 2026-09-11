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
    /// Complete Production Pose IDs. These are not Spatial Target IDs.
    /// </summary>
    public static class FreePoseProductionPoseIds
    {
        public const string RightChest =
            "freepose_v0_pose_right_chest";
        public const string LeftSide =
            "freepose_v0_pose_left_side";
        public const string BothSide =
            "freepose_v0_pose_both_side";
        public const string BothFront =
            "freepose_v0_pose_both_front";
    }

    /// <summary>
    /// Stage 3B source of truth for complete Production Free Poses.
    /// The catalog stores semantic IDs only; Scene coordinates remain owned
    /// by SpatialTargetRegistry and are resolved by the existing executor.
    /// </summary>
    public sealed class FreePoseCatalogV0
    {
        private readonly Dictionary<string, FreePoseDefinition> definitions =
            new Dictionary<string, FreePoseDefinition>(StringComparer.Ordinal);
        private readonly List<string> poseIds = new List<string>();

        public static FreePoseCatalogV0 Production => production;
        public bool IsValid { get; private set; }
        public string ValidationError { get; private set; }
        public IReadOnlyList<string> PoseIds => poseIds.AsReadOnly();

        internal FreePoseCatalogV0(
            IEnumerable<FreePoseDefinition> sourceDefinitions)
        {
            ValidationError = string.Empty;
            IsValid = TryInitialize(sourceDefinitions, out string error);
            if (!IsValid)
            {
                definitions.Clear();
                poseIds.Clear();
                ValidationError = error;
            }
        }

        public bool TryResolve(
            string poseId,
            out FreePoseDefinition definition,
            out string reason)
        {
            definition = null;

            if (!IsValid)
            {
                reason = "free_pose_catalog_invalid:" + ValidationError;
                return false;
            }

            if (string.IsNullOrWhiteSpace(poseId))
            {
                reason = "production_pose_id_missing";
                return false;
            }

            if (!definitions.TryGetValue(poseId, out definition))
            {
                reason = "production_pose_id_not_found";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool TryInitialize(
            IEnumerable<FreePoseDefinition> sourceDefinitions,
            out string reason)
        {
            if (sourceDefinitions == null)
            {
                reason = "catalog_definitions_missing";
                return false;
            }

            foreach (FreePoseDefinition definition in sourceDefinitions)
            {
                if (!TryValidateProductionDefinition(
                        definition,
                        out reason))
                {
                    return false;
                }

                if (definitions.ContainsKey(definition.PoseId))
                {
                    reason = "production_pose_id_duplicate:" +
                        definition.PoseId;
                    return false;
                }

                definitions.Add(definition.PoseId, definition);
                poseIds.Add(definition.PoseId);
            }

            if (definitions.Count == 0)
            {
                reason = "catalog_empty";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool TryValidateProductionDefinition(
            FreePoseDefinition definition,
            out string reason)
        {
            if (definition == null)
            {
                reason = "catalog_definition_missing";
                return false;
            }

            if (!definition.TryValidate(out reason))
                return false;

            if (!string.Equals(
                    definition.PoseId,
                    definition.PoseId.Trim(),
                    StringComparison.Ordinal))
            {
                reason = "production_pose_id_not_canonical";
                return false;
            }

            if (definition.PoseId.StartsWith(
                    "debug_",
                    StringComparison.OrdinalIgnoreCase))
            {
                reason = "debug_pose_id_forbidden";
                return false;
            }

            if (definition.HeadIntent != FreePoseHeadIntent.Keep)
            {
                reason = "stage3b_v0_head_intent_must_keep";
                return false;
            }

            if (!TryValidateArmTarget(
                    definition.RightArm,
                    true,
                    out reason))
            {
                return false;
            }

            return TryValidateArmTarget(
                definition.LeftArm,
                false,
                out reason);
        }

        private static bool TryValidateArmTarget(
            FreePoseArmDirective directive,
            bool rightArm,
            out string reason)
        {
            if (directive.IsKeep)
            {
                reason = string.Empty;
                return true;
            }

            string id = directive.SpatialTargetId;
            string[] known = rightArm
                ? RightSpatialTargetIds
                : LeftSpatialTargetIds;
            for (int i = 0; i < known.Length; i++)
            {
                if (string.Equals(id, known[i], StringComparison.Ordinal))
                {
                    reason = string.Empty;
                    return true;
                }
            }

            reason = rightArm
                ? "production_right_spatial_target_invalid"
                : "production_left_spatial_target_invalid";
            return false;
        }

        private static IEnumerable<FreePoseDefinition>
            CreateProductionDefinitions()
        {
            return new[]
            {
                Definition(
                    FreePoseProductionPoseIds.RightChest,
                    FreePoseSpatialTargetIds.RightChest,
                    null),
                Definition(
                    FreePoseProductionPoseIds.LeftSide,
                    null,
                    FreePoseSpatialTargetIds.LeftSide),
                Definition(
                    FreePoseProductionPoseIds.BothSide,
                    FreePoseSpatialTargetIds.RightSide,
                    FreePoseSpatialTargetIds.LeftSide),
                Definition(
                    FreePoseProductionPoseIds.BothFront,
                    FreePoseSpatialTargetIds.RightFront,
                    FreePoseSpatialTargetIds.LeftFront)
            };
        }

        private static FreePoseDefinition Definition(
            string poseId,
            string rightTargetId,
            string leftTargetId)
        {
            return new FreePoseDefinition(
                poseId,
                rightTargetId == null
                    ? FreePoseArmDirective.Keep()
                    : FreePoseArmDirective.ForSpatialTarget(rightTargetId),
                leftTargetId == null
                    ? FreePoseArmDirective.Keep()
                    : FreePoseArmDirective.ForSpatialTarget(leftTargetId),
                FreePoseHeadIntent.Keep);
        }

        private static readonly string[] RightSpatialTargetIds =
        {
            FreePoseSpatialTargetIds.RightNeutral,
            FreePoseSpatialTargetIds.RightChest,
            FreePoseSpatialTargetIds.RightWaist,
            FreePoseSpatialTargetIds.RightFront,
            FreePoseSpatialTargetIds.RightSide,
            FreePoseSpatialTargetIds.RightUp
        };

        private static readonly string[] LeftSpatialTargetIds =
        {
            FreePoseSpatialTargetIds.LeftNeutral,
            FreePoseSpatialTargetIds.LeftChest,
            FreePoseSpatialTargetIds.LeftWaist,
            FreePoseSpatialTargetIds.LeftFront,
            FreePoseSpatialTargetIds.LeftSide,
            FreePoseSpatialTargetIds.LeftUp
        };

        // Construct the Production catalog only after both allow-list arrays
        // have completed static initialization.
        private static readonly FreePoseCatalogV0 production =
            new FreePoseCatalogV0(CreateProductionDefinitions());
    }
}
