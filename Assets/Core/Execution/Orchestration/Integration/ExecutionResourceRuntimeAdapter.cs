// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

/* Project Salieri AI - Apache-2.0 */
using System;
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Execution.Orchestration.Contracts;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    public interface IExecutionResourceAvailabilitySource
    {
        ResourceShadowAvailabilityStatus Query(
            string resourceId,
            string resourceType,
            ExecutionResourceAccessMode accessMode,
            out string reason);
    }

    public sealed class ExecutionResourceManagerAvailabilitySource :
        IExecutionResourceAvailabilitySource
    {
        private readonly ExecutionResourceManager manager;

        public ExecutionResourceManagerAvailabilitySource(
            ExecutionResourceManager manager)
        {
            this.manager = manager;
        }

        public ResourceShadowAvailabilityStatus Query(
            string resourceId,
            string resourceType,
            ExecutionResourceAccessMode accessMode,
            out string reason)
        {
            reason = string.Empty;
            if (manager == null)
            {
                reason = "RESOURCE_MANAGER_UNAVAILABLE";
                return ResourceShadowAvailabilityStatus.Failed;
            }

            bool available;
            if (!manager.TryGetResourceAvailability(
                    resourceId, out available, out reason))
                return ResourceShadowAvailabilityStatus.Unknown;
            return available
                ? ResourceShadowAvailabilityStatus.Available
                : ResourceShadowAvailabilityStatus.Unavailable;
        }
    }

    /// <summary>
    /// Read-only profile availability adapter. It never acquires or releases
    /// a runtime resource and never creates an external lease ID.
    /// </summary>
    public sealed class ExecutionResourceRuntimeAdapter
    {
        public const string AdapterVersion =
            "execution-resource-runtime-adapter-3c3.1";

        private readonly IExecutionResourceAvailabilitySource source;

        public ExecutionResourceRuntimeAdapter(
            ExecutionResourceManager manager)
            : this(new ExecutionResourceManagerAvailabilitySource(manager))
        {
        }

        public ExecutionResourceRuntimeAdapter(
            IExecutionResourceAvailabilitySource source)
        {
            this.source = source;
        }

        public ResourceShadowAvailabilityResult QueryAvailability(
            ResourceAcquireRequest request,
            DateTime observedAtUtc)
        {
            if (request == null)
                return Failed(null, observedAtUtc,
                    "RESOURCE_REQUEST_NULL");
            if (string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.PlanId) ||
                string.IsNullOrWhiteSpace(request.StepId) ||
                string.IsNullOrWhiteSpace(request.ResourceId) ||
                observedAtUtc == default(DateTime))
                return Failed(request, observedAtUtc,
                    "RESOURCE_REQUEST_INVALID");
            if (source == null)
                return Failed(request, observedAtUtc,
                    "RESOURCE_SOURCE_UNAVAILABLE");

            try
            {
                string reason;
                ResourceShadowAvailabilityStatus status = source.Query(
                    request.ResourceId, request.ResourceType,
                    request.AccessMode, out reason);
                if (status != ResourceShadowAvailabilityStatus.Available &&
                    status !=
                        ResourceShadowAvailabilityStatus.Unavailable &&
                    status != ResourceShadowAvailabilityStatus.Unknown &&
                    status != ResourceShadowAvailabilityStatus.Failed)
                    return Failed(request, observedAtUtc,
                        "RESOURCE_STATUS_INVALID");
                return Result(request, observedAtUtc, status, reason);
            }
            catch (Exception exception)
            {
                return Failed(request, observedAtUtc,
                    "RESOURCE_QUERY_EXCEPTION:" +
                    exception.GetType().Name);
            }
        }

        private static ResourceShadowAvailabilityResult Failed(
            ResourceAcquireRequest request,
            DateTime observedAtUtc,
            string reason)
        {
            return Result(request, observedAtUtc,
                ResourceShadowAvailabilityStatus.Failed, reason);
        }

        private static ResourceShadowAvailabilityResult Result(
            ResourceAcquireRequest request,
            DateTime observedAtUtc,
            ResourceShadowAvailabilityStatus status,
            string reason)
        {
            return new ResourceShadowAvailabilityResult(
                request != null ? request.RequestId : string.Empty,
                request != null ? request.PlanId : string.Empty,
                request != null ? request.StepId : string.Empty,
                request != null
                    ? request.ExecutionAttemptId : string.Empty,
                request != null ? request.ResourceId : string.Empty,
                request != null ? request.ResourceType : string.Empty,
                request != null
                    ? request.AccessMode
                    : ExecutionResourceAccessMode.Exclusive,
                status, reason, observedAtUtc);
        }
    }
}
