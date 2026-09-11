// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Body.Retargeting.Neck
{
    /// <summary>
    /// Read-only adapter from the M6.5 continuous commanded pose. It performs
    /// no LOOK calculation, smoothing, servo conversion, or feedback synthesis.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NeckControllerCommandedStateSource :
        MonoBehaviour,
        INeckJointStateSource
    {
        [SerializeField] private NeckController neckController = null;

        public NeckController NeckController => neckController;

        public bool TryGetSnapshot(out NeckJointStateSnapshot snapshot)
        {
            if (neckController == null)
            {
                snapshot = default;
                return false;
            }

            snapshot = NeckJointStateSnapshot.CommandedOnly(
                neckController.CommandedYawRelativeAngle,
                neckController.CommandedPitchRelativeAngle);
            return true;
        }
    }
}
