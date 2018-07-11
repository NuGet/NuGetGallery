// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ServiceBus.Messaging;

namespace NuGet.Services.ServiceBus
{
    public sealed class OnMessageOptionsWrapper : IOnMessageOptions
    {
        public OnMessageOptions OnMessageOptions { get; set; }

        public bool AutoComplete
        {
            get => OnMessageOptions.AutoComplete;
            set => OnMessageOptions.AutoComplete = value;
        }

        public int MaxConcurrentCalls
        {
            get => OnMessageOptions.MaxConcurrentCalls;
            set => OnMessageOptions.MaxConcurrentCalls = value;
        }

        public OnMessageOptionsWrapper(OnMessageOptions options = null)
        {
            OnMessageOptions = OnMessageOptions ?? new OnMessageOptions();
        }
    }
}
