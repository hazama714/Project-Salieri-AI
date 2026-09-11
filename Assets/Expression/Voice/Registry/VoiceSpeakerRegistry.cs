// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections.Generic;
using UnityEngine;

public class VoiceSpeakerRegistry
{
    private readonly List<VoiceModelInfo> models = new();

    public void Clear()
    {
        models.Clear();
    }

    public void AddModel(VoiceModelInfo modelInfo)
    {
        if (modelInfo == null)
            return;

        models.Add(modelInfo);
    }

    public bool TryGetStyleId(string speakerName, out int styleId)
    {
        styleId = -1;

        foreach (VoiceModelInfo model in models)
        {
            if (model?.speakers == null)
                continue;

            foreach (VoiceSpeakerInfo speaker in model.speakers)
            {
                if (speaker == null || speaker.name != speakerName)
                    continue;

                if (speaker.styles == null || speaker.styles.Length == 0)
                    return false;

                styleId = speaker.styles[0].id;
                return true;
            }
        }

        return false;
    }

    public bool TryGetStyleInfo(
        string speakerName,
        out int styleId,
        out string styleName,
        out string vvmPath
    )
    {
        styleId = -1;
        styleName = null;
        vvmPath = null;

        foreach (VoiceModelInfo model in models)
        {
            if (model?.speakers == null)
                continue;

            foreach (VoiceSpeakerInfo speaker in model.speakers)
            {
                if (speaker == null || speaker.name != speakerName)
                    continue;

                if (speaker.styles == null || speaker.styles.Length == 0)
                    return false;

                VoiceStyleInfo style = speaker.styles[0];
                styleId = style.id;
                styleName = style.name;
                vvmPath = model.filePath;
                return true;
            }
        }

        return false;
    }

    public bool TryGetStyleInfo(
        string speakerName,
        string targetStyleName,
        out int styleId,
        out string vvmPath
    )
    {
        styleId = -1;
        vvmPath = null;

        foreach (VoiceModelInfo model in models)
        {
            if (model?.speakers == null)
                continue;

            foreach (VoiceSpeakerInfo speaker in model.speakers)
            {
                if (speaker == null || speaker.name != speakerName)
                    continue;

                if (speaker.styles == null)
                    return false;

                foreach (VoiceStyleInfo style in speaker.styles)
                {
                    if (style == null || style.name != targetStyleName)
                        continue;

                    styleId = style.id;
                    vvmPath = model.filePath;
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryGetSpeakerAndStyleByStyleId(
        int styleId,
        out string speakerName,
        out string styleName,
        out string vvmPath
    )
    {
        speakerName = null;
        styleName = null;
        vvmPath = null;

        foreach (VoiceModelInfo model in models)
        {
            if (model?.speakers == null)
                continue;

            foreach (VoiceSpeakerInfo speaker in model.speakers)
            {
                if (speaker?.styles == null)
                    continue;

                foreach (VoiceStyleInfo style in speaker.styles)
                {
                    if (style == null || style.id != styleId)
                        continue;

                    speakerName = speaker.name;
                    styleName = style.name;
                    vvmPath = model.filePath;
                    return true;
                }
            }
        }

        return false;
    }

    public void LogAll()
    {
        foreach (VoiceModelInfo model in models)
        {
            Debug.Log($"[VoiceSpeakerRegistry] VVM: {model.fileName}");

            if (model.speakers == null)
                continue;

            foreach (VoiceSpeakerInfo speaker in model.speakers)
            {
                Debug.Log($"[VoiceSpeakerRegistry]  Speaker: {speaker.name}");

                if (speaker.styles == null)
                    continue;

                foreach (VoiceStyleInfo style in speaker.styles)
                {
                    Debug.Log($"[VoiceSpeakerRegistry]    Style: {style.name} ID:{style.id}");
                }
            }
        }
    }
}