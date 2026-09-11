// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Sensors.Camera.Common;

public class FaceBoxUI : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private MonoBehaviour detectorBehaviour;

    [Header("UI")]
    public RectTransform box;
    public RectTransform wipeRect;

    [Header("Mirror")]
    public bool mirrorX = false;
    public bool mirrorY = false;

    private IFacePerception detector;

    private void Awake()
    {
        detector = detectorBehaviour as IFacePerception;

        if (detectorBehaviour != null && detector == null)
        {
            Debug.LogWarning(
                "[FaceBoxUI] detectorBehaviour does not implement IFacePerception."
            );
        }
    }

    private void Update()
    {
        if (detector == null || box == null || wipeRect == null)
            return;

        if (!detector.HasFace)
        {
            box.gameObject.SetActive(false);
            return;
        }

        float frameW = detector.FrameWidth;
        float frameH = detector.FrameHeight;

        if (frameW <= 0f || frameH <= 0f)
        {
            box.gameObject.SetActive(false);
            return;
        }

        box.gameObject.SetActive(true);

        float x = detector.FaceCenterX / frameW;
        float y = detector.FaceCenterY / frameH;
        float w = detector.FaceWidth / frameW;
        float h = detector.FaceHeight / frameH;

        if (mirrorX)
            x = 1f - x;

        if (mirrorY)
            y = 1f - y;

        float wipeW = wipeRect.rect.width;
        float wipeH = wipeRect.rect.height;

        box.anchoredPosition = new Vector2(
            (x - 0.5f) * wipeW,
            (0.5f - y) * wipeH
        );

        box.sizeDelta = new Vector2(
            w * wipeW,
            h * wipeH
        );
    }
}