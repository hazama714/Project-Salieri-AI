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
    /// <summary>
    /// Single explicit Production request boundary for Free Pose v0.
    /// It owns neither selection policy nor body execution resources.
    /// </summary>
    public sealed class ProductionPoseRequestService
    {
        public const string ServiceVersion =
            "free-pose-production-request-stage3c-a-v0";

        private readonly IFreePoseSelector selector;
        private readonly FreePoseSelectionExecutionHandoff executionHandoff;
        private readonly Action<string> logSink;
        private readonly FreePoseExecutor executor;
        private readonly FreePoseApplyPose applyDirectHandTargets;

        public int RequestAttemptCount { get; private set; }
        public int ExecutionAcceptedCount { get; private set; }
        public ProductionPoseRequestResult LastResult { get; private set; }

        public ProductionPoseRequestService(
            FreePoseExecutor executor,
            Action<string> logSink = null)
            : this(
                new LocalDeterministicPoseSelector(),
                new FreePoseSelectionExecutionHandoff(
                    FreePoseCatalogV0.Production,
                    executor),
                executor,
                executor != null
                    ? (FreePoseApplyPose)executor.TryApplyPose
                    : null,
                logSink)
        {
        }

        public ProductionPoseRequestService(
            IFreePoseSelector selector,
            FreePoseSelectionExecutionHandoff executionHandoff,
            Action<string> logSink = null)
            : this(selector, executionHandoff, null, null, logSink)
        {
        }

        internal ProductionPoseRequestService(
            IFreePoseSelector selector,
            FreePoseSelectionExecutionHandoff executionHandoff,
            FreePoseExecutor executor,
            FreePoseApplyPose applyDirectHandTargets,
            Action<string> logSink = null)
        {
            this.selector = selector ??
                throw new ArgumentNullException(nameof(selector));
            this.executionHandoff = executionHandoff ??
                throw new ArgumentNullException(nameof(executionHandoff));
            this.executor = executor;
            this.applyDirectHandTargets = applyDirectHandTargets;
            this.logSink = logSink;
        }

        public ProductionPoseRequestResult RequestPose(
            ProductionPoseRequest request)
        {
            RequestAttemptCount++;
            Log(
                "[ProductionPoseRequest][RECEIVED] requestId=" +
                Text(request != null ? request.RequestId : string.Empty) +
                " source=" +
                Text(request != null ? request.Source : string.Empty) +
                " intent=" +
                (request != null
                    ? request.SemanticIntent.ToString()
                    : "<null>"));

            if (request == null ||
                string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.Source))
            {
                return Store(
                    request,
                    ProductionPoseRequestStatus.InvalidRequest,
                    default,
                    default,
                    "production_pose_request_invalid");
            }

            FreePoseSelectionResult selection;
            try
            {
                selection = selector.Select(
                    new FreePoseSelectionRequest(
                        request.SemanticIntent));
            }
            catch (Exception exception)
            {
                return Store(
                    request,
                    ProductionPoseRequestStatus.SelectionRejected,
                    default,
                    default,
                    "production_pose_selection_exception:" +
                    exception.GetType().Name);
            }

            Log(
                "[ProductionPoseRequest][SELECTION] requestId=" +
                Text(request.RequestId) +
                " status=" + selection.Status +
                " poseId=" + Text(selection.PoseId) +
                " reason=" + Text(selection.Reason));

            FreePoseExecutionHandoffResult execution =
                executionHandoff.TryExecute(selection);
            ProductionPoseRequestStatus status = Map(execution.Status);
            string reason = !string.IsNullOrWhiteSpace(execution.Reason)
                ? execution.Reason
                : selection.Reason;
            return Store(request, status, selection, execution, reason);
        }

        public ProductionHandTargetRequestResult RequestHandTargets(
            ProductionHandTargetRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.Source))
            {
                return DirectResult(
                    request,
                    ProductionHandTargetRequestStatus.InvalidRequest,
                    default,
                    "production_hand_target_request_invalid");
            }

            if (!ConversationalHandTargetSelectionV0.TryNormalize(
                    request.RightHandTarget,
                    request.LeftHandTarget,
                    out ConversationalHandTargetSelection selection,
                    out string reason))
            {
                return DirectResult(
                    request,
                    ProductionHandTargetRequestStatus.TargetRejected,
                    default,
                    reason);
            }

            if (!selection.HasTargetChange)
            {
                return DirectResult(
                    request,
                    ProductionHandTargetRequestStatus.NoChange,
                    selection,
                    "conversation_hand_targets_keep");
            }

            if (!ConversationalHandTargetSelectionV0.TryCreateDefinition(
                    request.RequestId,
                    selection,
                    out FreePoseDefinition definition,
                    out reason))
            {
                return DirectResult(
                    request,
                    ProductionHandTargetRequestStatus.TargetRejected,
                    selection,
                    reason);
            }

            if (applyDirectHandTargets == null)
            {
                return DirectResult(
                    request,
                    ProductionHandTargetRequestStatus.ExecutionUnavailable,
                    selection,
                    "free_pose_executor_missing");
            }

            try
            {
                if (!applyDirectHandTargets(definition, out reason))
                {
                    return DirectResult(
                        request,
                        ProductionHandTargetRequestStatus.ExecutionRejected,
                        selection,
                        string.IsNullOrWhiteSpace(reason)
                            ? "conversation_hand_target_execution_rejected"
                            : reason);
                }
            }
            catch (Exception exception)
            {
                return DirectResult(
                    request,
                    ProductionHandTargetRequestStatus.ExecutionRejected,
                    selection,
                    "conversation_hand_target_execution_exception:" +
                    exception.GetType().Name);
            }

            return DirectResult(
                request,
                ProductionHandTargetRequestStatus.Executed,
                selection,
                string.Empty);
        }

        public ProductionPoseRuntimeSnapshot CaptureRuntimeSnapshot()
        {
            FreePoseDefinition definition = executor != null
                ? executor.ActiveDefinition
                : null;
            return new ProductionPoseRuntimeSnapshot(
                executor != null,
                executor != null && executor.IsActive,
                executor != null && executor.IsArmTransitioning,
                executor != null ? executor.ActivePoseId : string.Empty,
                definition != null && !definition.RightArm.IsKeep
                    ? definition.RightArm.SpatialTargetId
                    : string.Empty,
                definition != null && !definition.LeftArm.IsKeep
                    ? definition.LeftArm.SpatialTargetId
                    : string.Empty,
                LastResult);
        }

        /// <summary>
        /// Releases only the currently active Free Pose ownership. Existing
        /// Hold semantics are preserved; this does not command Neutral.
        /// </summary>
        public bool TryReleaseActivePose(out string reason)
        {
            if (executor == null || !executor.IsActive)
            {
                reason = string.Empty;
                return true;
            }

            try
            {
                return executor.ReleasePose(out reason);
            }
            catch (Exception exception)
            {
                reason = "production_pose_release_exception:" +
                    exception.GetType().Name;
                return false;
            }
        }

        private ProductionPoseRequestResult Store(
            ProductionPoseRequest request,
            ProductionPoseRequestStatus status,
            FreePoseSelectionResult selection,
            FreePoseExecutionHandoffResult execution,
            string reason)
        {
            var result = new ProductionPoseRequestResult(
                request,
                status,
                selection,
                execution,
                reason);
            if (result.IsExecutionAccepted)
                ExecutionAcceptedCount++;
            LastResult = result;

            Log(
                "[ProductionPoseRequest][RESULT] requestId=" +
                Text(result.RequestId) +
                " status=" + result.Status +
                " selection=" + result.Selection.Status +
                " poseId=" + Text(result.Selection.PoseId) +
                " execution=" + result.Execution.Status +
                " reason=" + Text(result.Reason));
            return result;
        }

        private ProductionHandTargetRequestResult DirectResult(
            ProductionHandTargetRequest request,
            ProductionHandTargetRequestStatus status,
            ConversationalHandTargetSelection selection,
            string reason)
        {
            var result = new ProductionHandTargetRequestResult(
                request, status, selection, reason);
            Log(
                "[ProductionHandTargetRequest][RESULT] requestId=" +
                Text(result.RequestId) +
                " status=" + result.Status +
                " right=" + Text(result.Selection.RightHandTarget) +
                " left=" + Text(result.Selection.LeftHandTarget) +
                " reason=" + Text(result.Reason));
            return result;
        }

        private static ProductionPoseRequestStatus Map(
            FreePoseExecutionHandoffStatus status)
        {
            switch (status)
            {
                case FreePoseExecutionHandoffStatus.Executed:
                    return ProductionPoseRequestStatus.Executed;
                case FreePoseExecutionHandoffStatus.NotSelected:
                    return ProductionPoseRequestStatus.NoSelection;
                case FreePoseExecutionHandoffStatus.SelectionRejected:
                    return ProductionPoseRequestStatus.SelectionRejected;
                case FreePoseExecutionHandoffStatus.CatalogRejected:
                    return ProductionPoseRequestStatus.CatalogRejected;
                case FreePoseExecutionHandoffStatus.ExecutionUnavailable:
                    return ProductionPoseRequestStatus.ExecutionUnavailable;
                case FreePoseExecutionHandoffStatus.ExecutionRejected:
                    return ProductionPoseRequestStatus.ExecutionRejected;
                default:
                    return ProductionPoseRequestStatus.ExecutionRejected;
            }
        }

        private void Log(string message)
        {
            logSink?.Invoke(message);
        }

        private static string Text(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "<empty>"
                : value;
        }
    }
}
