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
    /// Small Stage 3B semantic input set. It is intentionally independent
    /// from InteractionState and does not describe coordinates or joints.
    /// </summary>
    public enum FreePoseSemanticIntent
    {
        Unknown = 0,
        RightExpressive = 1,
        LeftExpressive = 2,
        BilateralExpressive = 3,
        ForwardPresentation = 4
    }

    public enum FreePoseSelectionStatus
    {
        Unspecified = 0,
        Selected = 1,
        NoSelection = 2,
        Invalid = 3
    }

    /// <summary>
    /// Immutable semantic request snapshot. Stage 3B deliberately carries
    /// no Scene object, Transform, spatial coordinate, joint, or servo data.
    /// </summary>
    public readonly struct FreePoseSelectionRequest
    {
        public FreePoseSemanticIntent Intent { get; }

        public FreePoseSelectionRequest(FreePoseSemanticIntent intent)
        {
            Intent = intent;
        }
    }

    /// <summary>
    /// Selection outcome only. A Selected result does not mean that the
    /// existing FreePoseExecutor accepted or executed the pose.
    /// </summary>
    public readonly struct FreePoseSelectionResult
    {
        public FreePoseSelectionStatus Status { get; }
        public FreePoseSemanticIntent Intent { get; }
        public string PoseId { get; }
        public string Reason { get; }

        public bool IsSelected =>
            Status == FreePoseSelectionStatus.Selected;

        private FreePoseSelectionResult(
            FreePoseSelectionStatus status,
            FreePoseSemanticIntent intent,
            string poseId,
            string reason)
        {
            Status = status;
            Intent = intent;
            PoseId = poseId;
            Reason = reason ?? string.Empty;
        }

        public static FreePoseSelectionResult Selected(
            FreePoseSemanticIntent intent,
            string poseId)
        {
            return new FreePoseSelectionResult(
                FreePoseSelectionStatus.Selected,
                intent,
                poseId,
                string.Empty);
        }

        public static FreePoseSelectionResult NoSelection(
            FreePoseSemanticIntent intent,
            string reason)
        {
            return new FreePoseSelectionResult(
                FreePoseSelectionStatus.NoSelection,
                intent,
                string.Empty,
                reason);
        }

        public static FreePoseSelectionResult Invalid(
            FreePoseSemanticIntent intent,
            string reason)
        {
            return new FreePoseSelectionResult(
                FreePoseSelectionStatus.Invalid,
                intent,
                string.Empty,
                reason);
        }
    }

    public interface IFreePoseSelector
    {
        FreePoseSelectionResult Select(FreePoseSelectionRequest request);
    }
}
