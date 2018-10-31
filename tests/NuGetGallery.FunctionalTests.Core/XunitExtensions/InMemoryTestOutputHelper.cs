// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    public class InMemoryTestOutputHelper
        : ITestOutputHelper
    {
        private StringBuilder _output;

        private InMemoryTestOutputHelper()
        {
            _output = new StringBuilder();
        }

        public static InMemoryTestOutputHelper New
        {
            get { return new InMemoryTestOutputHelper(); }
        }

        public void WriteLine(string message)
        {
            _output.Append(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            _output.Append(string.Format(format, args));
        }

        public string GetOutput()
        {
            return _output.ToString();
        }
    }
}