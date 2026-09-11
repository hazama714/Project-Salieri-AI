// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections.Generic;

using UnityEngine;

using SalieriAI.Core.Language.Contracts;

namespace SalieriAI.Core.Input
{
    /// <summary>
    /// Runtime へ渡す外部入力イベントのバッファ。
    ///
    /// Phase 10-D:
    /// UserSpeech の source を保持できる Push を追加。
    /// 既存互換のため、旧 Push も維持する。
    /// </summary>
    public class ExternalInputBuffer : MonoBehaviour
    {
        private readonly Queue<ExternalInputEvent> queue =
            new Queue<ExternalInputEvent>();

        [SerializeField]
        private int maxQueueSize = 16;

        public bool HasInput => queue.Count > 0;

        /// <summary>
        /// 既存互換入口。
        /// source は Unknown として扱う。
        /// </summary>
        public void Push(
            ExternalInputType type,
            ExternalInputPriority priority,
            string payload
        )
        {
            Push(
                type,
                priority,
                payload,
                "Unknown"
            );
        }

        /// <summary>
        /// Phase 10-D:
        /// source 付き入力イベントを積む。
        /// </summary>
        public void Push(
            ExternalInputType type,
            ExternalInputPriority priority,
            string payload,
            string source
        )
        {
            var inputEvent = new ExternalInputEvent(
                type,
                priority,
                payload,
                Time.time,
                source
            );

            Enqueue(inputEvent);
        }

        public void Push(
            ExternalInputType type,
            ExternalInputPriority priority,
            CommunicationInput communicationInput
        )
        {
            if (communicationInput == null)
            {
                Debug.LogWarning(
                    "[ExternalInputBuffer] Typed input ignored: communicationInput is null."
                );
                return;
            }

            var inputEvent = new ExternalInputEvent(
                type,
                priority,
                communicationInput,
                Time.time
            );

            Enqueue(inputEvent);
        }

        private void Enqueue(ExternalInputEvent inputEvent)
        {
            if (queue.Count >= maxQueueSize)
            {
                ExternalInputEvent dropped = queue.Dequeue();

                Debug.LogWarning(
                    "[ExternalInputBuffer] Queue full. Dropped oldest event: " +
                    dropped.Type +
                    " source=" +
                    SafeLog(dropped.Source) +
                    " payload=" +
                    SafeLog(dropped.Payload)
                );
            }

            queue.Enqueue(inputEvent);

            Debug.Log(
                "[ExternalInputBuffer] Push: " +
                inputEvent.Type +
                " priority=" +
                inputEvent.Priority +
                " source=" +
                SafeLog(inputEvent.Source) +
                " inputId=" +
                SafeLog(
                    inputEvent.CommunicationInput != null
                        ? inputEvent.CommunicationInput.InputId
                        : string.Empty
                ) +
                " interactionId=" +
                SafeLog(
                    inputEvent.CommunicationInput != null
                        ? inputEvent.CommunicationInput.InteractionId
                        : string.Empty
                ) +
                " payload=" +
                SafeLog(inputEvent.Payload)
            );
        }

        public bool TryConsume(out ExternalInputEvent inputEvent)
        {
            if (queue.Count <= 0)
            {
                inputEvent = null;
                return false;
            }

            inputEvent = queue.Dequeue();

            Debug.Log(
                "[ExternalInputBuffer] Consume: " +
                inputEvent.Type +
                " source=" +
                SafeLog(inputEvent.Source) +
                " inputId=" +
                SafeLog(
                    inputEvent.CommunicationInput != null
                        ? inputEvent.CommunicationInput.InputId
                        : string.Empty
                ) +
                " interactionId=" +
                SafeLog(
                    inputEvent.CommunicationInput != null
                        ? inputEvent.CommunicationInput.InteractionId
                        : string.Empty
                ) +
                " payload=" +
                SafeLog(inputEvent.Payload)
            );

            return true;
        }

        public void Clear()
        {
            queue.Clear();
            Debug.Log("[ExternalInputBuffer] Clear");
        }

        private static string SafeLog(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "<empty>"
                : value;
        }
    }
}
