// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// v1.4 / RuntimeConnectionSettings support

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SalieriAI.Runtime;

public class ServoMaintenancePanel : MonoBehaviour
{
    public enum TargetAxis
    {
        Yaw,
        Pitch
    }

    [Header("Target")]
    public ServoControlUnit targetServo;
    public TargetAxis targetAxis = TargetAxis.Yaw;

    [Header("Sender")]
    public MonoBehaviour senderComponent;

    [Header("Runtime")]
    [SerializeField] private RuntimeConnectionSettings runtimeSettings;

    [Header("UI")]
    public Slider testAngleSlider;
    public Slider minSlider;
    public Slider maxSlider;
    public Slider offsetSlider;
    public Toggle invertToggle;

    public TMP_Text testAngleValueText;
    public TMP_Text minValueText;
    public TMP_Text maxValueText;
    public TMP_Text offsetValueText;

    [Header("Send Throttle")]
    public float minTestSendInterval = 0.05f;

    private bool isInitializing = false;
    private int lastSentTestAngle = -999;
    private float lastTestSendTime = -999f;

    private ServoControlUnit TargetServo
    {
        get
        {
            return targetServo;
        }
    }

    private void Start()
    {
        if (runtimeSettings == null)
        {
            runtimeSettings = FindObjectOfType<RuntimeConnectionSettings>();
        }

        if (TargetServo == null)
        {
            Debug.LogWarning(
                $"[ServoMaintenancePanel] TargetServo is null. " +
                $"GameObject:{gameObject.name} Axis:{targetAxis}"
            );
            return;
        }

        InitializeServoSender();

        SetupUIFromServo();
        RegisterEvents();
        UpdateTexts();
    }

    private void InitializeServoSender()
    {
        if (senderComponent == null)
        {
            Debug.LogWarning(
                $"[ServoMaintenancePanel][SENDER_ERROR] senderComponent is null. " +
                $"GameObject:{gameObject.name} Axis:{targetAxis}"
            );
            return;
        }

        ICommandSender sender = senderComponent as ICommandSender;

        if (sender == null)
        {
            Debug.LogWarning(
                $"[ServoMaintenancePanel][SENDER_ERROR] senderComponent does not implement ICommandSender. " +
                $"GameObject:{gameObject.name} Axis:{targetAxis} Sender:{senderComponent.name}"
            );
            return;
        }

        TargetServo.Initialize(sender, runtimeSettings);

        Debug.Log(
            $"[ServoMaintenancePanel][SENDER_INIT] " +
            $"GameObject:{gameObject.name} Axis:{targetAxis} " +
            $"ServoID:{TargetServo.servoIndex} Sender:{senderComponent.name} " +
            $"RuntimeSettings:{(runtimeSettings != null ? runtimeSettings.name : "null")}"
        );
    }

    private void RegisterEvents()
    {
        if (testAngleSlider != null)
        {
            testAngleSlider.onValueChanged.RemoveAllListeners();
            testAngleSlider.onValueChanged.AddListener(OnTestAngleChanged);
        }

        if (minSlider != null)
        {
            minSlider.onValueChanged.RemoveAllListeners();
            minSlider.onValueChanged.AddListener(OnMinChanged);
        }

        if (maxSlider != null)
        {
            maxSlider.onValueChanged.RemoveAllListeners();
            maxSlider.onValueChanged.AddListener(OnMaxChanged);
        }

        if (offsetSlider != null)
        {
            offsetSlider.onValueChanged.RemoveAllListeners();
            offsetSlider.onValueChanged.AddListener(OnOffsetChanged);
        }

        if (invertToggle != null)
        {
            invertToggle.onValueChanged.RemoveAllListeners();
            invertToggle.onValueChanged.AddListener(OnInvertChanged);
        }
    }

    private void SetupUIFromServo()
    {
        ServoControlUnit servo = TargetServo;
        if (servo == null)
            return;

        isInitializing = true;

        if (testAngleSlider != null)
            testAngleSlider.SetValueWithoutNotify(90);

        if (minSlider != null)
            minSlider.SetValueWithoutNotify(servo.minAngle);

        if (maxSlider != null)
            maxSlider.SetValueWithoutNotify(servo.maxAngle);

        if (offsetSlider != null)
            offsetSlider.SetValueWithoutNotify(servo.offset);

        if (invertToggle != null)
            invertToggle.SetIsOnWithoutNotify(servo.invert);

        lastSentTestAngle = -999;
        lastTestSendTime = -999f;

        isInitializing = false;
    }

    private void OnTestAngleChanged(float value)
    {
        Debug.Log(
            $"[ServoMaintenancePanel][TEST_CHANGED] " +
            $"GameObject:{gameObject.name} " +
            $"Axis:{targetAxis} " +
            $"PanelID:{GetInstanceID()} " +
            $"SliderID:{testAngleSlider?.GetInstanceID()} " +
            $"Value:{value}"
        );

        int angle = Mathf.RoundToInt(value);

        UpdateTexts();

        if (isInitializing)
            return;

        ServoControlUnit servo = TargetServo;
        if (servo == null)
            return;

        if (angle == lastSentTestAngle)
            return;

        if (Time.realtimeSinceStartup - lastTestSendTime < minTestSendInterval)
            return;

        lastSentTestAngle = angle;
        lastTestSendTime = Time.realtimeSinceStartup;

        servo.SetAngle(angle);
    }

    private void OnMinChanged(float value)
    {
        ServoControlUnit servo = TargetServo;
        if (servo == null)
            return;

        servo.minAngle = Mathf.RoundToInt(value);

        if (servo.minAngle > servo.maxAngle)
        {
            servo.maxAngle = servo.minAngle;

            if (maxSlider != null)
                maxSlider.SetValueWithoutNotify(servo.maxAngle);
        }

        UpdateTexts();
    }

    private void OnMaxChanged(float value)
    {
        ServoControlUnit servo = TargetServo;
        if (servo == null)
            return;

        servo.maxAngle = Mathf.RoundToInt(value);

        if (servo.maxAngle < servo.minAngle)
        {
            servo.minAngle = servo.maxAngle;

            if (minSlider != null)
                minSlider.SetValueWithoutNotify(servo.minAngle);
        }

        UpdateTexts();
    }

    private void OnOffsetChanged(float value)
    {
        ServoControlUnit servo = TargetServo;
        if (servo == null)
            return;

        servo.offset = Mathf.RoundToInt(value);
        UpdateTexts();
    }

    private void OnInvertChanged(bool value)
    {
        ServoControlUnit servo = TargetServo;
        if (servo == null)
            return;

        servo.invert = value;
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        ServoControlUnit servo = TargetServo;
        if (servo == null)
            return;

        if (testAngleValueText != null && testAngleSlider != null)
            testAngleValueText.text = Mathf.RoundToInt(testAngleSlider.value).ToString();

        if (minValueText != null)
            minValueText.text = servo.minAngle.ToString();

        if (maxValueText != null)
            maxValueText.text = servo.maxAngle.ToString();

        if (offsetValueText != null)
            offsetValueText.text = servo.offset.ToString();
    }
}