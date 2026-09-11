// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;

public sealed class CrawlerActionExecutor : MonoBehaviour
{
    private const string ActionCrawlerStop = "crawler_stop";
    private const string ActionCrawlerForwardShort = "crawler_forward_short";
    private const string ActionCrawlerBackShort = "crawler_back_short";
    private const string ActionCrawlerTurnLeftShort = "crawler_turn_left_short";
    private const string ActionCrawlerTurnRightShort = "crawler_turn_right_short";

    [Header("Target")]
    [SerializeField] private CrawlerController crawlerController;

    private void Awake()
    {
        if (crawlerController == null)
            crawlerController = FindObjectOfType<CrawlerController>();
    }

    public bool TryExecute(string action, string reason = "", string source = "CrawlerAction")
    {
        action = NormalizeActionName(action);

        Debug.Log(
            $"[CrawlerActionExecutor] TryExecute action={action} " +
            $"source={source} reason={reason}"
        );

        if (crawlerController == null)
        {
            Debug.LogWarning($"[CrawlerActionExecutor] crawlerController is null action={action}");
            return false;
        }

        switch (action)
        {
            case ActionCrawlerStop:
                return crawlerController.Stop();

            case ActionCrawlerForwardShort:
                return crawlerController.ForwardShort();

            case ActionCrawlerBackShort:
                return crawlerController.BackShort();

            case ActionCrawlerTurnLeftShort:
                return crawlerController.TurnLeftShort();

            case ActionCrawlerTurnRightShort:
                return crawlerController.TurnRightShort();

            default:
                Debug.LogWarning($"[CrawlerActionExecutor] Unknown crawler action={action}");
                return false;
        }
    }

    private static string NormalizeActionName(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return ActionCrawlerStop;

        action = action.Trim();

        switch (action)
        {
            case "crawlerStop":
            case "stopCrawler":
                return ActionCrawlerStop;

            case "crawlerForwardShort":
            case "crawler_fwd_short":
            case "move_forward_short":
                return ActionCrawlerForwardShort;

            case "crawlerBackShort":
            case "crawler_backward_short":
            case "move_back_short":
                return ActionCrawlerBackShort;

            case "crawlerTurnLeftShort":
            case "turn_left_short":
                return ActionCrawlerTurnLeftShort;

            case "crawlerTurnRightShort":
            case "turn_right_short":
                return ActionCrawlerTurnRightShort;

            default:
                return action;
        }
    }
}
