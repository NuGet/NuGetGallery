// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery
{
    public class ConfirmationViewModel
    {
        public bool IsOrganization { get; }

        public string AccountName { get; }

        public bool SuccessfulConfirmation { get; set; }

        public bool SentEmail { get; set; }

        public bool WrongUsername { get; }

        public bool DuplicateEmailAddress { get; set; }

        public bool AlreadyConfirmed { get; }

        public bool ConfirmingNewAccount { get; }

        public string ConfirmedEmailAddress { get; }

        public string UnconfirmedEmailAddress { get; }

        /// <summary>
        /// Creates a <see cref="ConfirmationViewModel"/> for an attempt to confirm a different account's email.
        /// </summary>
        /// <param name="username">The username of the account whose email was attempted to be confirmed.</param>
        public ConfirmationViewModel(string username)
        {
            AccountName = username;
            WrongUsername = true;
            SuccessfulConfirmation = false;
            AlreadyConfirmed = true;
        }

        /// <summary>
        /// Creates a <see cref="ConfirmationViewModel"/> for a user attempting to confirm their email.
        /// </summary>
        /// <param name="user">The account attempting to confirm their email.</param>
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
