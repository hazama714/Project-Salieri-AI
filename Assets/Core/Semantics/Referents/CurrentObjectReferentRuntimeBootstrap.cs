// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Core.Semantics.Referents
{
    /// <summary>
    /// Source-only scene-lifetime composition. Start performs the provider
    /// binding after all AfterSceneLoad bootstrap methods have completed.
    /// </summary>
    public static class CurrentObjectReferentRuntimeBootstrap
    {
        private const string HostName =
            "CurrentObjectReferentRuntime_M5B";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindObjectOfType<
                    CurrentObjectReferentRuntimeHost>() != null)
            {
                return;
            }

            var root = new GameObject(HostName);
            root.AddComponent<CurrentObjectReferentRuntimeHost>();

            Debug.Log(
                "[CurrentObjectReferent][BOOTSTRAP] Host installed. " +
                "Input=IObservedObjectMemoryContextProvider");
        }
    }
}
