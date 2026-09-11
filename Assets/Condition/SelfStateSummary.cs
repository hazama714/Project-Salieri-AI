// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;
using UnityEngine;

[Serializable]
public class SelfStateSummary
{
    // ============================================================
    // SelfStateSummary
    // ------------------------------------------------------------
    // RobotConditionCollector が収集した
    // 「現在の自分の状態」を保持する共通DTO。
    //
    // 重要：
    // このクラスは判断しない。
    // このクラスは行動を決めない。
    // このクラスは状態値を渡すだけ。
    //
    // Cloud / Local / Limbo / Autonomy が参照する。
    // ============================================================

    [Header("Device / Boot")]
    public bool cameraAvailable;
    public bool microphoneAvailable;
    public bool bluetoothReady;
    public bool llamaReady;
    public bool voicevoxReady;

    // 将来用。
    // センサーセルフチェックや環境反応に使用する。
    public bool lightSensorAvailable;
    public bool gyroAvailable;

    [Header("Power / Thermal")]
    public float batteryLevel;
    public BatteryStatus batteryStatus;
    public float maxTemperatureCelsius;

    [Header("Interaction State")]
    // 例：Idle / Listening / Thinking / Speaking / Acting / Booting
    public string interactionState = "Idle";

    // 互換用・将来整理候補。
    public string mode = "idle";

    [Header("Runtime Trigger")]
    // 今回なぜSelfStateSummaryを使って判断するのか。
    //
    // 例：
    // IdleTick
    // EventReaction
    // ActionResult
    // DeviceStateChanged
    //
    // UserSpeechは会話専用ルートで扱うため、
    // 当面このDTOを使わなくてもよい。
    public string triggerType = "None";

    // triggerTypeの具体的な内訳。
    //
    // 例：
    // FullyLost
    // FoundAfterSearch
    // ReFoundAfterGap
    // LostForLong
    public string triggerDetail = "";

    // EventReaction発生前の顔状態。
    //
    // 例：
    // StableFound
    // TemporaryLost
    // FullyLost
    public string previousFaceState = "";

    // 状態変化が成立するまでの経過時間。
    //
    // 例：
    // FullyLostから再発見まで8.4秒
    public float triggerElapsedSeconds;

    [Header("Perception")]
    // 現在この瞬間に顔が検出されているか。
    public bool faceDetected;

    // 最後に顔を検出してからの経過秒。
    public float secondsSinceLastFace;

    // 顔が継続して安定表示されている秒数。
    public float faceVisibleDuration;

    // 顔が安定して見えていない継続秒数。
    public float noFaceDuration;

    [Header("Activity")]
    public bool isSpeaking;
    public bool isActing;

    public float secondsSinceLastThink;
    public float secondsSinceLastAction;
    public float secondsSinceLastSpeech;

    // 例：none / lookAround / returnCenter / crawler_forward_short
    public string lastAction = "none";

    [Header("Limbo Permission")]
    // LLMが変更する値ではない。
    // Promptでは「今できること」の説明材料として使う。
    public bool canThink;
    public bool canSpeak;
    public bool canMoveServo;
    public bool canStartAction;
    public bool canTrackFace;
    public bool canInterrupt;

    [Header("Memo")]
    // デバッグ用。
    // 重要な分岐はnoteだけに依存させない。
    [TextArea(2, 5)]
    public string note;

    public string ToPromptJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    public string ToShortPromptText()
    {
        return
            $"state={interactionState}, " +
            $"trigger={triggerType}, " +
            $"detail={triggerDetail}, " +
            $"previousFace={previousFaceState}, " +
            $"triggerElapsed={triggerElapsedSeconds:F1}s, " +
            $"face={faceDetected}, " +
            $"lastFace={secondsSinceLastFace:F1}s, " +
            $"faceVisible={faceVisibleDuration:F1}s, " +
            $"noFace={noFaceDuration:F1}s, " +
            $"lastAction={secondsSinceLastAction:F1}s, " +
            $"lastSpeech={secondsSinceLastSpeech:F1}s, " +
            $"speaking={isSpeaking}, " +
            $"acting={isActing}, " +
            $"canThink={canThink}, " +
            $"canMove={canMoveServo}, " +
            $"canSpeak={canSpeak}, " +
            $"battery={batteryLevel:F2}, " +
            $"temp={maxTemperatureCelsius:F1}, " +
            $"lastActionId={lastAction}";
    }
}