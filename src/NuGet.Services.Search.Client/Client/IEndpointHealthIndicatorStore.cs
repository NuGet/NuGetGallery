// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Search.Client
{
    public interface IEndpointHealthIndicatorStore
    {
        int GetHealth(Uri endpoint);
        void DecreaseHealth(Uri endpoint, Exception exception);
        void IncreaseHealth(Uri endpoint);
    }
}