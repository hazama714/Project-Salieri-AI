// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;

namespace SalieriAI.Expression.Motion
{
    /// <summary>
    /// Read-only observation of the Animator-solved VRM neck/head pose.
    /// This state owns no servo, retargeting, or physical output semantics.
    /// </summary>
    [Serializable]
    public sealed class VirtualNeckPoseState
    {
        [SerializeField] private bool valid;
        [SerializeField] private int capturedFrame = -1;
        [SerializeField] private string capturedAtUtc = string.Empty;
        [SerializeField] private string targetKey = string.Empty;

        [SerializeField] private Quaternion neckNeutralRelativeRotation =
            Quaternion.identity;
        [SerializeField] private Quaternion headNeutralRelativeRotation =
            Quaternion.identity;
        [SerializeField]
        private Quaternion combinedHeadNeutralRelativeRotation =
            Quaternion.identity;
        [SerializeField]
        private Vector3 headForwardInBodyFrame = Vector3.forward;

        [SerializeField] private float headRelativeYaw;
        [SerializeField] private float headRelativePitch;

        [SerializeField] private bool neckBoneAvailable;
        [SerializeField] private bool headBoneAvailable;
        [SerializeField] private bool leftEyeBoneAvailable;
        [SerializeField] private bool rightEyeBoneAvailable;
        [SerializeField] private bool eyeMidpointAvailable;
        [SerializeField] private Vector3 eyeMidpointWorld;

        public bool Valid => valid;
        public int CapturedFrame => capturedFrame;
        public string CapturedAtUtc => capturedAtUtc;
        public string TargetKey => targetKey;
        public Quaternion NeckNeutralRelativeRotation =>
            neckNeutralRelativeRotation;
        public Quaternion HeadNeutralRelativeRotation =>
            headNeutralRelativeRotation;
        public Quaternion CombinedHeadNeutralRelativeRotation =>
            combinedHeadNeutralRelativeRotation;
        public Vector3 HeadForwardInBodyFrame => headForwardInBodyFrame;
        public float HeadRelativeYaw => headRelativeYaw;
        public float HeadRelativePitch => headRelativePitch;
        public bool NeckBoneAvailable => neckBoneAvailable;
        public bool HeadBoneAvailable => headBoneAvailable;
        public bool LeftEyeBoneAvailable => leftEyeBoneAvailable;
        public bool RightEyeBoneAvailable => rightEyeBoneAvailable;
        public bool EyeMidpointAvailable => eyeMidpointAvailable;
        public Vector3 EyeMidpointWorld => eyeMidpointWorld;

        internal void SetSolved(
            int frame,
            DateTime capturedUtc,
            string sourceTargetKey,
            Quaternion neckDelta,
            Quaternion headDelta,
            Quaternion combinedHeadDelta,
            Vector3 bodyFrameHeadForward,
            float yaw,
            float pitch,
            bool hasLeftEye,
            bool hasRightEye,
            Vector3 midpoint)
        {
            valid = true;
            capturedFrame = frame;
            capturedAtUtc = capturedUtc.ToUniversalTime().ToString("O");
            targetKey = sourceTargetKey ?? string.Empty;
            neckNeutralRelativeRotation = neckDelta;
            headNeutralRelativeRotation = headDelta;
            combinedHeadNeutralRelativeRotation = combinedHeadDelta;
            headForwardInBodyFrame = bodyFrameHeadForward;
            headRelativeYaw = yaw;
            headRelativePitch = pitch;
            neckBoneAvailable = true;
            headBoneAvailable = true;
            leftEyeBoneAvailable = hasLeftEye;
            rightEyeBoneAvailable = hasRightEye;
            eyeMidpointAvailable = hasLeftEye && hasRightEye;
            eyeMidpointWorld = eyeMidpointAvailable
                ? midpoint
                : Vector3.zero;
        }

        internal void SetUnavailable(
            int frame,
            DateTime capturedUtc,
            string sourceTargetKey,
            bool hasNeck,
            bool hasHead,
            bool hasLeftEye,
            bool hasRightEye)
        {
            valid = false;
            capturedFrame = frame;
            capturedAtUtc = capturedUtc.ToUniversalTime().ToString("O");
            targetKey = sourceTargetKey ?? string.Empty;
            neckNeutralRelativeRotation = Quaternion.identity;
            headNeutralRelativeRotation = Quaternion.identity;
            combinedHeadNeutralRelativeRotation = Quaternion.identity;
            headForwardInBodyFrame = Vector3.forward;
            headRelativeYaw = 0f;
            headRelativePitch = 0f;
            neckBoneAvailable = hasNeck;
            headBoneAvailable = hasHead;
            leftEyeBoneAvailable = hasLeftEye;
            rightEyeBoneAvailable = hasRightEye;
            eyeMidpointAvailable = false;
            eyeMidpointWorld = Vector3.zero;
        }
    }
}
