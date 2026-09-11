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
    /// Single runtime availability boundary for the existing Stage 3C-A
    /// service. It adds no queue, scheduler, selection, or execution policy.
    /// </summary>
    public static class ProductionPoseRequestRuntime
    {
        private static ProductionPoseRequestService current;

        public static bool IsAvailable => current != null;

        public static bool TryRegister(
            ProductionPoseRequestService service)
        {
            if (service == null)
                return false;

            if (current != null && !ReferenceEquals(current, service))
                return false;

            current = service;
            return true;
        }

        public static void Unregister(
            ProductionPoseRequestService service)
        {
            if (ReferenceEquals(current, service))
                current = null;
        }

        public static bool TryRequest(
            ProductionPoseRequest request,
            out ProductionPoseRequestResult result,
            out string reason)
        {
            result = null;
            if (current == null)
            {
                reason = "production_pose_runtime_unavailable";
                return false;
            }

            try
            {
                result = current.RequestPose(request);
                reason = result != null
                    ? result.Reason
                    : "production_pose_result_missing";
                return true;
            }
            catch (Exception exception)
            {
                reason = "production_pose_runtime_exception:" +
                    exception.GetType().Name;
                return false;
            }
        }

        public static bool TryCaptureSnapshot(
            out ProductionPoseRuntimeSnapshot snapshot)
        {
            if (current == null)
            {
                snapshot = null;
                return false;
            }

            snapshot = current.CaptureRuntimeSnapshot();
            return snapshot != null;
        }

        public static bool TryReleaseActivePose(out string reason)
        {
            if (current == null)
            {
                reason = "production_pose_runtime_unavailable";
                return false;
            }

            return current.TryReleaseActivePose(out reason);
        }

        public static bool TryRequestHandTargets(
            ProductionHandTargetRequest request,
            out ProductionHandTargetRequestResult result,
            out string reason)
        {
            result = null;
            if (current == null)
            {
                reason = "production_pose_runtime_unavailable";
                return false;
            }

            try
            {
                result = current.RequestHandTargets(request);
                reason = result != null
                    ? result.Reason
                    : "production_hand_target_result_missing";
                return true;
            }
            catch (Exception exception)
            {
                reason = "production_hand_target_runtime_exception:" +
                    exception.GetType().Name;
                return false;
            }
        }

        internal static void ResetForTests()
        {
            current = null;
        }
    }
}
