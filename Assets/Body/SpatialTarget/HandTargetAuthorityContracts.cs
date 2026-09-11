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
    public enum HandTargetArm
    {
        Right = 0,
        Left = 1
    }

    public enum HandTargetOwnerKind
    {
        ProductionPoint = 0,
        DebugPoint = 1,
        ManualDrag = 2,
        FreePose = 3
    }

    public enum HandTargetReleasePolicy
    {
        Hold = 0,
        RestoreBaseline = 1
    }

    public readonly struct HandTargetLease
    {
        public HandTargetArm Arm { get; }
        public HandTargetOwnerKind Owner { get; }
        public Guid LeaseId { get; }
        public long Generation { get; }

        public bool IsValid => LeaseId != Guid.Empty && Generation > 0;

        public HandTargetLease(
            HandTargetArm arm,
            HandTargetOwnerKind owner,
            Guid leaseId,
            long generation)
        {
            Arm = arm;
            Owner = owner;
            LeaseId = leaseId;
            Generation = generation;
        }
    }

    public readonly struct HandTargetAuthoritySnapshot
    {
        public HandTargetArm Arm { get; }
        public bool TargetAvailable { get; }
        public bool HasOwner { get; }
        public HandTargetOwnerKind Owner { get; }
        public Guid LeaseId { get; }
        public long Generation { get; }
        public Vector3 BaselineWorldPosition { get; }
        public Vector3 AppliedWorldPosition { get; }
        public Vector3 LastSubmittedWorldPosition { get; }
        public int LastSubmitFrame { get; }

        public HandTargetAuthoritySnapshot(
            HandTargetArm arm,
            bool targetAvailable,
            bool hasOwner,
            HandTargetOwnerKind owner,
            Guid leaseId,
            long generation,
            Vector3 baselineWorldPosition,
            Vector3 appliedWorldPosition,
            Vector3 lastSubmittedWorldPosition,
            int lastSubmitFrame)
        {
            Arm = arm;
            TargetAvailable = targetAvailable;
            HasOwner = hasOwner;
            Owner = owner;
            LeaseId = leaseId;
            Generation = generation;
            BaselineWorldPosition = baselineWorldPosition;
            AppliedWorldPosition = appliedWorldPosition;
            LastSubmittedWorldPosition = lastSubmittedWorldPosition;
            LastSubmitFrame = lastSubmitFrame;
        }
    }
}
