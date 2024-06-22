// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.IO;
using System.Reflection;

namespace NuGetGallery
{
    internal static class TestDataResourceUtility
    {
        private static readonly string ResourceNameFormat = "NuGetGallery.TestData.{0}";
        private static readonly Assembly CurrentAssembly = typeof(TestDataResourceUtility).Assembly;

        internal static byte[] GetResourceBytes(string name)
        {
            using (var reader = new BinaryReader(GetManifestResourceStream(name)))
            {
                return reader.ReadBytes((int)reader.BaseStream.Length);
            }
        }

        private static Stream GetManifestResourceStream(string name)
        {
            var resourceName = GetResourceName(name);

            return CurrentAssembly.GetManifestResourceStream(resourceName);
        }

        private static string GetResourceName(string name)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                ResourceNameFormat,
                name);
        }
    }
}