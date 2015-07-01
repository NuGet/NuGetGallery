// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    public class ConsoleTestOutputHelper
        : ITestOutputHelper
    {
        private ConsoleTestOutputHelper()
        {
        }

        public static ConsoleTestOutputHelper New
        {
            get { return new ConsoleTestOutputHelper(); }
        }

        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }
    }
}