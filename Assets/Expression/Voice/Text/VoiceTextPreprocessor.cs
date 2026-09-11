// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System.Collections.Generic;
using UnityEngine;

public class VoiceTextPreprocessor : MonoBehaviour
{
    [Header("Ruby Dictionary")]
    [SerializeField]
    private List<RubyEntry> rubyEntries = new();

    private Dictionary<string, string> rubyDictionary;

    private void Awake()
    {
        BuildDictionary();
    }

    private void BuildDictionary()
    {
        rubyDictionary = new Dictionary<string, string>();

        foreach (RubyEntry entry in rubyEntries)
        {
            if (entry == null)
                continue;

            if (string.IsNullOrEmpty(entry.original))
                continue;

            rubyDictionary[entry.original] = entry.reading;
        }
    }

    public string Process(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string result = text;

        result = NormalizeSpaces(result);
        result = ApplyRuby(result);
        result = NormalizeLineBreaks(result);

        return result;
    }

    private string NormalizeSpaces(string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();
    }

    private string NormalizeLineBreaks(string text)
    {
        while (text.Contains("\n\n\n"))
        {
            text = text.Replace("\n\n\n", "\n\n");
        }

        return text;
    }

    private string ApplyRuby(string text)
    {
        if (rubyDictionary == null)
            return text;

        foreach (KeyValuePair<string, string> pair in rubyDictionary)
        {
            text = text.Replace(pair.Key, pair.Value);
        }

        return text;
    }
}