// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Sensors.Camera.Common;

namespace SalieriAI.Sensors.Camera.Dummy
{
    public class DummyFacePerception : MonoBehaviour, IFacePerception
    {
        [Header("Dummy Face")]
        [SerializeField] private bool hasFace = true;

        [Header("Frame")]
        [SerializeField] private float frameWidth = 640f;

        [SerializeField] private float frameHeight = 480f;

        [Header("Face Rect")]
        [SerializeField] private float faceWidth = 160f;

        [SerializeField] private float faceHeight = 160f;

        [SerializeField] private float faceCenterX = 320f;

        [SerializeField] private float faceCenterY = 240f;

        [Header("Demo Motion")]
        [SerializeField] private bool autoMove = false;

        [SerializeField] private float moveSpeed = 0.7f;

        [SerializeField] private float moveRangeX = 160f;

        [SerializeField] private float moveRangeY = 80f;

        private float _baseX;
        private float _baseY;

        public bool HasFace => hasFace;

        public float FrameWidth => frameWidth;
        public float FrameHeight => frameHeight;

        public float FaceWidth => faceWidth;
        public float FaceHeight => faceHeight;

        public float FaceCenterX => faceCenterX;
        public float FaceCenterY => faceCenterY;

        public float FaceX => faceCenterX - (faceWidth * 0.5f);
        public float FaceY => faceCenterY - (faceHeight * 0.5f);

        private void Awake()
        {
            _baseX = faceCenterX;
            _baseY = faceCenterY;
        }

        private void Update()
        {
            if (!autoMove)
                return;

            faceCenterX =
                _baseX + Mathf.Sin(Time.time * moveSpeed) * moveRangeX;

            faceCenterY =
                _baseY + Mathf.Cos(Time.time * moveSpeed * 0.6f) * moveRangeY;
        }
    }
}