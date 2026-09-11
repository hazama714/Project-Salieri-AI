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
    internal delegate bool FreePoseApplyPose(
        FreePoseDefinition definition,
        out string reason);

    public enum FreePoseExecutionHandoffStatus
    {
        Unspecified = 0,
        Executed = 1,
        NotSelected = 2,
        SelectionRejected = 3,
        CatalogRejected = 4,
        ExecutionUnavailable = 5,
        ExecutionRejected = 6
    }

    /// <summary>
    /// Result of the execution handoff. SelectionStatus is retained so a
    /// caller cannot confuse a valid selection with successful execution.
    /// </summary>
    public readonly struct FreePoseExecutionHandoffResult
    {
        public FreePoseExecutionHandoffStatus Status { get; }
        public FreePoseSelectionStatus SelectionStatus { get; }
        public string PoseId { get; }
        public string Reason { get; }

        public bool SelectionSucceeded =>
            SelectionStatus == FreePoseSelectionStatus.Selected;
        public bool ExecutionSucceeded =>
            Status == FreePoseExecutionHandoffStatus.Executed;

        internal FreePoseExecutionHandoffResult(
            FreePoseExecutionHandoffStatus status,
            FreePoseSelectionStatus selectionStatus,
            string poseId,
            string reason)
        {
            Status = status;
            SelectionStatus = selectionStatus;
            PoseId = poseId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Revalidates an untrusted selected Pose ID and delegates execution only
    /// to the existing FreePoseExecutor.TryApplyPose boundary.
    /// </summary>
    public sealed class FreePoseSelectionExecutionHandoff
    {
        private readonly FreePoseCatalogV0 catalog;
        private readonly FreePoseApplyPose applyPose;

        public FreePoseSelectionExecutionHandoff(
            FreePoseCatalogV0 catalog,
            FreePoseExecutor executor)
            : this(
                catalog,
                executor != null
                    ? (FreePoseApplyPose)executor.TryApplyPose
                    : null)
        {
        }

        internal FreePoseSelectionExecutionHandoff(
            FreePoseCatalogV0 catalog,
            FreePoseApplyPose applyPose)
        {
            this.catalog = catalog;
            this.applyPose = applyPose;
        }

        public FreePoseExecutionHandoffResult TryExecute(
            FreePoseSelectionResult selection)
        {
            if (selection.Status == FreePoseSelectionStatus.NoSelection)
            {
                return Result(
                    FreePoseExecutionHandoffStatus.NotSelected,
                    selection,
                    selection.Reason);
            }

            if (selection.Status != FreePoseSelectionStatus.Selected)
            {
                return Result(
                    FreePoseExecutionHandoffStatus.SelectionRejected,
                    selection,
                    string.IsNullOrWhiteSpace(selection.Reason)
                        ? "free_pose_selection_not_selected"
                        : selection.Reason);
            }

            string catalogReason = "free_pose_catalog_unavailable";
            if (catalog == null ||
                !catalog.TryResolve(
                    selection.PoseId,
                    out FreePoseDefinition definition,
                    out catalogReason))
            {
                return Result(
                    FreePoseExecutionHandoffStatus.CatalogRejected,
                    selection,
                    string.IsNullOrWhiteSpace(catalogReason)
                        ? "free_pose_catalog_unavailable"
                        : catalogReason);
            }

            if (applyPose == null)
            {
                return Result(
                    FreePoseExecutionHandoffStatus.ExecutionUnavailable,
                    selection,
                    "free_pose_executor_missing");
            }

            try
            {
                if (!applyPose(definition, out string executionReason))
                {
                    return Result(
                        FreePoseExecutionHandoffStatus.ExecutionRejected,
                        selection,
                        string.IsNullOrWhiteSpace(executionReason)
                            ? "free_pose_execution_rejected"
                            : executionReason);
                }
            }
            catch (Exception exception)
            {
                return Result(
                    FreePoseExecutionHandoffStatus.ExecutionRejected,
                    selection,
                    "free_pose_execution_exception:" +
                    exception.GetType().Name);
            }

            return Result(
                FreePoseExecutionHandoffStatus.Executed,
                selection,
                string.Empty);
        }

        private static FreePoseExecutionHandoffResult Result(
            FreePoseExecutionHandoffStatus status,
            FreePoseSelectionResult selection,
            string reason)
        {
            return new FreePoseExecutionHandoffResult(
                status,
                selection.Status,
                selection.PoseId,
                reason);
        }
    }
}
