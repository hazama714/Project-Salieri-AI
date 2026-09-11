// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using UnityEngine.UI;
using SalieriAI.Core.Perception.ObjectTargeting;

namespace SalieriAI.App.UI.Perception
{
    /// <summary>
    /// Displays the current non-person observation target as a UI rectangle
    /// over the camera preview.
    ///
    /// This component is display-only:
    /// - It does not select targets.
    /// - It does not move the neck, eyes, avatar, or physical body.
    /// - It does not modify the existing red face target UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObservationTargetBoxUI : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField]
        private ObservationTargetSelectionService selectionService;

        [Header("UI")]
        [Tooltip("Camera preview RectTransform, normally WipeRawImage.")]
        [SerializeField]
        private RectTransform previewRect;

        [Tooltip("Blue object-target frame RectTransform.")]
        [SerializeField]
        private RectTransform box;

        [Tooltip("Graphic used by the target frame. Usually the Image on the box object.")]
        [SerializeField]
        private Graphic boxGraphic;

        [Header("Display")]
        [SerializeField]
        private Color boxColor =
            new Color(
                0.10f,
                0.55f,
                1.00f,
                1.00f
            );

        [Tooltip("Enable only when the displayed preview is horizontally mirrored.")]
        [SerializeField]
        private bool mirrorX;

        [Tooltip("Enable only when the displayed preview is vertically mirrored.")]
        [SerializeField]
        private bool mirrorY;

        [Header("Debug")]
        [SerializeField]
        private bool logVisibilityChanges;

        private bool subscribed;
        private bool visible;
        private ObservationTarget currentTarget;

        private void Awake()
        {
            ResolveGraphic();
            ApplyGraphicSettings();
            HideBox("Awake");
        }

        private void OnEnable()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            Subscribe();

            if (selectionService.CurrentTarget != null)
            {
                HandleTargetUpdated(
                    selectionService.CurrentTarget
                );
            }
            else
            {
                HideBox("No current target");
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            currentTarget = null;
            HideBox("Component disabled");
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            ResolveGraphic();
            ApplyGraphicSettings();
        }

        private void Subscribe()
        {
            if (subscribed ||
                selectionService == null)
            {
                return;
            }

            selectionService.TargetUpdated +=
                HandleTargetUpdated;

            selectionService.TargetCleared +=
                HandleTargetCleared;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed ||
                selectionService == null)
            {
                return;
            }

            selectionService.TargetUpdated -=
                HandleTargetUpdated;

            selectionService.TargetCleared -=
                HandleTargetCleared;

            subscribed = false;
        }

        private void HandleTargetUpdated(
            ObservationTarget target
        )
        {
            currentTarget = target;

            if (!TryApplyTarget(target))
            {
                HideBox("Target data is invalid");
            }
        }

        private void HandleTargetCleared(
            string reason
        )
        {
            currentTarget = null;
            HideBox(reason);
        }

        private bool TryApplyTarget(
            ObservationTarget target
        )
        {
            if (target == null ||
                target.TrackSnapshot == null ||
                previewRect == null ||
                box == null ||
                box.parent == null)
            {
                return false;
            }

            float sourceX1 =
                Mathf.Clamp01(
                    Mathf.Min(
                        target.TrackSnapshot.NormalizedX1,
                        target.TrackSnapshot.NormalizedX2
                    )
                );

            float sourceX2 =
                Mathf.Clamp01(
                    Mathf.Max(
                        target.TrackSnapshot.NormalizedX1,
                        target.TrackSnapshot.NormalizedX2
                    )
                );

            float sourceY1 =
                Mathf.Clamp01(
                    Mathf.Min(
                        target.TrackSnapshot.NormalizedY1,
                        target.TrackSnapshot.NormalizedY2
                    )
                );

            float sourceY2 =
                Mathf.Clamp01(
                    Mathf.Max(
                        target.TrackSnapshot.NormalizedY1,
                        target.TrackSnapshot.NormalizedY2
                    )
                );

            if (mirrorX)
            {
                float mirroredX1 =
                    1f - sourceX2;

                float mirroredX2 =
                    1f - sourceX1;

                sourceX1 = mirroredX1;
                sourceX2 = mirroredX2;
            }

            if (mirrorY)
            {
                float mirroredY1 =
                    1f - sourceY2;

                float mirroredY2 =
                    1f - sourceY1;

                sourceY1 = mirroredY1;
                sourceY2 = mirroredY2;
            }

            // Detection coordinates use a top-left origin.
            // Unity UI coordinates are bottom-up.
            float uiYMin =
                1f - sourceY2;

            float uiYMax =
                1f - sourceY1;

            Bounds previewBounds =
                RectTransformUtility
                    .CalculateRelativeRectTransformBounds(
                        box.parent,
                        previewRect
                    );

            if (previewBounds.size.x <= 0f ||
                previewBounds.size.y <= 0f)
            {
                return false;
            }

            float localLeft =
                Mathf.Lerp(
                    previewBounds.min.x,
                    previewBounds.max.x,
                    sourceX1
                );

            float localRight =
                Mathf.Lerp(
                    previewBounds.min.x,
                    previewBounds.max.x,
                    sourceX2
                );

            float localBottom =
                Mathf.Lerp(
                    previewBounds.min.y,
                    previewBounds.max.y,
                    uiYMin
                );

            float localTop =
                Mathf.Lerp(
                    previewBounds.min.y,
                    previewBounds.max.y,
                    uiYMax
                );

            float width =
                Mathf.Max(
                    1f,
                    localRight - localLeft
                );

            float height =
                Mathf.Max(
                    1f,
                    localTop - localBottom
                );

            Vector3 localPosition =
                box.localPosition;

            localPosition.x =
                (localLeft + localRight) *
                0.5f;

            localPosition.y =
                (localBottom + localTop) *
                0.5f;

            box.pivot =
                new Vector2(
                    0.5f,
                    0.5f
                );

            box.localPosition =
                localPosition;

            box.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                width
            );

            box.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                height
            );

            ShowBox();

            return true;
        }

        private void ShowBox()
        {
            if (box == null)
                return;

            ApplyGraphicSettings();

            if (!box.gameObject.activeSelf)
            {
                box.gameObject.SetActive(true);
            }

            if (!visible &&
                logVisibilityChanges)
            {
                Debug.Log(
                    "[ObservationTargetBoxUI][SHOW]",
                    this
                );
            }

            visible = true;
        }

        private void HideBox(
            string reason
        )
        {
            if (box != null &&
                box.gameObject.activeSelf)
            {
                box.gameObject.SetActive(false);
            }

            if (visible &&
                logVisibilityChanges)
            {
                Debug.Log(
                    "[ObservationTargetBoxUI][HIDE] " +
                    $"Reason={reason}",
                    this
                );
            }

            visible = false;
        }

        private void ResolveGraphic()
        {
            if (boxGraphic == null &&
                box != null)
            {
                boxGraphic =
                    box.GetComponent<Graphic>();
            }
        }

        private void ApplyGraphicSettings()
        {
            if (boxGraphic == null)
                return;

            boxGraphic.color =
                boxColor;

            boxGraphic.raycastTarget =
                false;
        }

        private bool ValidateReferences()
        {
            bool valid = true;

            if (selectionService == null)
            {
                Debug.LogError(
                    "[ObservationTargetBoxUI][ERROR] " +
                    "ObservationTargetSelectionService is not assigned.",
                    this
                );

                valid = false;
            }

            if (previewRect == null)
            {
                Debug.LogError(
                    "[ObservationTargetBoxUI][ERROR] " +
                    "Preview RectTransform is not assigned.",
                    this
                );

                valid = false;
            }

            if (box == null)
            {
                Debug.LogError(
                    "[ObservationTargetBoxUI][ERROR] " +
                    "Target box RectTransform is not assigned.",
                    this
                );

                valid = false;
            }

            return valid;
        }
    }
}
