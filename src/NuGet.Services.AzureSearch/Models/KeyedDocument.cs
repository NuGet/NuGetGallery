// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// This is a base type but can be used directly for operations that only require a document key, such as deleting
    /// a document.
    /// </summary>
    [SerializePropertyNamesAsCamelCase]
    public class KeyedDocument : IKeyedDocument
    {
        [Key]
        public string Key { get; set; }
    }
}
