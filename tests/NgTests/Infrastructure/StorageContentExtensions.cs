// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Infrastructure
{
    public static class StorageContentExtensions
    {
        public static string GetContentString(this StorageContent content)
        {
            var stringStorageContent = content as StringStorageContent;
            if (stringStorageContent != null)
            {
                return stringStorageContent.Content;
            }

            using (var reader = new StreamReader(content.GetContentStream()))
            {
                return reader.ReadToEnd();
            }
        }
    }
}