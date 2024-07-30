// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace Validation.Symbols.Tests
{
    public static class TestData
    {
        private static readonly string Prefix = typeof(TestData).Namespace + ".TestData.";

        private static byte[] GetEmbeddedResource(string name)
        {
            using (var resourceStream = typeof(TestData).Assembly.GetManifestResourceStream(Prefix + name))
            using (var buffer = new MemoryStream())
            {
                resourceStream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }

        public static byte[] BaselineDll => GetEmbeddedResource("testlib._0_baseline.testlib.dll");
        public static byte[] BaselinePdb => GetEmbeddedResource("testlib._0_baseline.testlib.pdb");

        public static byte[] AddClassDll => GetEmbeddedResource("testlib._1_add_class.testlib.dll");
        public static byte[] AddClassPdb => GetEmbeddedResource("testlib._1_add_class.testlib.pdb");

        public static byte[] WindowsDll => GetEmbeddedResource("testlib._2_windows.testlib.dll");
        public static byte[] WindowsPdb => GetEmbeddedResource("testlib._2_windows.testlib.pdb");
    }
}
