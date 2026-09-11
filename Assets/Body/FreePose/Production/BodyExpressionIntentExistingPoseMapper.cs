// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

namespace SalieriAI.Body.FreePose
{
    public enum BodyExpressionPoseMappingStatus
    {
        Unspecified = 0,
        Selected = 1,
        NoSelection = 2
    }

    /// <summary>
    /// Pure semantic result. Selection does not mean that Production accepted
    /// or executed a pose request.
    /// </summary>
    public readonly struct BodyExpressionPoseMappingResult
    {
        public BodyExpressionPoseMappingStatus Status { get; }
        public FreePoseSemanticIntent SemanticIntent { get; }
        public string Reason { get; }

        public bool IsSelected =>
            Status == BodyExpressionPoseMappingStatus.Selected;

        private BodyExpressionPoseMappingResult(
            BodyExpressionPoseMappingStatus status,
            FreePoseSemanticIntent semanticIntent,
            string reason)
        {
            Status = status;
            SemanticIntent = semanticIntent;
            Reason = reason ?? string.Empty;
        }

        internal static BodyExpressionPoseMappingResult Selected(
            FreePoseSemanticIntent semanticIntent)
        {
            return new BodyExpressionPoseMappingResult(
                BodyExpressionPoseMappingStatus.Selected,
                semanticIntent,
                string.Empty);
        }

        internal static BodyExpressionPoseMappingResult NoSelection(
            string reason)
        {
            return new BodyExpressionPoseMappingResult(
                BodyExpressionPoseMappingStatus.NoSelection,
                FreePoseSemanticIntent.Unknown,
                reason);
        }
    }

    /// <summary>
    /// Exact v0 mapping to the four existing Production semantic poses only.
    /// It deliberately leaves unmatched demand visible as NoSelection.
    /// </summary>
    public static class BodyExpressionIntentExistingPoseMapper
    {
        public static BodyExpressionPoseMappingResult Map(
            string overallPoseIntent,
            string rightArmIntent,
            string leftArmIntent)
        {
            if (Is(rightArmIntent, "Emphasize") &&
                Is(leftArmIntent, "Keep"))
            {
                return BodyExpressionPoseMappingResult.Selected(
                    FreePoseSemanticIntent.RightExpressive);
            }

            if (Is(rightArmIntent, "Keep") &&
                Is(leftArmIntent, "Emphasize"))
            {
                return BodyExpressionPoseMappingResult.Selected(
                    FreePoseSemanticIntent.LeftExpressive);
            }

            if (Is(rightArmIntent, "Open") &&
                Is(leftArmIntent, "Open"))
            {
                return BodyExpressionPoseMappingResult.Selected(
                    FreePoseSemanticIntent.BilateralExpressive);
            }

            if (Is(rightArmIntent, "Present") &&
                Is(leftArmIntent, "Present"))
            {
                return BodyExpressionPoseMappingResult.Selected(
                    FreePoseSemanticIntent.ForwardPresentation);
            }

            if (Is(rightArmIntent, "Present") &&
                Is(leftArmIntent, "Keep"))
            {
                return BodyExpressionPoseMappingResult.NoSelection(
                    "no_existing_unilateral_right_presentation_pose");
            }

            if (Is(rightArmIntent, "Keep") &&
                Is(leftArmIntent, "Present"))
            {
                return BodyExpressionPoseMappingResult.NoSelection(
                    "no_existing_unilateral_left_presentation_pose");
            }

            if (Is(rightArmIntent, "Keep") &&
                Is(leftArmIntent, "Keep"))
            {
                return BodyExpressionPoseMappingResult.NoSelection(
                    "body_expression_arms_keep");
            }

            return BodyExpressionPoseMappingResult.NoSelection(
                "no_exact_existing_production_pose");
        }

        private static bool Is(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.Ordinal);
        }
    }
}
