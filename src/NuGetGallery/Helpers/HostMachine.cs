// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGetGallery.Helpers
{
    public static class HostMachine
    {
        private static Lazy<string> _name = new Lazy<string>(DetermineName);

        public static string Name
        {
            get { return _name.Value; }
        }

        private static string DetermineName()
        {
            return Environment.MachineName;
        }
    }
}
