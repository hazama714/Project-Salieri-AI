// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Body.FreePose
{
    /// <summary>
    /// Pure Stage 3B mapping from a bounded semantic intent to one complete,
    /// locally allow-listed Production Pose ID.
    /// </summary>
    public sealed class LocalDeterministicPoseSelector : IFreePoseSelector
    {
        private readonly FreePoseCatalogV0 catalog;

        public LocalDeterministicPoseSelector()
            : this(FreePoseCatalogV0.Production)
        {
        }

        internal LocalDeterministicPoseSelector(FreePoseCatalogV0 catalog)
        {
            this.catalog = catalog;
        }

        public FreePoseSelectionResult Select(
            FreePoseSelectionRequest request)
        {
            string poseId;
            switch (request.Intent)
            {
                case FreePoseSemanticIntent.Unknown:
                    return FreePoseSelectionResult.NoSelection(
                        request.Intent,
                        "semantic_intent_unmapped");

                case FreePoseSemanticIntent.RightExpressive:
                    poseId = FreePoseProductionPoseIds.RightChest;
                    break;

                case FreePoseSemanticIntent.LeftExpressive:
                    poseId = FreePoseProductionPoseIds.LeftSide;
                    break;

                case FreePoseSemanticIntent.BilateralExpressive:
                    poseId = FreePoseProductionPoseIds.BothSide;
                    break;

                case FreePoseSemanticIntent.ForwardPresentation:
                    poseId = FreePoseProductionPoseIds.BothFront;
                    break;

                default:
                    return FreePoseSelectionResult.Invalid(
                        request.Intent,
                        "semantic_intent_invalid");
            }

            string reason = "production_pose_catalog_unavailable";
            if (catalog == null ||
                !catalog.TryResolve(poseId, out _, out reason))
            {
                return FreePoseSelectionResult.Invalid(
                    request.Intent,
                    string.IsNullOrWhiteSpace(reason)
                        ? "production_pose_catalog_unavailable"
                        : reason);
            }

            return FreePoseSelectionResult.Selected(
                request.Intent,
                poseId);
        }
    }
}
