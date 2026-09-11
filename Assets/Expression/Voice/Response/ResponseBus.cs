// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

// v2025-09-02-ReleaseBus-01 (2025-09-02 22:40 JST)
// コメント: UI応答を全域に配信する単純な静的バス。既存Hubの有無に関わらずTTSへ通知を飛ばす。

using System;

public static class ResponseBus
{
    /// <summary>UIに応答テキストが表示されたら流されるイベント。</summary>
    public static event Action<string> OnResponse;

    /// <summary>
    /// Voice等の実行側へ、後方互換の文字列と明示相関情報を渡すイベント。
    /// legacy callerはUncorrelated envelopeとして同じ経路を通る。
    /// </summary>
    public static event Action<ResponseEnvelope> OnResponseEnvelope;

    /// <summary>発火（空文字は無視）</summary>
    public static void Raise(string text)
    {
        RaiseCore(ResponseEnvelope.Uncorrelated(text));
    }

    /// <summary>
    /// Correlated responseを発火する。戻り値はEnvelope consumerへ少なくとも
    /// 1回正常配送できたかを表し、文字列observerの有無とは独立する。
    /// </summary>
    public static bool Raise(ResponseEnvelope envelope)
    {
        return RaiseCore(envelope);
    }

    private static bool RaiseCore(ResponseEnvelope envelope)
    {
        if (envelope == null || string.IsNullOrEmpty(envelope.Text))
            return false;

        InvokeLegacyObservers(envelope.Text);
        return InvokeEnvelopeObservers(envelope);
    }

    private static void InvokeLegacyObservers(string text)
    {
        Action<string> handlers = OnResponse;
        if (handlers == null)
            return;

        Delegate[] invocationList = handlers.GetInvocationList();
        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<string>)invocationList[i]).Invoke(text);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    $"[ResponseBus] string observer exception: {e.Message}");
            }
        }
    }

    private static bool InvokeEnvelopeObservers(ResponseEnvelope envelope)
    {
        Action<ResponseEnvelope> handlers = OnResponseEnvelope;
        if (handlers == null)
            return false;

        bool delivered = false;
        Delegate[] invocationList = handlers.GetInvocationList();
        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<ResponseEnvelope>)invocationList[i]).Invoke(envelope);
                delivered = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    $"[ResponseBus] envelope observer exception: {e.Message}");
            }
        }
        return delivered;
    }
}
