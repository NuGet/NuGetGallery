// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.AccountDeleter.Configuration
{
    // Include evaluator config here maybe?
    public class SourceConfiguration
    {
        public string SourceName { get; set; }

        public string SubjectTemplate { get; set; }

        public string MessageTemplate { get; set; }

        public bool SendMessageOnSuccess { get; set; }

        public string SuccessSubjectTemplate { get; set; }

        public string SuccessMessageTemplate { get; set; }
    }
}
