// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using UnityEngine;
using SalieriAI.Body.Command;

public sealed class CrawlerController : MonoBehaviour
{
    [Header("Sender")]

    [Tooltip(
        "通常運用時の Body Command Sender。BodyCommandCoordinator を指定する。"
    )]
    [SerializeField]
    private MonoBehaviour senderBehaviour;

    [Header("Crawler Timing")]

    [SerializeField]
    private int shortMoveMilliseconds = 300;

    [SerializeField]
    private int maxMoveMilliseconds = 1000;

    private const string CommandStop = "CRAWLER:STOP";
    private const string CommandForward = "CRAWLER:FWD";
    private const string CommandBack = "CRAWLER:BACK";
    private const string CommandLeft = "CRAWLER:LEFT";
    private const string CommandRight = "CRAWLER:RIGHT";

    private IBodyCommandSender sender;

    private void Awake()
    {
        ResolveSender();
    }

    /// <summary>
    /// Inspector で指定した Sender を取得する。
    ///
    /// 未指定の場合は Scene 内の BodyCommandCoordinator を自動検索する。
    /// </summary>
    [ContextMenu("Resolve Sender")]
    public void ResolveSender()
    {
        sender = senderBehaviour as IBodyCommandSender;

        if (sender != null)
        {
            Debug.Log(
                $"[CrawlerController] " +
                $"Sender resolved: {senderBehaviour.name}",
                this
            );

            return;
        }

        BodyCommandCoordinator coordinator =
            FindObjectOfType<BodyCommandCoordinator>();

        if (coordinator != null)
        {
            senderBehaviour = coordinator;
            sender = coordinator;

            Debug.Log(
                $"[CrawlerController] " +
                $"Sender auto-resolved: {coordinator.name}",
                this
            );

            return;
        }

        Debug.LogWarning(
            "[CrawlerController] " +
            "IBodyCommandSender is not assigned.",
            this
        );
    }

    public bool Stop()
    {
        return SendCrawlerCommand(CommandStop);
    }

    public bool ForwardShort()
    {
        return Forward(shortMoveMilliseconds);
    }

    public bool BackShort()
    {
        return Back(shortMoveMilliseconds);
    }

    public bool TurnLeftShort()
    {
        return TurnLeft(shortMoveMilliseconds);
    }

    public bool TurnRightShort()
    {
        return TurnRight(shortMoveMilliseconds);
    }

    public bool Forward(int milliseconds)
    {
        return SendTimedCrawlerCommand(
            CommandForward,
            milliseconds
        );
    }

    public bool Back(int milliseconds)
    {
        return SendTimedCrawlerCommand(
            CommandBack,
            milliseconds
        );
    }

    public bool TurnLeft(int milliseconds)
    {
        return SendTimedCrawlerCommand(
            CommandLeft,
            milliseconds
        );
    }

    public bool TurnRight(int milliseconds)
    {
        return SendTimedCrawlerCommand(
            CommandRight,
            milliseconds
        );
    }

    private bool SendTimedCrawlerCommand(
        string baseCommand,
        int milliseconds
    )
    {
        int safeMilliseconds =
            Mathf.Clamp(
                milliseconds,
                1,
                maxMoveMilliseconds
            );

        return SendCrawlerCommand(
            $"{baseCommand}:{safeMilliseconds}"
        );
    }

    private bool SendCrawlerCommand(
        string command
    )
    {
        if (sender == null)
        {
            Debug.LogWarning(
                $"[CrawlerController] " +
                $"sender is null. command={command}",
                this
            );

            return false;
        }

        Debug.Log(
            $"[CrawlerController] Send command={command}",
            this
        );

        sender.SendRawCommand(command);

        return true;
    }
}