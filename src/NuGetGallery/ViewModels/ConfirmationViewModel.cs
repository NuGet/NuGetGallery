// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ConfirmationViewModel
    {
        public bool IsOrganization { get; }

        public string AccountName { get; }

        public bool SuccessfulConfirmation { get; set; }

        public bool SentEmail { get; set; }

        public bool WrongUsername { get; set; }

        public bool DuplicateEmailAddress { get; set; }

        public bool AlreadyConfirmed { get; }

        public bool ConfirmingNewAccount { get; }

        public string ConfirmedEmailAddress { get; }

        public string UnconfirmedEmailAddress { get; }
    
        public ConfirmationViewModel(string username)
        {
            AccountName = username;
            AlreadyConfirmed = true;
        }
        
        public ConfirmationViewModel(User user)
        {
            IsOrganization = user is Organization;
            AccountName = user.Username;
            AlreadyConfirmed = user.UnconfirmedEmailAddress == null;
            ConfirmingNewAccount = !user.Confirmed;
            ConfirmedEmailAddress = user.EmailAddress;
            UnconfirmedEmailAddress = user.UnconfirmedEmailAddress;
        }
    }
}
