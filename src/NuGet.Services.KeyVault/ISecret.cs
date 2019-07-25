// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;

namespace NuGet.Services.KeyVault
{
    public interface ISecret
    {
        string Name { get; }
        string Value { get; }
        DateTimeOffset? Expiration { get; }
    }
}
