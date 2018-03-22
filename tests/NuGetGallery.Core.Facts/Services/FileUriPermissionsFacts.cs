// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;

namespace NuGetGallery.Services
{
    public class FileUriPermissionsFacts
    {
        [Fact]
        public void MatchesWindowAzureStorageEnumNames()
        {
            var expectedPairs = GetPairs<SharedAccessBlobPermissions>();
            var actualPairs = GetPairs<FileUriPermissions>();

            Assert.Equal(expectedPairs, actualPairs);
        }

        private static List<KeyValuePair<string, int>> GetPairs<T>() where T : struct
        {
            return Enum
                .GetValues(typeof(T))
                .Cast<T>()
                .ToDictionary(x => x.ToString(), x => (int)(object)x)
                .OrderBy(x => x.Value)
                .ToList();
        }
    }
}
