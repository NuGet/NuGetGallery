// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    public abstract class HelperBase
    {
        private readonly ITestOutputHelper _testOutputHelper;

        protected HelperBase(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        protected ITestOutputHelper TestOutputHelper
        {
            get { return _testOutputHelper; }
        }

        protected void WriteLine(string format, params object[] args)
        {
            if (_testOutputHelper == null)
                return;

            _testOutputHelper.WriteLine(format, args);
        }

        protected void WriteLine(string message)
        {
            if (_testOutputHelper == null)
                return;

            _testOutputHelper.WriteLine(message);
        }
    }
}