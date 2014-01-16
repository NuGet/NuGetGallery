using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.FunctionalTests
{
    internal static class Constants
    {
        #region FormFields
        internal const string ConfirmPasswordFormField = "ConfirmPassword";
        internal const string EmailAddressFormField = "Register.EmailAddress";
        internal const string RegisterPasswordFormField = "Register.Password";
        internal const string PasswordFormField = "SignIn.Password";
        internal const string ConfirmPasswordField = "ConfirmPassword";
        internal const string UserNameFormField = "Register.Username";
        internal const string UserNameOrEmailFormField = "SignIn.UserNameOrEmail";
        internal const string AcceptTermsField = "AcceptTerms";
        #endregion FormFields

        #region PredefinedText
        internal const string HomePageText = "What is NuGet?";
        internal const string RegisterNewUserPendingConfirmationText = "Your account is now registered!";
        internal const string UserAlreadyExistsText = "User already exists";
        internal const string ReadOnlyModeRegisterNewUserText = "503 : Please try again later! (Read-only)";
        internal const string SearchTerm = "elmah";
        internal const string CreateNewAccountText = "Create A New Account";
        internal const string StatsPageDefaultText = "Download statistics displayed on this page reflect the actual package downloads from the NuGet.org site";
        internal const string ContactOwnersText = "Your message has been sent to the owners of ";
        internal const string UnListedPackageText = "This package is unlisted and hidden from package listings";
        internal const string TestPackageId = "BaseTestPackage";
        #endregion PredefinedText

    }


}

