// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using SalieriAI.Core.Experience.Storage;
using SalieriAI.Core.Perception.ObjectTargeting;

using UnityEngine;

namespace SalieriAI.Core.Experience.Observation
{
    /// <summary>
    /// Narrow source-only composition for M5-A. It creates a scene-lifetime
    /// host at runtime and never modifies or saves the Scene asset.
    /// </summary>
    public static class ObservedObjectMemoryContextRuntimeBootstrap
    {
        private const string HostName =
            "ObservedObjectMemoryContextRuntime_M5A";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindObjectOfType<
                    ObservedObjectMemoryContextRuntimeHost>() != null)
            {
                return;
            }

            ObservationTargetSelectionService[] sources =
                UnityEngine.Object.FindObjectsOfType<
                    ObservationTargetSelectionService>();
            if (sources == null || sources.Length == 0)
            {
                Debug.Log(
                    "[ObservedObjectMemoryContext][BOOTSTRAP] " +
                    "ObservationTargetSelectionService is not present; skipped.");
                return;
            }
            if (sources.Length != 1)
            {
                Debug.LogError(
                    "[ObservedObjectMemoryContext][BOOTSTRAP_ERROR] " +
                    "Expected exactly one ObservationTargetSelectionService. " +
                    "Count=" + sources.Length);
                return;
            }

            var root = new GameObject(HostName);
            root.SetActive(false);
            ObservedObjectMemoryContextRuntimeHost host =
                root.AddComponent<ObservedObjectMemoryContextRuntimeHost>();
            host.Configure(
                sources[0],
                new JsonFileExperienceStore(
                    ExperienceProductionStorePath.Resolve()));
            root.SetActive(true);

            Debug.Log(
                "[ObservedObjectMemoryContext][BOOTSTRAP] Ready. " +
                "Trigger=TargetChanged Store=" +
                ExperienceProductionStorePath.Resolve());
        }
    }
}
