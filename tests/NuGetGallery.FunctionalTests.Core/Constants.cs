// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.FunctionalTests
{
    public static class Constants
    {
        // Headers
        public const string NuGetHeaderApiKey = "X-NuGet-ApiKey";
        public const string NuGetHeaderClientVersion = "X-NuGet-Client-Version";
        public const string NuGetHeaderProtocolVersion = "X-NuGet-Protocol-Version";
        public const string NuGetProtocolVersion = "4.1.0";

        // Form Fields
        public const string ConfirmPasswordFormField = "ConfirmPassword";
        public const string EmailAddressFormField = "Register.EmailAddress";
        public const string RegisterPasswordFormField = "Register.Password";
        public const string PasswordFormField = "SignIn.Password";
        public const string ConfirmPasswordField = "ConfirmPassword";
        public const string UserNameFormField = "Register.Username";
        public const string UserNameOrEmailFormField = "SignIn.UserNameOrEmail";
        public const string AcceptTermsField = "AcceptTerms";

        // Predefined Texts
        public const string HomePageText = "What is NuGet?";
        public const string InvalidUserText = "A unique user with that username or email address and password does not exist";
        public const string RegisterNewUserConfirmationText = "Your account is now registered!";
        public const string UserAlreadyExistsText = "User already exists";
        public const string ReadOnlyModeRegisterNewUserText = "503 : Please try again later! (Read-only)";
        public const string CreateNewAccountText = "Create A New Account";
        public const string StatsPageDefaultText = "Statistics last updated";
        public const string ContactOwnersText = "Your message has been sent to the owners of";
        public const string UnListedPackageText = "This package is unlisted and hidden from package listings";
        public const string TestDataAccount = "NuGetTestData";
        public const string TestPackageId = "BaseTestPackage";
        public const string TestPackageIdDotNetTool = "BaseTestPackage.DotnetTool";
        public const string TestPackageIdARandomType = "BaseTestPackage.ARandomType";
        public const string TestPackageIdTemplate = "BaseTestPackage.Template";
        public const string TestPackageIdWithPrereleases = "BaseTestPackage.SearchFilters";
        public const string TestPackageIdNoStable = "BaseTestPackage.NoStable";
        public const string ReadOnlyModeError = "Error 503 - Read-only Mode";
        public const string UploadFailureMessage = "The package upload via Nuget.exe didnt succeed properly. Check the logs to see the process error and output stream";
        public const string PackageInstallFailureMessage = "Package install failed. Either the file is not present on disk or it is corrupted. Check logs for details";
        public const string PackageNotFoundAfterUpload = "Package {0} is not found in the site {1} after uploading.";
        public const string PackageDownloadFailureMessage = "Package download from V2 feed didnt work";
        public const string UnableToZipError = "Unable to unzip the package downloaded via V2 feed. Check log for details";
        public const string NuGetOrgUrl = "https://www.nuget.org";
    }
}