// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Reflection;
using Xunit;

namespace NuGetGallery.Services
{
    public class FolderNamesDataAttributeFacts
    {
        [Fact]
        public void FolderNameDataContainsAllFolders()
        {
            var folderNameFields = typeof(CoreConstants.Folders)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly).ToList();

            var folderNames = new FolderNamesDataAttribute().GetData(null).Select(a => (string)a[0]).ToList();

            Assert.Equal(folderNameFields.Count, folderNames.Count);

            foreach (var folderNameField in folderNameFields)
            {
                var folderName = (string)folderNameField.GetRawConstantValue();
                Assert.Contains(folderName, folderNames);
            }
        }
    }
}
