// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

public class VoiceModelManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool registerDebugSpeakerOnStart = true;

    [TextArea(5, 20)]
    [SerializeField] private string testMetasJson;

    [SerializeField] private string testFileName = "test.vvm";
    [SerializeField] private string testFilePath = "models/test.vvm";

    private VoiceSpeakerRegistry registry;

    public VoiceSpeakerRegistry Registry => registry;

    private void Awake()
    {
        registry = new VoiceSpeakerRegistry();
    }

    private void Start()
    {
        if (registerDebugSpeakerOnStart)
        {
            RegisterDebugSpeaker();
        }
    }

    private void RegisterDebugSpeaker()
    {
        VoiceStyleInfo style = new VoiceStyleInfo();
        style.name = "ノーマル";
        style.id = 14;

        VoiceSpeakerInfo speaker = new VoiceSpeakerInfo();
        speaker.name = "冥鳴ひまり";
        speaker.styles = new[] { style };

        VoiceModelInfo modelInfo = new VoiceModelInfo(
            "1.vvm",
            "models/1.vvm",
            new[] { speaker }
        );

        registry.AddModel(modelInfo);

        Debug.Log("[VoiceModelManager] Debug speaker registered: 冥鳴ひまり / 14 / 1.vvm");
    }

    [ContextMenu("Clear Registry")]
    public void ClearRegistry()
    {
        registry.Clear();
        Debug.Log("[VoiceModelManager] Registry cleared");
    }

    [ContextMenu("Register Test Metas")]
    public void RegisterTestMetas()
    {
        RegisterModelFromMetas(
            testFileName,
            testFilePath,
            testMetasJson
        );
    }

    public bool RegisterModelFromMetas(
        string fileName,
        string filePath,
        string metasJson
    )
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("[VoiceModelManager] fileName is empty");
            return false;
        }

        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[VoiceModelManager] filePath is empty");
            return false;
        }

        if (string.IsNullOrEmpty(metasJson))
        {
            Debug.LogError($"[VoiceModelManager] metasJson is empty: {fileName}");
            return false;
        }

        string wrappedJson = "{ \"speakers\": " + metasJson + "}";

        VoiceSpeakerListWrapper wrapper =
            JsonUtility.FromJson<VoiceSpeakerListWrapper>(wrappedJson);

        if (wrapper == null || wrapper.speakers == null)
        {
            Debug.LogError($"[VoiceModelManager] metasJson parse failed: {fileName}");
            return false;
        }

        VoiceModelInfo modelInfo = new VoiceModelInfo(
            fileName,
            filePath,
            wrapper.speakers
        );

        registry.AddModel(modelInfo);

        Debug.Log(
            $"[VoiceModelManager] Registered VVM: {fileName} SpeakerCount:{wrapper.speakers.Length}"
        );

        return true;
    }

    [ContextMenu("Log Registry")]
    public void LogRegistry()
    {
        registry.LogAll();
    }

    [ContextMenu("Debug StyleId 14")]
    public void DebugStyleId14()
    {
        DebugStyleId(14);
    }

    [ContextMenu("Debug StyleId 1")]
    public void DebugStyleId1()
    {
        DebugStyleId(1);
    }

    private void DebugStyleId(int styleId)
    {
        if (registry.TryGetSpeakerAndStyleByStyleId(
                styleId,
                out string speakerName,
                out string styleName,
                out string vvmPath
            ))
        {
            Debug.Log(
                $"[VoiceModelManager] StyleId:{styleId} = Speaker:{speakerName} / Style:{styleName} / VVM:{vvmPath}"
            );
        }
        else
        {
            Debug.LogWarning(
                $"[VoiceModelManager] StyleId:{styleId} was not found"
            );
        }
    }
}