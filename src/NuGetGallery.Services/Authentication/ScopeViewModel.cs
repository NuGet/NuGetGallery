// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class ScopeViewModel
    {
        public string Owner { get; set; }
        public string Subject { get; set; }
        public string AllowedAction { get; set; }

        public ScopeViewModel(string owner, string subject, string allowedAction)
        {
            Owner = owner;
            Subject = subject;
            AllowedAction = allowedAction;
        }
    }
}