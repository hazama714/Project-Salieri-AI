// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

namespace SalieriAI.Body.FreePose
{
    public enum ProductionPoseRequestStatus
    {
        Unspecified = 0,
        Executed = 1,
        InvalidRequest = 2,
        NoSelection = 3,
        SelectionRejected = 4,
        CatalogRejected = 5,
        ExecutionUnavailable = 6,
        ExecutionRejected = 7
    }

    /// <summary>
    /// Explicit Production-facing Free Pose request. Identity is supplied by
    /// the caller so Stage 3C-A does not introduce another ID generator.
    /// </summary>
    public sealed class ProductionPoseRequest
    {
        public string RequestId { get; }
        public string Source { get; }
        public FreePoseSemanticIntent SemanticIntent { get; }

        public ProductionPoseRequest(
            string requestId,
            string source,
            FreePoseSemanticIntent semanticIntent)
        {
            RequestId = requestId ?? string.Empty;
            Source = source ?? string.Empty;
            SemanticIntent = semanticIntent;
        }
    }

    /// <summary>
    /// Production request outcome retaining both Stage 3B selection and
    /// execution handoff results. Executed is the only accepted terminal.
    /// </summary>
    public sealed class ProductionPoseRequestResult
    {
        public string RequestId { get; }
        public string Source { get; }
        public FreePoseSemanticIntent SemanticIntent { get; }
        public ProductionPoseRequestStatus Status { get; }
        public FreePoseSelectionResult Selection { get; }
        public FreePoseExecutionHandoffResult Execution { get; }
        public string Reason { get; }

        public bool IsExecutionAccepted =>
            Status == ProductionPoseRequestStatus.Executed &&
            Execution.ExecutionSucceeded;

        internal ProductionPoseRequestResult(
            ProductionPoseRequest request,
            ProductionPoseRequestStatus status,
            FreePoseSelectionResult selection,
            FreePoseExecutionHandoffResult execution,
            string reason)
        {
            RequestId = request != null
                ? request.RequestId
                : string.Empty;
            Source = request != null
                ? request.Source
                : string.Empty;
            SemanticIntent = request != null
                ? request.SemanticIntent
                : FreePoseSemanticIntent.Unknown;
            Status = status;
            Selection = selection;
            Execution = execution;
            Reason = reason ?? string.Empty;
        }
    }

    public enum ProductionHandTargetRequestStatus
    {
        Unspecified = 0,
        Executed = 1,
        NoChange = 2,
        InvalidRequest = 3,
        TargetRejected = 4,
        ExecutionUnavailable = 5,
        ExecutionRejected = 6
    }

    /// <summary>
    /// Direct semantic-target request from Conversation. The values remain
    /// untrusted until the Production service applies the v0 allow-list.
    /// </summary>
    public sealed class ProductionHandTargetRequest
    {
        public string RequestId { get; }
        public string Source { get; }
        public string RightHandTarget { get; }
        public string LeftHandTarget { get; }

        public ProductionHandTargetRequest(
            string requestId,
            string source,
            string rightHandTarget,
            string leftHandTarget)
        {
            RequestId = requestId ?? string.Empty;
            Source = source ?? string.Empty;
            RightHandTarget = rightHandTarget ?? string.Empty;
            LeftHandTarget = leftHandTarget ?? string.Empty;
        }
    }

    public sealed class ProductionHandTargetRequestResult
    {
        public string RequestId { get; }
        public string Source { get; }
        public ProductionHandTargetRequestStatus Status { get; }
        public ConversationalHandTargetSelection Selection { get; }
        public string Reason { get; }

        public bool IsExecutionAccepted =>
            Status == ProductionHandTargetRequestStatus.Executed;

        internal ProductionHandTargetRequestResult(
            ProductionHandTargetRequest request,
            ProductionHandTargetRequestStatus status,
            ConversationalHandTargetSelection selection,
            string reason)
        {
            RequestId = request != null ? request.RequestId : string.Empty;
            Source = request != null ? request.Source : string.Empty;
            Status = status;
            Selection = selection;
            Reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Read-only execution availability used by semantic observers. It does
    /// not expose the executor or imply physical pose completion.
    /// </summary>
    public sealed class ProductionPoseRuntimeSnapshot
    {
        public bool ExecutionStateAvailable { get; }
        public bool IsActive { get; }
        public bool IsArmTransitioning { get; }
        public string ActivePoseId { get; }
        public string RightArmSpatialTargetId { get; }
        public string LeftArmSpatialTargetId { get; }
        public ProductionPoseRequestResult LastResult { get; }

        internal ProductionPoseRuntimeSnapshot(
            bool executionStateAvailable,
            bool isActive,
            bool isArmTransitioning,
            string activePoseId,
            string rightArmSpatialTargetId,
            string leftArmSpatialTargetId,
            ProductionPoseRequestResult lastResult)
        {
            ExecutionStateAvailable = executionStateAvailable;
            IsActive = isActive;
            IsArmTransitioning = isArmTransitioning;
            ActivePoseId = activePoseId ?? string.Empty;
            RightArmSpatialTargetId = rightArmSpatialTargetId ?? string.Empty;
            LeftArmSpatialTargetId = leftArmSpatialTargetId ?? string.Empty;
            LastResult = lastResult;
        }
    }
}
