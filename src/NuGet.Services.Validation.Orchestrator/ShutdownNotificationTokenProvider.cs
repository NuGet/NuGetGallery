// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ShutdownNotificationTokenProvider : IShutdownNotificationTokenProvider
    {
        public ShutdownNotificationTokenProvider(CancellationToken cancellationToken)
        {
            Token = cancellationToken;
        }

        public CancellationToken Token { get; }
    }
}
