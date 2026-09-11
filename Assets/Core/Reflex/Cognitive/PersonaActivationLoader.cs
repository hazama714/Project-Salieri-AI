// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Core.Reflex.Cognitive
{
    public static class PersonaActivationLoader
    {
        public static PersonaActivationProfile LoadOrDefault(
            TextAsset jsonAsset,
            string fallbackDisplayName,
            string fallbackAck
        )
        {
            PersonaActivationProfile profile = null;

            if (jsonAsset != null && !string.IsNullOrWhiteSpace(jsonAsset.text))
            {
                try
                {
                    profile = JsonUtility.FromJson<PersonaActivationProfile>(
                        jsonAsset.text
                    );
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning(
                        "[PersonaActivationLoader] Failed to parse activation json. " +
                        ex.Message
                    );
                }
            }

            if (profile == null)
            {
                profile = new PersonaActivationProfile();
            }

            if (string.IsNullOrWhiteSpace(profile.displayName))
            {
                profile.displayName = string.IsNullOrWhiteSpace(fallbackDisplayName)
                    ? "アシス"
                    : fallbackDisplayName.Trim();
            }

            if (string.IsNullOrWhiteSpace(profile.activationAck))
            {
                profile.activationAck = string.IsNullOrWhiteSpace(fallbackAck)
                    ? "はい"
                    : fallbackAck.Trim();
            }

            return profile;
        }
    }
}
