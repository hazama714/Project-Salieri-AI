// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

public class SimpleCameraOrbit : MonoBehaviour
{
    public enum RotationTargetType
    {
        Head,
        Chest,
        Root
    }

    [Header("回転対象")]
    [SerializeField]
    private RotationTargetType rotationTargetType = RotationTargetType.Head;

    [Header("注視対象（未設定時は自動検索）")]
    [SerializeField]
    private Transform target;

    [Header("初期アングル")]
    [SerializeField]
    private float defaultHorizontalAngle = 180f;

    [SerializeField]
    private float defaultVerticalAngle = 5f;

    [SerializeField]
    private float defaultDistance = 1.5f;

    [Header("操作速度")]
    [SerializeField]
    private float zoomSpeed = 0.5f;

    [SerializeField]
    private float rotateSpeed = 2f;

    [SerializeField]
    private float panSpeed = 0.2f;

    [Header("操作制限")]
    [SerializeField]
    private float minimumVerticalAngle = -60f;

    [SerializeField]
    private float maximumVerticalAngle = 80f;

    [SerializeField]
    private float minimumDistance = 0.5f;

    [SerializeField]
    private float maximumDistance = 10f;

    private float horizontalAngle;
    private float verticalAngle;
    private float currentDistance;

    // target自体を動かさず、カメラの注視位置だけをずらす。
    private Vector3 focusOffset;

    private void Start()
    {
        horizontalAngle = defaultHorizontalAngle;
        verticalAngle = defaultVerticalAngle;
        currentDistance = defaultDistance;
        focusOffset = Vector3.zero;

        if (target == null)
        {
            target = FindTargetTransform(rotationTargetType);
        }

        if (target == null)
        {
            Debug.LogWarning(
                "[SimpleCameraOrbit] 注視対象が見つかりません。InspectorでTargetを設定してください。",
                this);
            return;
        }

        Debug.Log(
            $"[SimpleCameraOrbit] Target={target.name}",
            this);

        UpdateCameraTransform();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        bool didUpdate = false;

        // 右ドラッグ：回転
        if (Input.GetMouseButton(1))
        {
            horizontalAngle += Input.GetAxis("Mouse X") * rotateSpeed;
            verticalAngle -= Input.GetAxis("Mouse Y") * rotateSpeed;

            verticalAngle = Mathf.Clamp(
                verticalAngle,
                minimumVerticalAngle,
                maximumVerticalAngle);

            didUpdate = true;
        }

        // ホイール：ズーム
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.0001f)
        {
            currentDistance -= scroll * zoomSpeed;

            currentDistance = Mathf.Clamp(
                currentDistance,
                minimumDistance,
                maximumDistance);

            didUpdate = true;
        }

        // 中ドラッグ：注視位置の平行移動
        if (Input.GetMouseButton(2))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            Vector3 pan =
                transform.right * -mouseX
                + transform.up * -mouseY;

            focusOffset += pan * panSpeed;
            didUpdate = true;
        }

        if (didUpdate)
        {
            UpdateCameraTransform();
        }
    }

    public void ResetCameraPosition()
    {
        horizontalAngle = defaultHorizontalAngle;
        verticalAngle = defaultVerticalAngle;
        currentDistance = defaultDistance;
        focusOffset = Vector3.zero;

        UpdateCameraTransform();

        Debug.Log(
            "[SimpleCameraOrbit] Camera reset.",
            this);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        focusOffset = Vector3.zero;

        UpdateCameraTransform();
    }

    public void SetRotationTargetType(RotationTargetType newTargetType)
    {
        rotationTargetType = newTargetType;
        target = FindTargetTransform(rotationTargetType);
        focusOffset = Vector3.zero;

        UpdateCameraTransform();
    }

    private Transform FindTargetTransform(RotationTargetType type)
    {
        string keyword = type.ToString().ToLowerInvariant();
        Transform fallback = null;

        Transform[] transforms = FindObjectsOfType<Transform>();

        foreach (Transform candidate in transforms)
        {
            string objectName = candidate.name.ToLowerInvariant();

            if (objectName.Contains(keyword))
            {
                return candidate;
            }

            if (fallback == null
                && type == RotationTargetType.Chest
                && objectName.Contains("spine"))
            {
                fallback = candidate;
            }

            if (fallback == null
                && type == RotationTargetType.Root
                && objectName.Contains("hips"))
            {
                fallback = candidate;
            }
        }

        return fallback;
    }

    private void UpdateCameraTransform()
    {
        if (target == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(
            verticalAngle,
            horizontalAngle,
            0f);

        Vector3 focusPosition = target.position + focusOffset;
        Vector3 cameraOffset = rotation * new Vector3(
            0f,
            0f,
            -currentDistance);

        transform.position = focusPosition + cameraOffset;
        transform.rotation = rotation;
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null)
        {
            return;
        }

        Vector3 focusPosition = target.position + focusOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(focusPosition, 0.05f);
        Gizmos.DrawLine(transform.position, focusPosition);
    }
}