// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;

namespace Stats.RefreshClientDimension
{
    [DebuggerDisplay("{UserAgent}")]
    public sealed class UserAgentToClientDimensionLink
    {
        public UserAgentToClientDimensionLink(string userAgent, int userAgentId, int currentClientDimensionId, int newClientDimensionId)
        {
            UserAgent = userAgent;
            UserAgentId = userAgentId;
            CurrentClientDimensionId = currentClientDimensionId;
            NewClientDimensionId = newClientDimensionId;
        }

        public string UserAgent { get; private set; }
        public int UserAgentId { get; private set; }
        public int CurrentClientDimensionId { get; private set; }
        public int NewClientDimensionId { get; private set; }
    }
}