// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using UnityEngine;

namespace SalieriAI.Body.Frames.Observation
{
    /// <summary>
    /// Inspector-facing diagnostic snapshot of one existing Transform.
    /// This is not a second body-orientation IR and never owns the Transform.
    /// </summary>
    [Serializable]
    public sealed class BodyFrameObservationSnapshot
    {
        [SerializeField] private string frameKey = string.Empty;
        [SerializeField] private bool valid;
        [SerializeField] private int capturedFrame = -1;
        [SerializeField] private string capturedAtUtc = string.Empty;
        [SerializeField] private Vector3 worldPosition;
        [SerializeField] private Quaternion worldRotation = Quaternion.identity;
        [SerializeField] private Quaternion localRotation = Quaternion.identity;
        [SerializeField] private Vector3 forward = Vector3.forward;
        [SerializeField] private Vector3 up = Vector3.up;
        [SerializeField] private Vector3 right = Vector3.right;

        public string FrameKey => frameKey;
        public bool Valid => valid;
        public int CapturedFrame => capturedFrame;
        public string CapturedAtUtc => capturedAtUtc;
        public Vector3 WorldPosition => worldPosition;
        public Quaternion WorldRotation => worldRotation;
        public Quaternion LocalRotation => localRotation;
        public Vector3 Forward => forward;
        public Vector3 Up => up;
        public Vector3 Right => right;

        internal void Capture(
            string key,
            Transform source,
            int frame,
            DateTime capturedUtc)
        {
            frameKey = key ?? string.Empty;
            capturedFrame = frame;
            capturedAtUtc = capturedUtc.ToString("O");
            valid = source != null;
            if (!valid)
            {
                worldPosition = Vector3.zero;
                worldRotation = Quaternion.identity;
                localRotation = Quaternion.identity;
                forward = Vector3.forward;
                up = Vector3.up;
                right = Vector3.right;
                return;
            }

            worldPosition = source.position;
            worldRotation = source.rotation;
            localRotation = source.localRotation;
            forward = source.forward;
            up = source.up;
            right = source.right;
        }
    }
}
