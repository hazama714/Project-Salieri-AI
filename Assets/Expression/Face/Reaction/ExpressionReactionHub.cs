// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections;
using UnityEngine;

using SalieriAI.Core.Runtime;

/// <summary>
/// Runtime側で決定された ExpressionIntent を、
/// モデル非依存の表情名・強度・遷移へ変換する。
///
/// このクラスは VRM0 / FBX などのモデル固有APIを知らない。
/// 最終変換は IExpressionController 実装側に委譲する。
///
/// 表情は短いPulseではなく、次の要求まで保持する。
/// 一定時間、新しい表情要求がない場合だけNeutralへ戻す。
/// </summary>
public sealed class ExpressionReactionHub : MonoBehaviour
{
    [Header("Expression Controller")]
    [Tooltip("VRM0ExpressionController または FBXExpressionController を設定する。")]
    [SerializeField]
    private MonoBehaviour expressionControllerBehaviour;

    [Header("Unified Expression Names")]
    [SerializeField]
    private string joyExpressionName = "Joy";

    [SerializeField]
    private string attentiveExpressionName = "Fun";

    [SerializeField]
    private string concernedExpressionName = "Sorrow";

    [SerializeField]
    private string relievedExpressionName = "Joy";

    [SerializeField]
    private string neutralExpressionName = "Neutral";

    [Header("Intent Strength")]
    [SerializeField]
    private float joySoftStrength = 0.80f;

    [SerializeField]
    private float joyStrength = 1.00f;

    [SerializeField]
    private float attentiveStrength = 0.60f;

    [SerializeField]
    private float concernedStrength = 0.90f;

    [SerializeField]
    private float relievedStrength = 0.80f;

    [Header("Transition Timing")]
    [Tooltip("現在表情から次の表情へ移る時間。")]
    [SerializeField]
    private float transitionSeconds = 0.55f;

    [Tooltip("新しい表情要求がない場合にNeutralへ戻り始めるまでの時間。")]
    [SerializeField]
    private float idleReturnSeconds = 8.00f;

    [Tooltip("Neutralへ戻る時の遷移時間。")]
    [SerializeField]
    private float neutralReturnSeconds = 1.20f;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    [SerializeField]
    private bool verboseWeightLog = false;

    private IExpressionController expressionController;
    private Coroutine reactionCoroutine;

    private string activeExpressionName = string.Empty;
    private float activeExpressionStrength;

    private void Awake()
    {
        expressionController =
            expressionControllerBehaviour as IExpressionController;

        if (expressionController == null)
        {
            Debug.LogWarning(
                "[ExpressionReactionHub] " +
                "expressionControllerBehaviour does not implement " +
                "IExpressionController."
            );
        }

        SetImmediateNeutral();
    }

    /// <summary>
    /// RuntimeExpressionController から呼ばれる共通入口。
    ///
    /// FaceEvent専用ではなく、
    /// 会話・行動・Persona由来の表情要求もここへ集約する。
    /// </summary>
    public void PlayIntent(
        ExpressionIntent intent,
        string source,
        string reason
    )
    {
        if (verboseLog)
        {
            Debug.Log(
                "[ExpressionReactionHub] PlayIntent " +
                "intent=" +
                intent +
                " source=" +
                Safe(source) +
                " reason=" +
                Safe(reason)
            );
        }

        // Keep / None は現在表情を維持する。
        // Neutral復帰タイマーも止めない。
        if (intent == ExpressionIntent.Keep ||
            intent == ExpressionIntent.None)
        {
            return;
        }

        if (reactionCoroutine != null)
        {
            StopCoroutine(reactionCoroutine);
            reactionCoroutine = null;
        }

        switch (intent)
        {
            case ExpressionIntent.Neutral:
                reactionCoroutine =
                    StartCoroutine(
                        TransitionToNeutralRoutine(
                            neutralReturnSeconds
                        )
                    );
                break;

            case ExpressionIntent.JoySoft:
                reactionCoroutine =
                    StartCoroutine(
                        TransitionAndHoldRoutine(
                            joyExpressionName,
                            joySoftStrength
                        )
                    );
                break;

            case ExpressionIntent.Joy:
                reactionCoroutine =
                    StartCoroutine(
                        TransitionAndHoldRoutine(
                            joyExpressionName,
                            joyStrength
                        )
                    );
                break;

            case ExpressionIntent.Attentive:
                reactionCoroutine =
                    StartCoroutine(
                        TransitionAndHoldRoutine(
                            attentiveExpressionName,
                            attentiveStrength
                        )
                    );
                break;

            case ExpressionIntent.Concerned:
                reactionCoroutine =
                    StartCoroutine(
                        TransitionAndHoldRoutine(
                            concernedExpressionName,
                            concernedStrength
                        )
                    );
                break;

            case ExpressionIntent.Relieved:
                reactionCoroutine =
                    StartCoroutine(
                        TransitionAndHoldRoutine(
                            relievedExpressionName,
                            relievedStrength
                        )
                    );
                break;
        }
    }

    // Legacy methods.
    // 旧FaceExpressionTrigger直結用。
    // 今後はRuntimeExpressionController経由へ移行する。
    public void OnFaceFound()
    {
        Debug.Log(
            "[ExpressionReactionHub] OnFaceFound legacy"
        );

        PlayIntent(
            ExpressionIntent.JoySoft,
            "LegacyFaceFound",
            "OnFaceFound"
        );
    }

    public void OnFaceLost()
    {
        Debug.Log(
            "[ExpressionReactionHub] OnFaceLost legacy"
        );

        PlayIntent(
            ExpressionIntent.Neutral,
            "LegacyFaceLost",
            "OnFaceLost"
        );
    }

    /// <summary>
    /// 新しい表情へ滑らかに遷移し、
    /// 次の表情要求またはIdleタイムアウトまで保持する。
    /// </summary>
    private IEnumerator TransitionAndHoldRoutine(
        string nextExpressionName,
        float nextStrength
    )
    {
        nextStrength =
            Mathf.Clamp01(
                nextStrength
            );

        yield return TransitionExpression(
            nextExpressionName,
            nextStrength,
            transitionSeconds
        );

        if (verboseLog)
        {
            Debug.Log(
                "[ExpressionReactionHub] Expression hold started. " +
                "expression=" +
                Safe(activeExpressionName) +
                " strength=" +
                activeExpressionStrength +
                " idleReturnSeconds=" +
                idleReturnSeconds
            );
        }

        if (idleReturnSeconds > 0f)
        {
            yield return new WaitForSeconds(
                idleReturnSeconds
            );

            yield return TransitionToNeutralRoutine(
                neutralReturnSeconds
            );

            yield break;
        }

        reactionCoroutine = null;
    }

    /// <summary>
    /// 現在表情からNeutralへ滑らかに戻す。
    /// </summary>
    private IEnumerator TransitionToNeutralRoutine(
        float seconds
    )
    {
        if (string.Equals(
            activeExpressionName,
            neutralExpressionName,
            System.StringComparison.OrdinalIgnoreCase
        ))
        {
            SetImmediateNeutral();
            reactionCoroutine = null;

            yield break;
        }

        yield return TransitionExpression(
            neutralExpressionName,
            1f,
            seconds
        );

        if (verboseLog)
        {
            Debug.Log(
                "[ExpressionReactionHub] Returned to Neutral."
            );
        }

        reactionCoroutine = null;
    }

    /// <summary>
    /// 現在表情から次の表情へクロスフェードする。
    ///
    /// 途中で別表情要求が来た場合でも、
    /// 現在値を起点に再遷移できる。
    /// </summary>
    private IEnumerator TransitionExpression(
        string nextExpressionName,
        float nextStrength,
        float seconds
    )
    {
        string previousExpressionName =
            string.IsNullOrWhiteSpace(
                activeExpressionName
            )
                ? neutralExpressionName
                : activeExpressionName;

        float previousStrength =
            activeExpressionStrength;

        if (string.Equals(
            previousExpressionName,
            nextExpressionName,
            System.StringComparison.OrdinalIgnoreCase
        ))
        {
            yield return FadeSingleExpression(
                nextExpressionName,
                previousStrength,
                nextStrength,
                seconds
            );

            SnapToSingleExpression(
                nextExpressionName,
                nextStrength
            );

            yield break;
        }

        PrepareSingleActiveExpression(
            previousExpressionName,
            previousStrength
        );

        float timer = 0f;

        while (timer < seconds)
        {
            timer += Time.deltaTime;

            float t =
                seconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        timer / seconds
                    );

            float smooth =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            float previousValue =
                Mathf.Lerp(
                    previousStrength,
                    0f,
                    smooth
                );

            float nextValue =
                Mathf.Lerp(
                    0f,
                    nextStrength,
                    smooth
                );

            SetExpressionWeight(
                previousExpressionName,
                previousValue
            );

            SetExpressionWeight(
                nextExpressionName,
                nextValue
            );

            activeExpressionName =
                nextExpressionName;

            activeExpressionStrength =
                nextValue;

            yield return null;
        }

        SnapToSingleExpression(
            nextExpressionName,
            nextStrength
        );
    }

    private IEnumerator FadeSingleExpression(
        string expressionName,
        float from,
        float to,
        float seconds
    )
    {
        float timer = 0f;

        while (timer < seconds)
        {
            timer += Time.deltaTime;

            float t =
                seconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        timer / seconds
                    );

            float value =
                Mathf.Lerp(
                    from,
                    to,
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t
                    )
                );

            SetExpressionWeight(
                expressionName,
                value
            );

            activeExpressionName =
                expressionName;

            activeExpressionStrength =
                value;

            yield return null;
        }

        SetExpressionWeight(
            expressionName,
            to
        );

        activeExpressionName =
            expressionName;

        activeExpressionStrength =
            to;
    }

    /// <summary>
    /// 途中で前のCoroutineを止めた場合に、
    /// 残留BlendShapeを消して現在表情だけを再構成する。
    /// </summary>
    private void PrepareSingleActiveExpression(
        string expressionName,
        float strength
    )
    {
        ResetAllExpressions();

        if (!string.Equals(
            expressionName,
            neutralExpressionName,
            System.StringComparison.OrdinalIgnoreCase
        ))
        {
            SetExpressionWeight(
                neutralExpressionName,
                0f
            );
        }

        SetExpressionWeight(
            expressionName,
            strength
        );
    }

    /// <summary>
    /// 遷移完了時にBlendShape残留を除去し、
    /// 最終表情だけを残す。
    /// </summary>
    private void SnapToSingleExpression(
        string expressionName,
        float strength
    )
    {
        ResetAllExpressions();

        if (!string.Equals(
            expressionName,
            neutralExpressionName,
            System.StringComparison.OrdinalIgnoreCase
        ))
        {
            SetExpressionWeight(
                neutralExpressionName,
                0f
            );
        }

        SetExpressionWeight(
            expressionName,
            strength
        );

        activeExpressionName =
            expressionName;

        activeExpressionStrength =
            strength;
    }

    private void SetImmediateNeutral()
    {
        ResetAllExpressions();

        SetExpressionWeight(
            neutralExpressionName,
            1f
        );

        activeExpressionName =
            neutralExpressionName;

        activeExpressionStrength =
            1f;
    }

    private void SetExpressionWeight(
        string expressionName,
        float strength
    )
    {
        if (expressionController == null)
        {
            Debug.LogWarning(
                "[ExpressionReactionHub] expressionController is null"
            );

            return;
        }

        strength =
            Mathf.Clamp01(
                strength
            );

        expressionController.SetExpressionWeight(
            expressionName,
            strength
        );

        if (verboseWeightLog)
        {
            Debug.Log(
                "[ExpressionReactionHub] SetExpressionWeight " +
                "expression=" +
                Safe(expressionName) +
                " weight=" +
                strength
            );
        }
    }

    private void ResetAllExpressions()
    {
        if (expressionController == null)
            return;

        expressionController.ResetExpression();
    }

    private static string Safe(
        string value
    )
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}