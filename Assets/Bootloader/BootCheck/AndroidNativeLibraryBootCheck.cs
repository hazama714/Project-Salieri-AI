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

public class AndroidNativeLibraryBootCheck : BootCheckBase
{
    [Header("Native Library")]
    [SerializeField] private string libraryNameWithoutPrefix = "llama_unity_shim";

    protected override IEnumerator RunCheck()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (string.IsNullOrEmpty(libraryNameWithoutPrefix))
        {
            Fail("library name is empty.");
            yield break;
        }

        try
        {
            using (AndroidJavaClass system = new AndroidJavaClass("java.lang.System"))
            {
                system.CallStatic("loadLibrary", libraryNameWithoutPrefix);
            }

            Pass("Loaded: lib" + libraryNameWithoutPrefix + ".so");
        }
        catch (Exception ex)
        {
            Fail("Load failed: lib" + libraryNameWithoutPrefix + ".so / " + ex.Message);
        }
#else
        Pass("Skipped native load outside Android runtime: " + libraryNameWithoutPrefix);
#endif
        yield return null;
    }
}
