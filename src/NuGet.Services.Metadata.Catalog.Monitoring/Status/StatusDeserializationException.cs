// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    [Serializable]
    public class StatusDeserializationException : Exception
    {
        public StatusDeserializationException(Exception e)
            : base("Failed to deserialize the status!", e)
        {
        }
    }
}
