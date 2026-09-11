// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

namespace SalieriAI.Body.SpatialTarget
{
    /// <summary>
    /// Exclusive per-arm owner and the only runtime writer of the shared
    /// world-space hand target positions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandTargetAuthority : MonoBehaviour
    {
        [Header("World Hand Targets")]
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform leftHandTarget;

        [Header("Diagnostics")]
        [SerializeField] private bool logStateTransitions;

        private readonly ArmSlot right = new ArmSlot(HandTargetArm.Right);
        private readonly ArmSlot left = new ArmSlot(HandTargetArm.Left);

        private void Awake()
        {
            EnsureTargetsBound();
        }

        private void OnDisable()
        {
            // Lifecycle cleanup only restores arms that still have an active
            // owner. A pose deliberately released with Hold is left intact.
            InvalidateActiveLease(right, restoreBaseline: true);
            InvalidateActiveLease(left, restoreBaseline: true);
        }

        public bool TryAcquire(
            HandTargetArm arm,
            HandTargetOwnerKind owner,
            out HandTargetLease lease,
            out string reason)
        {
            EnsureTargetsBound();
            ArmSlot slot = GetSlot(arm);
            lease = default;

            if (slot.Target == null || !slot.BaselineCaptured)
            {
                reason = arm + " HandTarget is not available.";
                return false;
            }

            if (slot.HasOwner)
            {
                reason = arm + " HandTarget is busy. owner=" + slot.Owner;
                return false;
            }

            slot.Generation = NextGeneration(slot.Generation);
            slot.LeaseId = Guid.NewGuid();
            slot.Owner = owner;
            slot.HasOwner = true;
            lease = new HandTargetLease(
                arm,
                owner,
                slot.LeaseId,
                slot.Generation);
            reason = string.Empty;
            LogTransition("ACQUIRED", slot);
            return true;
        }

        public bool TrySubmitWorldPosition(
            HandTargetLease lease,
            Vector3 worldPosition,
            out HandTargetAuthoritySnapshot snapshot,
            out string reason)
        {
            EnsureTargetsBound();
            ArmSlot slot = GetSlot(lease.Arm);
            if (!ValidateLease(slot, lease, out reason))
            {
                snapshot = CreateSnapshot(slot);
                return false;
            }

            if (!IsFinite(worldPosition))
            {
                reason = "HandTarget world position contains NaN or Infinity.";
                snapshot = CreateSnapshot(slot);
                return false;
            }

            // Synchronous apply intentionally avoids an implicit Script
            // Execution Order dependency or an additional frame of latency.
            slot.Target.position = worldPosition;
            slot.LastSubmittedWorldPosition = worldPosition;
            slot.LastSubmitFrame = Time.frameCount;
            snapshot = CreateSnapshot(slot);
            reason = string.Empty;
            return true;
        }

        public bool TryRelease(
            HandTargetLease lease,
            HandTargetReleasePolicy policy,
            out HandTargetAuthoritySnapshot snapshot,
            out string reason)
        {
            EnsureTargetsBound();
            ArmSlot slot = GetSlot(lease.Arm);
            if (!ValidateLease(slot, lease, out reason))
            {
                snapshot = CreateSnapshot(slot);
                return false;
            }

            if (policy == HandTargetReleasePolicy.RestoreBaseline &&
                slot.Target != null &&
                slot.BaselineCaptured)
            {
                slot.Target.position = slot.BaselineWorldPosition;
                slot.LastSubmittedWorldPosition = slot.BaselineWorldPosition;
                slot.LastSubmitFrame = Time.frameCount;
            }

            LogTransition("RELEASED_" + policy, slot);
            ClearOwner(slot);
            snapshot = CreateSnapshot(slot);
            reason = string.Empty;
            return true;
        }

        public bool IsLeaseActive(HandTargetLease lease)
        {
            EnsureTargetsBound();
            return ValidateLease(GetSlot(lease.Arm), lease, out _);
        }

        public HandTargetAuthoritySnapshot GetSnapshot(HandTargetArm arm)
        {
            EnsureTargetsBound();
            return CreateSnapshot(GetSlot(arm));
        }

        private void EnsureTargetsBound()
        {
            BindTarget(right, rightHandTarget);
            BindTarget(left, leftHandTarget);
        }

        private static void BindTarget(ArmSlot slot, Transform target)
        {
            if (slot.Target == target && slot.BaselineCaptured)
                return;

            if (slot.HasOwner)
                return;

            slot.Target = target;
            slot.BaselineCaptured = target != null;
            slot.BaselineWorldPosition = target != null
                ? target.position
                : Vector3.zero;
            slot.LastSubmittedWorldPosition = slot.BaselineWorldPosition;
            slot.LastSubmitFrame = -1;
        }

        private ArmSlot GetSlot(HandTargetArm arm)
        {
            return arm == HandTargetArm.Right ? right : left;
        }

        private static bool ValidateLease(
            ArmSlot slot,
            HandTargetLease lease,
            out string reason)
        {
            if (!lease.IsValid ||
                !slot.HasOwner ||
                slot.Arm != lease.Arm ||
                slot.Owner != lease.Owner ||
                slot.LeaseId != lease.LeaseId ||
                slot.Generation != lease.Generation)
            {
                reason = "HandTarget lease is stale or is not the active owner.";
                return false;
            }

            if (slot.Target == null)
            {
                reason = slot.Arm + " HandTarget is not available.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void InvalidateActiveLease(
            ArmSlot slot,
            bool restoreBaseline)
        {
            if (!slot.HasOwner)
                return;

            if (restoreBaseline &&
                slot.Target != null &&
                slot.BaselineCaptured)
            {
                slot.Target.position = slot.BaselineWorldPosition;
                slot.LastSubmittedWorldPosition = slot.BaselineWorldPosition;
                slot.LastSubmitFrame = Time.frameCount;
            }

            LogTransition("INVALIDATED", slot);
            ClearOwner(slot);
        }

        private static void ClearOwner(ArmSlot slot)
        {
            slot.HasOwner = false;
            slot.LeaseId = Guid.Empty;
            slot.Owner = default;
        }

        private static HandTargetAuthoritySnapshot CreateSnapshot(ArmSlot slot)
        {
            Vector3 applied = slot.Target != null
                ? slot.Target.position
                : Vector3.zero;
            return new HandTargetAuthoritySnapshot(
                slot.Arm,
                slot.Target != null,
                slot.HasOwner,
                slot.Owner,
                slot.LeaseId,
                slot.Generation,
                slot.BaselineWorldPosition,
                applied,
                slot.LastSubmittedWorldPosition,
                slot.LastSubmitFrame);
        }

        private void LogTransition(string transition, ArmSlot slot)
        {
            if (!logStateTransitions)
                return;

            Debug.Log(
                "[HandTargetAuthority] " + transition +
                " arm=" + slot.Arm +
                " owner=" + slot.Owner +
                " generation=" + slot.Generation,
                this);
        }

        private static long NextGeneration(long current)
        {
            return current == long.MaxValue ? 1L : current + 1L;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class ArmSlot
        {
            public HandTargetArm Arm { get; }
            public Transform Target;
            public bool BaselineCaptured;
            public Vector3 BaselineWorldPosition;
            public bool HasOwner;
            public HandTargetOwnerKind Owner;
            public Guid LeaseId;
            public long Generation;
            public Vector3 LastSubmittedWorldPosition;
            public int LastSubmitFrame = -1;

            public ArmSlot(HandTargetArm arm)
            {
                Arm = arm;
            }
        }
    }
}
