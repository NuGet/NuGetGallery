// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGet.Services.Validation;
using NuGetGallery.Authentication;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public abstract class MarkdownMessageBuilderFacts
    {
        protected static readonly IMessageServiceConfiguration Configuration = new TestMessageServiceConfiguration();

        public class Fakes
        {
            public static readonly User PackageOwnerSubscribedToPackagePushedNotification
                = new User("subscribedToPushNotification")
                {
                    EmailAddress = "subscribed.packageOwner@gallery.org",
                    NotifyPackagePushed = true
                };
            public static readonly User PackageOwnerNotSubscribedToPackagePushedNotification
                = new User("notSubscribedToPushNotification")
                {
                    EmailAddress = "notSubscribed.packageOwner@gallery.org",
                    NotifyPackagePushed = false
                };
            public static readonly User PackageOwnerWithEmailNotAllowed
                = new User("emailNotAllowed")
                {
                    EmailAddress = "emailNotAllowed.packageOwner@gallery.org",
                    NotifyPackagePushed = false,
                    EmailAllowed = false
                };
            public static readonly User PackageOwnerWithEmailAllowed
                = new User("emailAllowed")
                {
                    EmailAddress = "emailAllowed.packageOwner@gallery.org",
                    NotifyPackagePushed = false,
                    EmailAllowed = true
                };

            public static readonly Package Package = new Package
            {
                PackageRegistration = new PackageRegistration
                {
                    Id = "PackageId",
                    Owners = new List<User>
                    {
                        PackageOwnerSubscribedToPackagePushedNotification,
                        PackageOwnerNotSubscribedToPackagePushedNotification,
                        PackageOwnerWithEmailNotAllowed,
                        PackageOwnerWithEmailAllowed
                    }
                },
                User = new User
                {
                    Username = "Username",
                    EmailAddress = "user@gallery.org"
                },
                Version = "1.0.0"
            };

            public static readonly SymbolPackage SymbolPackage = new SymbolPackage
            {
                Package = Package
            };

            public static PackageValidationSet PackageValidationSet = new PackageValidationSet
            {
                PackageValidations = new List<PackageValidation>
                {
                   new PackageValidation
                   {
                       Key = Guid.NewGuid(),
                       PackageValidationIssues = new List<PackageValidationIssue>
                       {
                           new PackageValidationIssue
                           {
                               IssueCode = ValidationIssueCode.Unknown,
                               Data = "data"
                           }
                       }
                   }
                }
            };
            public static IEnumerable<string> WarningMessages = new List<string> { "Warning message" };
            public const string PreviousEmailAddress = "previousAddress@gallery.org";
            public static readonly MailAddress FromAddress = new MailAddress("sender@gallery.org", "Sender");
            public static readonly User RequestingUser = new User("requestingUser") { EmailAddress = "requestUser@gallery.org", EmailAllowed = true };
            public static readonly User OrganizationAdmin = new User("organizationAdmin")
            {
                EmailAddress = "organizationAdmin@gallery.org",
                EmailAllowed = true
            };
            public static readonly Organization RequestingOrganization = new Organization("requestingOrganization")
            {
                EmailAddress = "requestOrganization@gallery.org",
                EmailAllowed = true
            };
            public static readonly User UnconfirmedUser = new User("unconfirmedUser") { UnconfirmedEmailAddress = "unconfirmedUser@gallery.org" };
            public static readonly Organization UnconfirmedOrganization = new Organization("unconfirmedOrganization") { UnconfirmedEmailAddress = "unconfirmedOrganization@gallery.org" };

            public static readonly Membership OrganizationMembership = new Membership
            {
                Member = RequestingUser,
                Organization = RequestingOrganization
            };

            public const string CancellationUrl = "cancellationUrl";
            public const string ConfirmationUrl = "confirmationUrl";
            public const string PackageUrl = "packageUrl";
            public const string PackageVersionUrl = "packageVersionUrl";
            public const string PackageSupportUrl = "packageSupportUrl";
            public const string ProfileUrl = "profileUrl";
            public const string EmailSettingsUrl = "emailSettingsUrl";
            public const string AnnouncementsUrl = "announcementsUrl";
            public const string TwitterUrl = "twitterUrl";

            public static readonly CredentialTypeInfo ApiKeyCredentialTypeInfo
               = new CredentialTypeInfo(
                   type: "ApiKey",
                   isApiKey: true,
                   description: "Api Key description");
            public static readonly CredentialTypeInfo NonApiKeyCredentialTypeInfo
               = new CredentialTypeInfo(
                   type: "MS Authentication",
                   isApiKey: false,
                   description: "Microsoft Account");

            public static readonly Credential ApiKeyCredential
                = new Credential("TestCredentialType", "TestCredentialValue");
        }
    }
}