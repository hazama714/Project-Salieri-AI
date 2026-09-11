// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using System.Collections;
using UnityEngine;

public abstract class BootCheckBase : MonoBehaviour
{
    [Header("Boot Check")]
    [SerializeField] private string displayName;
    [SerializeField] private bool required = true;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? GetType().Name : displayName;
    public bool Required => required;

    public bool IsRunning { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; }
    public float DurationSeconds { get; private set; }

    public IEnumerator Run()
    {
        IsRunning = true;
        IsCompleted = false;
        IsSuccess = false;
        Message = string.Empty;
        DurationSeconds = 0f;

        float startedAt = Time.realtimeSinceStartup;

        Debug.Log($"[BootCheck][START] {DisplayName} Required:{required}");

        IEnumerator routine = null;

        try
        {
            routine = RunCheck();
        }
        catch (Exception ex)
        {
            Fail("Exception before check start: " + ex.Message);
        }

        if (routine != null)
        {
            while (true)
            {
                object current = null;
                bool moveNext = false;

                try
                {
                    moveNext = routine.MoveNext();

                    if (moveNext)
                        current = routine.Current;
                }
                catch (Exception ex)
                {
                    Fail("Exception during check: " + ex.Message);
                    break;
                }

                if (!moveNext)
                    break;

                yield return current;
            }
        }

        DurationSeconds = Time.realtimeSinceStartup - startedAt;

        if (string.IsNullOrEmpty(Message))
            Message = IsSuccess ? "OK" : "No result message.";

        IsCompleted = true;
        IsRunning = false;

        string level = IsSuccess ? "OK" : (required ? "FAILED" : "WARNING");

        Debug.Log(
            $"[BootCheck][{level}] {DisplayName} / {Message} / {DurationSeconds:0.00}s"
        );
    }

    protected abstract IEnumerator RunCheck();

    protected void Pass(string message)
    {
        IsSuccess = true;
        Message = message;
    }

    protected void Fail(string message)
    {
        IsSuccess = false;
        Message = message;
    }
}
