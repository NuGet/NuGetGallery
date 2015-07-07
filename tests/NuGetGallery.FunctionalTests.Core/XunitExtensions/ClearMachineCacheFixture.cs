// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.FunctionalTests
{
    public class ClearMachineCacheFixture
        : IDisposable
    {
        public ClearMachineCacheFixture()
        {
            // Clear the machine cache during the start of every test to make sure that we always hit the gallery.
            ClientSdkHelper.ClearMachineCache();
        }

        public void Dispose()
        {
            // Clear the machine cache during the end of every test to make sure that we always hit the gallery.
            ClientSdkHelper.ClearMachineCache();
        }
    }
}