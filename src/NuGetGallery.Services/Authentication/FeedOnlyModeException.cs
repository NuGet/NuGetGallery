// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGetGallery.Authentication
{
    [Serializable]
    public class FeedOnlyModeException : Exception
    {
        public const string FeedOnlyModeError = "Illegal request! Running on Feed Only mode. User Registration or authentication is disallowed.";
        public FeedOnlyModeException() { }

        public FeedOnlyModeException(string message) : base(message) { }
    }
}
