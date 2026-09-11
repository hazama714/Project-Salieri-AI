// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Body.Frames
{
    /// <summary>
    /// Project-wide canonical body axis contract.
    /// +Z is forward, +Y is up, and +X is right.
    /// </summary>
    public static class CanonicalBodyAxes
    {
        public const string ContractVersion = "body-orientation-v0.1";

        public static Vector3 Forward => Vector3.forward;
        public static Vector3 Up => Vector3.up;
        public static Vector3 Right => Vector3.right;
    }

    /// <summary>
    /// Describes the upstream fact that supplied a body orientation.
    /// Selection between sources belongs to a future authority policy.
    /// </summary>
    public enum BodyOrientationSource
    {
        Unknown = 0,
        Explicit = 1,
        InitialPose = 2,
        VirtualBody = 3,
        PhysicalBody = 4,
        Navigation = 5
    }

    /// <summary>
    /// Identifies the frame in which an orientation value is expressed.
    /// This is semantic metadata and never resolves a Unity Transform.
    /// </summary>
    public enum BodyReferenceFrame
    {
        Unknown = 0,
        World = 1,
        BodyPlacement = 2,
        BodyOrientation = 3,
        VirtualHips = 4,
        ParentSegment = 5
    }

    public enum BodySegment
    {
        Unknown = 0,
        Pelvis = 1,
        Spine = 2,
        Chest = 3,
        UpperChest = 4,
        Neck = 5,
        Head = 6
    }
}

