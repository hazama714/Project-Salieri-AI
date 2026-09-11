// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Body.SpatialTarget
{
    /// <summary>
    /// Authority-aware replacement for DraggableTarget on shared HandTargets.
    /// Other draggable body targets continue to use DraggableTarget.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class HandTargetManualDragAdapter : MonoBehaviour
    {
        [SerializeField] private HandTargetAuthority authority;
        [SerializeField] private HandTargetArm arm;

        private Camera mainCamera;
        private HandTargetLease lease;
        private bool hasLease;
        private Vector3 dragOffset;
        private float screenDepth;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void OnDisable()
        {
            ReleaseIfHeld();
        }

        private void OnMouseDown()
        {
            if (authority == null || mainCamera == null || hasLease)
                return;

            if (!authority.TryAcquire(
                    arm,
                    HandTargetOwnerKind.ManualDrag,
                    out lease,
                    out _))
            {
                return;
            }

            hasLease = true;
            HandTargetAuthoritySnapshot snapshot =
                authority.GetSnapshot(arm);
            Vector3 screen = mainCamera.WorldToScreenPoint(
                snapshot.AppliedWorldPosition);
            screenDepth = screen.z;
            dragOffset = snapshot.AppliedWorldPosition -
                MouseWorldPosition();
        }

        private void OnMouseDrag()
        {
            if (!hasLease || authority == null)
                return;

            Vector3 desired = MouseWorldPosition() + dragOffset;
            if (!authority.TrySubmitWorldPosition(
                    lease,
                    desired,
                    out _,
                    out _))
            {
                hasLease = false;
                lease = default;
            }
        }

        private void OnMouseUp()
        {
            ReleaseIfHeld();
        }

        private Vector3 MouseWorldPosition()
        {
            Vector3 mouse = Input.mousePosition;
            mouse.z = screenDepth;
            return mainCamera.ScreenToWorldPoint(mouse);
        }

        private void ReleaseIfHeld()
        {
            if (!hasLease)
                return;

            if (authority != null)
            {
                authority.TryRelease(
                    lease,
                    HandTargetReleasePolicy.Hold,
                    out _,
                    out _);
            }

            hasLease = false;
            lease = default;
        }
    }
}
