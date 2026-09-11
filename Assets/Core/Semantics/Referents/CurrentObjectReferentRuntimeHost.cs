// Project Salieri AI
// Source Version: 0.1.0
// Source Date: 2026-09-11
// Copyright (c) 2026 Studio Hazama 714
// Licensed under Project Salieri License v1.0
// SPDX-License-Identifier: LicenseRef-Project-Salieri-1.0
// See LICENSE for details.

using System;

using SalieriAI.Core.Experience.Observation;

using UnityEngine;

namespace SalieriAI.Core.Semantics.Referents
{
    /// <summary>
    /// Main-thread semantic bridge from the M5-A provider to the independent
    /// referent owner. It performs no perception, Recall, persistence, speech,
    /// conversation, or behavior operation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CurrentObjectReferentRuntimeHost :
        MonoBehaviour,
        ICurrentObjectReferentProvider
    {
        private readonly CurrentObjectReferentService service =
            new CurrentObjectReferentService();

        private IObservedObjectMemoryContextProvider source;
        private Func<string> referentIdFactory;
        private Func<DateTime> utcNow;
        private bool subscribed;

        public bool HasCurrentReferent => service.HasCurrentReferent;
        public CurrentObjectReferent CurrentReferent => service.CurrentReferent;
        public string LastError { get; private set; } = string.Empty;

        public event Action<CurrentObjectReferent> ReferentChanged
        {
            add => service.ReferentChanged += value;
            remove => service.ReferentChanged -= value;
        }

        public event Action<CurrentObjectReferent, string> ReferentCleared
        {
            add => service.ReferentCleared += value;
            remove => service.ReferentCleared -= value;
        }

        public void Configure(IObservedObjectMemoryContextProvider provider)
        {
            Configure(
                provider,
                () => Guid.NewGuid().ToString("N"),
                () => DateTime.UtcNow);
        }

        internal void Configure(
            IObservedObjectMemoryContextProvider provider,
            Func<string> idFactory,
            Func<DateTime> clock)
        {
            Unsubscribe();
            service.Clear("Runtime host reconfigured.");
            source = provider;
            referentIdFactory = idFactory;
            utcNow = clock;
            LastError = string.Empty;

            if (isActiveAndEnabled)
                SubscribeAndReadInitial();
        }

        private void Start()
        {
            if (source == null)
                ResolveProductionProvider();
        }

        private void OnEnable()
        {
            SubscribeAndReadInitial();
        }

        private void OnDisable()
        {
            Unsubscribe();
            service.Clear("Runtime host disabled.");
        }

        private void OnDestroy()
        {
            Unsubscribe();
            service.Clear("Runtime host destroyed.");
        }

        private void ResolveProductionProvider()
        {
            ObservedObjectMemoryContextRuntimeHost[] providers =
                UnityEngine.Object.FindObjectsOfType<
                    ObservedObjectMemoryContextRuntimeHost>();

            if (providers == null || providers.Length != 1)
            {
                LastError =
                    "Expected exactly one M5-A memory context provider. Count=" +
                    (providers != null ? providers.Length : 0);
                service.Clear(LastError);
                Debug.LogError(
                    "[CurrentObjectReferent][BOOTSTRAP_ERROR] " + LastError);
                return;
            }

            Configure(providers[0]);
            Debug.Log(
                "[CurrentObjectReferent][BOOTSTRAP] Provider connected.");
        }

        private void SubscribeAndReadInitial()
        {
            if (subscribed || source == null ||
                referentIdFactory == null || utcNow == null)
            {
                return;
            }

            source.ContextChanged += HandleContextChanged;
            source.ContextCleared += HandleContextCleared;
            subscribed = true;

            if (source.HasCurrentContext && source.CurrentContext != null)
                Publish(source.CurrentContext);
            else
                service.Clear("M5-A provider has no current context.");
        }

        private void Unsubscribe()
        {
            if (!subscribed || source == null)
                return;

            source.ContextChanged -= HandleContextChanged;
            source.ContextCleared -= HandleContextCleared;
            subscribed = false;
        }

        private void HandleContextChanged(ObservedObjectMemoryContext context)
        {
            Publish(context);
        }

        private void HandleContextCleared(
            ObservedObjectMemoryContext previous,
            string reason)
        {
            LastError = reason ?? string.Empty;
            service.Clear(reason);
        }

        private bool Publish(ObservedObjectMemoryContext context)
        {
            if (context == null || source == null ||
                referentIdFactory == null || utcNow == null)
            {
                LastError = "Referent source or runtime metadata is unavailable.";
                service.Clear(LastError);
                return false;
            }

            CurrentObjectReferent mapped =
                CurrentObjectReferentMapper.Map(
                    context,
                    service.CurrentReferent,
                    referentIdFactory(),
                    utcNow());

            if (!source.HasCurrentContext ||
                !ReferenceEquals(source.CurrentContext, context))
            {
                LastError =
                    "Referent mapping discarded because M5-A context changed.";
                service.Clear(LastError);
                return false;
            }

            if (mapped == null)
            {
                LastError = "Referent mapping failed.";
                service.Clear(LastError);
                return false;
            }

            LastError = mapped.DiagnosticError;
            return service.ReplaceCurrent(mapped);
        }
    }
}
