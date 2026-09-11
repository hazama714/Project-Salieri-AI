// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using SalieriAI.Core.Execution.Orchestration.Adapters;
using SalieriAI.Core.Limbo;

namespace SalieriAI.Core.Execution.Orchestration.Integration
{
    public static class ExecutionPermissionKinds
    {
        public const string Think = "CanThink";
        public const string TrackOrientation = "CanTrackOrientation";
        public const string MoveServo = "CanMoveServo";
        public const string StartAction = "CanStartAction";
        public const string Speak = "CanSpeak";
        public const string Search = "CanSearch";
        public const string Interrupt = "CanInterrupt";
    }

    /// <summary>
    /// Read-only boundary used by the runtime adapter. A false return value
    /// means that the source could not answer; it does not mean Denied.
    /// </summary>
    public interface IExecutionPermissionReadOnlySource
    {
        bool TryRead(
            string permissionKind,
            out bool granted,
            out string reason);
    }

    /// <summary>
    /// Single-source-of-truth projection over LimboPermission. Only public
    /// get-only properties are read. No InteractionState rule is copied and
    /// no Limbo mutator is called.
    /// </summary>
    public sealed class LimboPermissionReadOnlySource :
        IExecutionPermissionReadOnlySource
    {
        private readonly LimboPermission limboPermission;

        public LimboPermissionReadOnlySource(
            LimboPermission limboPermission)
        {
            this.limboPermission = limboPermission;
        }

        public bool TryRead(
            string permissionKind,
            out bool granted,
            out string reason)
        {
            granted = false;
            reason = string.Empty;
            if (limboPermission == null)
            {
                reason = "LIMBO_PERMISSION_SOURCE_UNAVAILABLE";
                return false;
            }

            switch (permissionKind)
            {
                case ExecutionPermissionKinds.Think:
                    granted = limboPermission.CanThink;
                    break;
                case ExecutionPermissionKinds.TrackOrientation:
                    granted = limboPermission.CanTrackOrientation;
                    break;
                case ExecutionPermissionKinds.MoveServo:
                    granted = limboPermission.CanMoveServo;
                    break;
                case ExecutionPermissionKinds.StartAction:
                    granted = limboPermission.CanStartAction;
                    break;
                case ExecutionPermissionKinds.Speak:
                    granted = limboPermission.CanSpeak;
                    break;
                case ExecutionPermissionKinds.Search:
                    granted = limboPermission.CanSearch;
                    break;
                case ExecutionPermissionKinds.Interrupt:
                    granted = limboPermission.CanInterrupt;
                    break;
                default:
                    reason = "PERMISSION_KIND_UNSUPPORTED:" +
                        (permissionKind ?? string.Empty);
                    return false;
            }

            if (!granted)
                reason = "LIMBO_PERMISSION_DENIED:" + permissionKind;
            return true;
        }
    }

    /// <summary>
    /// Converts one immutable PermissionCheckRequest into one immutable
    /// PermissionAdapterResult. It never generates IDs or time, and never
    /// calls a reducer or another runtime domain.
    /// </summary>
    public sealed class ExecutionPermissionRuntimeAdapter
    {
        public const string AdapterVersion =
            "execution-permission-runtime-adapter-3c2.1";

        private readonly IExecutionPermissionReadOnlySource source;

        public ExecutionPermissionRuntimeAdapter(
            LimboPermission limboPermission)
            : this(new LimboPermissionReadOnlySource(limboPermission))
        {
        }

        public ExecutionPermissionRuntimeAdapter(
            IExecutionPermissionReadOnlySource source)
        {
            this.source = source;
        }

        public PermissionAdapterResult Query(
            PermissionCheckRequest request,
            DateTime occurredAtUtc)
        {
            if (request == null)
                return Failed(null, occurredAtUtc,
                    "PERMISSION_REQUEST_NULL");
            if (string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.PlanId) ||
                string.IsNullOrWhiteSpace(request.StepId) ||
                string.IsNullOrWhiteSpace(request.PermissionKind) ||
                occurredAtUtc == default(DateTime))
            {
                return Failed(request, occurredAtUtc,
                    "PERMISSION_REQUEST_INVALID");
            }
            if (source == null)
                return Failed(request, occurredAtUtc,
                    "PERMISSION_SOURCE_UNAVAILABLE");

            try
            {
                bool granted;
                string reason;
                if (!source.TryRead(
                        request.PermissionKind,
                        out granted,
                        out reason))
                {
                    return Failed(request, occurredAtUtc,
                        string.IsNullOrEmpty(reason)
                            ? "PERMISSION_QUERY_FAILED" : reason);
                }

                return new PermissionAdapterResult(
                    request.RequestId,
                    request.PlanId,
                    request.StepId,
                    request.ExecutionAttemptId,
                    granted
                        ? PermissionAdapterStatus.Granted
                        : PermissionAdapterStatus.Denied,
                    granted ? string.Empty : reason,
                    occurredAtUtc);
            }
            catch (Exception exception)
            {
                return Failed(request, occurredAtUtc,
                    "PERMISSION_QUERY_EXCEPTION:" +
                    exception.GetType().Name);
            }
        }

        private static PermissionAdapterResult Failed(
            PermissionCheckRequest request,
            DateTime occurredAtUtc,
            string reason)
        {
            return new PermissionAdapterResult(
                request != null ? request.RequestId : string.Empty,
                request != null ? request.PlanId : string.Empty,
                request != null ? request.StepId : string.Empty,
                request != null
                    ? request.ExecutionAttemptId : string.Empty,
                PermissionAdapterStatus.Failed,
                reason,
                occurredAtUtc);
        }
    }
}
