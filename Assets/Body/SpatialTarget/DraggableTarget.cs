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
    [RequireComponent(typeof(Collider))]
    public sealed class DraggableTarget : MonoBehaviour
    {
        private Vector3 offset;
        private float zCoord;
        private Camera targetCamera;

        private void Awake()
        {
            targetCamera = Camera.main;

            if (targetCamera == null)
            {
                Debug.LogError(
                    "[DraggableTarget] MainCameraが見つかりません。",
                    this
                );
            }
        }

        private void OnMouseDown()
        {
            if (targetCamera == null)
            {
                return;
            }

            zCoord = targetCamera
                .WorldToScreenPoint(transform.position)
                .z;

            offset =
                transform.position -
                GetMouseWorldPosition();
        }

        private void OnMouseDrag()
        {
            if (targetCamera == null)
            {
                return;
            }

            transform.position =
                GetMouseWorldPosition() +
                offset;
        }

        private Vector3 GetMouseWorldPosition()
        {
            Vector3 mousePoint =
                Input.mousePosition;

            mousePoint.z =
                zCoord;

            return targetCamera
                .ScreenToWorldPoint(mousePoint);
        }
    }
}