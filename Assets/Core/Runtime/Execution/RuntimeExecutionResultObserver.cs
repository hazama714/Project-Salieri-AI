// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

namespace SalieriAI.Core.Runtime
{
    /// <summary>
    /// ExecutionController.ExecutionFinished の監視専用Observer。
    ///
    /// 責任:
    /// - ExecutionControllerの完了通知を購読する
    /// - ExecutionResultをStatusごとに分類してログへ出す
    ///
    /// 現段階ではobserve-only。
    ///
    /// 禁止:
    /// - Stateを変更しない
    /// - 次のActionを起動しない
    /// - 再試行しない
    /// - SafeStateへ遷移しない
    /// - BodyActionExecutorや各デバイスを直接呼ばない
    /// </summary>
    public sealed class RuntimeExecutionResultObserver : MonoBehaviour
    {
        [Header("Execution Reference")]
        [SerializeField]
        private ExecutionController executionController;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLog = true;

        private bool subscribed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void Start()
        {
            // Script Execution Orderや初期化順が変わった場合の保険。
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void ResolveReferences()
        {
            if (executionController == null)
            {
                executionController =
                    FindObjectOfType<ExecutionController>();
            }
        }

        private void Subscribe()
        {
            if (executionController == null)
            {
                if (verboseLog)
                {
                    Debug.Log(
                        "[RuntimeExecutionResultObserver] " +
                        "ExecutionController subscribe skipped: not assigned."
                    );
                }

                return;
            }

            if (subscribed)
            {
                return;
            }

            executionController.ExecutionFinished +=
                OnExecutionFinished;

            subscribed = true;

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeExecutionResultObserver] " +
                    "ExecutionController subscribed: ExecutionFinished"
                );
            }
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (executionController != null)
            {
                executionController.ExecutionFinished -=
                    OnExecutionFinished;
            }

            subscribed = false;

            if (verboseLog)
            {
                Debug.Log(
                    "[RuntimeExecutionResultObserver] " +
                    "ExecutionController unsubscribed: ExecutionFinished"
                );
            }
        }

        private void OnExecutionFinished(
            ExecutionResult result
        )
        {
            if (result == null)
            {
                Debug.LogWarning(
                    "[RuntimeExecutionResultObserver] " +
                    "Execution result route: null result received. " +
                    "action=Unknown status=Unknown route=ignore"
                );

                return;
            }

            LogExecutionResultRoute(result);
        }

        /// <summary>
        /// ExecutionResultを分類してログへ出す。
        ///
        /// 状態変更、次Action投入、再処理、割り込み処理は行わない。
        /// </summary>
        private void LogExecutionResultRoute(
            ExecutionResult result
        )
        {
            if (result == null)
            {
                return;
            }

            switch (result.Status)
            {
                case ExecutionStatus.Completed:
                    LogExecutionCompleted(result);
                    break;

                case ExecutionStatus.Rejected:
                    LogExecutionRejected(result);
                    break;

                case ExecutionStatus.Failed:
                    LogExecutionFailed(result);
                    break;

                case ExecutionStatus.Cancelled:
                    LogExecutionCancelled(result);
                    break;

                default:
                    LogExecutionOtherStatus(result);
                    break;
            }
        }

        private void LogExecutionCompleted(
            ExecutionResult result
        )
        {
            if (!verboseLog)
            {
                return;
            }

            Debug.Log(
                "[RuntimeExecutionResultObserver] " +
                "Execution result route: completed " +
                "action=" +
                SafeLogValue(result.ActionId) +
                " status=" +
                result.Status +
                " requestId=" +
                SafeLogValue(result.RequestId) +
                " route=observe-only/no-state-change " +
                "message=" +
                SafeLogValue(result.Message)
            );
        }

        private void LogExecutionRejected(
            ExecutionResult result
        )
        {
            Debug.LogWarning(
                "[RuntimeExecutionResultObserver] " +
                "Execution result route: rejected " +
                "action=" +
                SafeLogValue(result.ActionId) +
                " status=" +
                result.Status +
                " requestId=" +
                SafeLogValue(result.RequestId) +
                " route=observe-only/no-retry " +
                "message=" +
                SafeLogValue(result.Message)
            );
        }

        private void LogExecutionFailed(
            ExecutionResult result
        )
        {
            Debug.LogWarning(
                "[RuntimeExecutionResultObserver] " +
                "Execution result route: failed " +
                "action=" +
                SafeLogValue(result.ActionId) +
                " status=" +
                result.Status +
                " requestId=" +
                SafeLogValue(result.RequestId) +
                " route=observe-only/no-recovery-yet " +
                "message=" +
                SafeLogValue(result.Message)
            );
        }

        private void LogExecutionCancelled(
            ExecutionResult result
        )
        {
            if (!verboseLog)
            {
                return;
            }

            Debug.Log(
                "[RuntimeExecutionResultObserver] " +
                "Execution result route: cancelled " +
                "action=" +
                SafeLogValue(result.ActionId) +
                " status=" +
                result.Status +
                " requestId=" +
                SafeLogValue(result.RequestId) +
                " route=observe-only/no-reprocess-yet " +
                "message=" +
                SafeLogValue(result.Message)
            );
        }

        private void LogExecutionOtherStatus(
            ExecutionResult result
        )
        {
            if (!verboseLog)
            {
                return;
            }

            Debug.Log(
                "[RuntimeExecutionResultObserver] " +
                "Execution result route: other " +
                "action=" +
                SafeLogValue(result.ActionId) +
                " status=" +
                result.Status +
                " requestId=" +
                SafeLogValue(result.RequestId) +
                " route=observe-only/no-state-change " +
                "message=" +
                SafeLogValue(result.Message)
            );
        }

        private static string SafeLogValue(
            string value
        )
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value;
        }
    }
}