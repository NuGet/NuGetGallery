// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery
{
    public class ConfirmationViewModel
    {
        public bool SuccessfulConfirmation { get; set; }

        public bool SentEmail { get; set; }

        public bool WrongUsername { get; set; }

        public bool DuplicateEmailAddress { get; set; }

        public bool AlreadyConfirmed { get; }

        public bool ConfirmingNewAccount { get; }

        public string ConfirmedEmailAddress { get; }

        public string UnconfirmedEmailAddress { get; }

        public ConfirmationViewModel(User user)
        {
            AlreadyConfirmed = user.UnconfirmedEmailAddress == null;
            ConfirmingNewAccount = !user.Confirmed;
            ConfirmedEmailAddress = user.EmailAddress;
            UnconfirmedEmailAddress = user.UnconfirmedEmailAddress;
        }
    }
}
