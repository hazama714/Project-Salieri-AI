// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Core.Limbo
{
    public sealed class LimboPermission : MonoBehaviour
    {
        [Header("Runtime Permission")]
        [SerializeField] private bool canThink;

        // Serialized field name is retained for MainScene compatibility.
        // The formal runtime API is now orientation tracking.
        [SerializeField] private bool canTrackFace;

        [SerializeField] private bool canMoveServo;
        [SerializeField] private bool canStartAction;
        [SerializeField] private bool canSpeak;
        [SerializeField] private bool canSearch;
        [SerializeField] private bool canInterrupt;
        [SerializeField] private bool isEmergencyMode;

        public bool CanThink => canThink;

        /// <summary>
        /// Formal shared permission for face/object/neutral orientation output.
        /// </summary>
        public bool CanTrackOrientation => canTrackFace;

        /// <summary>
        /// Legacy compatibility alias.
        /// Existing face-related sources may continue using this property.
        /// </summary>
        public bool CanTrackFace => CanTrackOrientation;

        public bool CanMoveServo => canMoveServo;
        public bool CanStartAction => canStartAction;
        public bool CanSpeak => canSpeak;
        public bool CanSearch => canSearch;
        public bool CanInterrupt => canInterrupt;
        public bool IsEmergencyMode => isEmergencyMode;

        private void Awake()
        {
            LockAll();
        }

        public void LockAll()
        {
            canThink = false;
            canTrackFace = false;
            canMoveServo = false;
            canStartAction = false;
            canSpeak = false;
            canSearch = false;
            canInterrupt = false;
            isEmergencyMode = false;

            Debug.Log("[LimboPermission] LockAll");
        }

        public void AllowServo()
        {
            canMoveServo = true;
            Debug.Log("[LimboPermission] AllowServo");
        }

        /// <summary>
        /// Enables shared orientation tracking output.
        /// </summary>
        public void AllowOrientationTracking()
        {
            canTrackFace = true;
            Debug.Log("[LimboPermission] AllowOrientationTracking");
        }

        /// <summary>
        /// Disables shared orientation tracking output.
        /// </summary>
        public void DenyOrientationTracking()
        {
            canTrackFace = false;
            Debug.Log("[LimboPermission] DenyOrientationTracking");
        }

        /// <summary>
        /// Legacy compatibility wrapper.
        /// </summary>
        public void AllowFaceTracking()
        {
            AllowOrientationTracking();
        }

        /// <summary>
        /// Legacy compatibility wrapper.
        /// </summary>
        public void DenyFaceTracking()
        {
            DenyOrientationTracking();
        }

        public void AllowSpeak()
        {
            canSpeak = true;
            Debug.Log("[LimboPermission] AllowSpeak");
        }

        public void AllowThinking()
        {
            canThink = true;
            canStartAction = true;

            Debug.Log("[LimboPermission] AllowThinking / AllowAction");
        }

        public void AllowSearch()
        {
            canSearch = true;
            Debug.Log("[LimboPermission] AllowSearch");
        }

        public void AllowInterrupt()
        {
            canInterrupt = true;
            Debug.Log("[LimboPermission] AllowInterrupt");
        }

        public void EnterRuntime()
        {
            canThink = true;
            canTrackFace = true;
            canMoveServo = true;
            canStartAction = true;
            canSpeak = true;
            canSearch = true;
            canInterrupt = true;
            isEmergencyMode = false;

            Debug.Log("[LimboPermission] EnterRuntime");
        }

        public void EnterEmergencyMode()
        {
            isEmergencyMode = true;

            canThink = false;
            canTrackFace = false;
            canMoveServo = false;
            canStartAction = false;
            canSpeak = false;
            canSearch = false;
            canInterrupt = false;

            Debug.Log("[LimboPermission] EnterEmergencyMode");
        }

        public void ExitEmergencyMode()
        {
            isEmergencyMode = false;
            Debug.Log("[LimboPermission] ExitEmergencyMode");
        }
    }
}