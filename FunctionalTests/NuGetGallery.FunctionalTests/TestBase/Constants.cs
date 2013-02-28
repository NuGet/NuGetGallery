﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests
{
    internal static class Constants
    {
        #region FormFields
        internal const string ConfirmPasswordFormField = "ConfirmPassword";
        internal const string EmailAddressFormField = "EmailAddress";
        internal const string PasswordFormField = "Password";
        internal const string UserNameFormField = "Username";
        internal const string UserNameOrEmailFormField = "UserNameOrEmail";
        #endregion FormFields

        #region PredefinedText
        internal const string HomePageText = "Jump Start Your Projects with NuGet";
        internal const string PendingConfirmationText = "An email with instructions on how to activate your account is on its way to you.";
        internal const string SearchTerm = "elmah";
        internal const string StatsPageDefaultText = "Download statistics displayed on this page reflect the actual package downloads from the NuGet.org site";
        #endregion PredefinedText

     }


}
