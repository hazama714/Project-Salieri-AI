// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using VRM;

public sealed class MouthController_VRM0 : MonoBehaviour, IMouthController
{
    [SerializeField]
    private VRMBlendShapeProxy blendShapeProxy;

    [Header("Mouth")]
    [SerializeField]
    private BlendShapePreset mouthPreset = BlendShapePreset.A;

    public void SetMouthOpen(float value)
    {
        if (blendShapeProxy == null)
            return;

        value = Mathf.Clamp01(value);

        BlendShapeKey key = BlendShapeKey.CreateFromPreset(mouthPreset);

        blendShapeProxy.ImmediatelySetValue(key, value);
    }

    public void CloseMouth()
    {
        SetMouthOpen(0f);
    }
}