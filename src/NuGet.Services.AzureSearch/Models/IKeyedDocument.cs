// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// A base interface for referring to documents by their key.
    /// </summary>
    public interface IKeyedDocument
    {
        string Key { get; set; }
    }
}
