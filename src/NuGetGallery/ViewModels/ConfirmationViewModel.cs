// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery
{
    public class ConfirmationViewModel
    {
        public string UnconfirmedEmailAddress { get; set; }

        public bool ConfirmingNewAccount { get; set; }

        public bool SuccessfulConfirmation { get; set; }

        public bool SentEmail { get; set; }

        public bool WrongUsername { get; set; }

        public bool DuplicateEmailAddress { get; set; }
    }
}
