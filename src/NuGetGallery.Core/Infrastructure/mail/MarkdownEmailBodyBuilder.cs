// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using NuGet.Services.Validation;

namespace NuGetGallery.Infrastructure.Mail
{
    /// <summary>
    /// Builds email-body messages using Markdown syntax.
    /// </summary>
    public class MarkdownEmailBodyBuilder : PlainTextEmailBodyBuilder
    {
        public MarkdownEmailBodyBuilder(ICoreMessageServiceConfiguration appConfiguration)
            : base(appConfiguration)
        {
        }

        public override string ForReportAbuse(string galleryOwnerDisplayName, ReportPackageRequest request)
        {
            var alreadyContactedOwnersString = request.AlreadyContactedOwners ? "Yes" : "No";
            var userString = string.Empty;
            if (request.RequestingUser != null && request.RequestingUserUrl != null)
            {
                userString = string.Format(
                    CultureInfo.CurrentCulture,
                    "{2}**User:** {0} ({1}){2}{3}",
                    request.RequestingUser.Username,
                    request.RequestingUser.EmailAddress,
                    Environment.NewLine,
                    request.RequestingUserUrl);
            }

            return $@"**Email**: {request.FromAddress.DisplayName} ({request.FromAddress.Address})

**Signature**: {request.Signature}

**Package**: {request.Package.PackageRegistration.Id}
{request.PackageUrl}

**Version**: {request.Package.Version}
{request.PackageVersionUrl}
{userString}

**Reason**:
{request.Reason}

**Has the package owner been contacted?**
{alreadyContactedOwnersString}

**Message:**
{request.Message}


Message sent from {galleryOwnerDisplayName}";
        }

        public override string ForReportMyPackage(string galleryOwnerDisplayName, ReportPackageRequest request)
        {
            var userString = string.Empty;
            if (request.RequestingUser != null && request.RequestingUserUrl != null)
            {
                userString = string.Format(
                    CultureInfo.CurrentCulture,
                    "{2}**User:** {0} ({1}){2}{3}",
                    request.RequestingUser.Username,
                    request.RequestingUser.EmailAddress,
                    Environment.NewLine,
                    request.RequestingUserUrl);
            }

            return $@"**Email**: {request.FromAddress.DisplayName} ({request.FromAddress.Address})

**Package**: {request.Package.PackageRegistration.Id}
{request.PackageUrl}

**Version**: {request.Package.Version}
{request.PackageVersionUrl}
{userString}

**Reason**:
{request.Reason}

**Message**:
{request.Message}


Message sent from {galleryOwnerDisplayName}";
        }

        public override string ForSendContactOwnersMessage(MailAddress fromAddress, Package package, string packageUrl, string message, string emailSettingsUrl)
        {
            var bodyTemplate = @"_User {0} &lt;{1}&gt; sends the following message to the owners of Package '[{2} {3}]({4})'._

{5}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving contact emails as an owner of this package, sign in to the {6} and
    [change your email notification settings]({7}).
</em>";

            return string.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                fromAddress.DisplayName,
                fromAddress.Address,
                package.PackageRegistration.Id,
                package.Version,
                packageUrl,
                message,
                Configuration.GalleryOwner.DisplayName,
                emailSettingsUrl);
        }

        public override string ForSendContactSupportEmail(ContactSupportRequest request)
        {
            return string.Format(CultureInfo.CurrentCulture, @"**Email:** {0} ({1})

**Reason:**
{2}

**Message:**
{3}
", request.RequestingUser.Username, request.RequestingUser.EmailAddress, request.SubjectLine, request.Message);
        }

        public override string ForSendEmailChangeNoticeToPreviousEmailAddress(bool isOrganization, User user, string oldEmailAddress)
        {
            var template = @"The email address associated with your {0} {1} was recently changed from _{2}_ to _{3}_.

Thanks,
The {0} Team";

            var yourString = user is Organization ? "organization" : "account";

            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                Configuration.GalleryOwner.DisplayName,
                yourString,
                oldEmailAddress,
                user.EmailAddress);
        }

        public override string ForSendOrganizationMembershipRequest(User adminUser, bool isAdmin, Organization organization, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            var membershipLevel = isAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{adminUser.Username}' would like you to become {membershipLevel} of their organization, ['{organization.Username}']({profileUrl}).

To learn more about organization roles, [refer to the documentation.](https://go.microsoft.com/fwlink/?linkid=870439)

To accept the request and become {membershipLevel} of '{organization.Username}':

[{confirmationUrl}]({confirmationUrl})

To decline the request:

[{rejectionUrl}]({rejectionUrl})

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public override string ForSendOrganizationTransformRequest(User accountToTransform, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            return string.Format(CultureInfo.CurrentCulture, $@"We have received a request to transform account ['{accountToTransform.Username}']({profileUrl}) into an organization.

To proceed with the transformation and become an administrator of '{accountToTransform.Username}':

[{confirmationUrl}]({confirmationUrl})

To cancel the transformation:

[{rejectionUrl}]({rejectionUrl})

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public override string ForSendPackageDeletedNotice(Package package, string packageUrl, string packageSupportUrl)
        {
            var body = @"The package [{1} {2}]({3}) was just deleted from {0}. If this was not intended, please [contact support]({4}).

Thanks,
The {0} Team";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                Configuration.GalleryOwner.DisplayName,
                package.PackageRegistration.Id,
                package.Version,
                packageUrl,
                packageSupportUrl);
        }

        public override string ForSendPackageOwnerAddedNotice(User newOwner, PackageRegistration package, string packageUrl)
        {
            return $@"User '{newOwner.Username}' is now an owner of the package ['{package.Id}']({packageUrl}).

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team";
        }

        public override string ForSendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string htmlEncodedMessage, string policyMessage)
        {

            if (!string.IsNullOrEmpty(policyMessage))
            {
                policyMessage = Environment.NewLine + policyMessage + Environment.NewLine;
            }

            var isToUserOrganization = toUser is Organization;

            string body = string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{fromUser.Username}' would like to add {(isToUserOrganization ? "your organization" : "you")} as an owner of the package ['{package.Id}']({packageUrl}).

{policyMessage}");

            if (!string.IsNullOrWhiteSpace(htmlEncodedMessage))
            {
                body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"The user '{fromUser.Username}' added the following message for you:

'{htmlEncodedMessage}'");
            }

            body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"To accept this request and {(isToUserOrganization ? "make your organization" : "become")} a listed owner of the package:

[{confirmationUrl}]({confirmationUrl})

To decline:

[{rejectionUrl}]({rejectionUrl})");

            body += Environment.NewLine + Environment.NewLine + $@"Thanks,
The {Configuration.GalleryOwner.DisplayName} Team";

            return body;
        }

        public override string ForSendPackageOwnerRequestInitiatedNotice(User requestingOwner, User newOwner, PackageRegistration package, string cancellationUrl)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{requestingOwner.Username}' has requested that user '{newOwner.Username}' be added as an owner of the package '{package.Id}'.

To cancel this request:

[{cancellationUrl}]({cancellationUrl})

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public override string ForSendPasswordResetInstructions(string resetPasswordUrl, bool forgotPassword)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                forgotPassword ? CoreStrings.Emails_ForgotPassword_MarkdownBody : CoreStrings.Emails_SetPassword_MarkdownBody,
                resetPasswordUrl,
                Configuration.GalleryOwner.DisplayName);
        }

        public override string ForSendPackageAddedNotice(bool hasWarnings, IEnumerable<string> warningMessages, Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl)
        {
            var warningMessagesPlaceholder = string.Empty;
            if (hasWarnings)
            {
                warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, warningMessages);
            }

            return $@"The package [{package.PackageRegistration.Id} {package.Version}]({packageUrl}) was recently published on {Configuration.GalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please [contact support]({packageSupportUrl}).
{warningMessagesPlaceholder}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {Configuration.GalleryOwner.DisplayName} and
    [change your email notification settings]({emailSettingsUrl}).
</em>";
        }

        public override string ForSendPackageAddedWithWarningsNotice(Package package, string packageUrl, string packageSupportUrl, IEnumerable<string> warningMessages)
        {
            var warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, warningMessages);

            return $@"The package [{package.PackageRegistration.Id} {package.Version}]({packageUrl}) was recently pushed to {Configuration.GalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please [contact support]({packageSupportUrl}).
{warningMessagesPlaceholder}
";
        }

        public override string ForSendPackageValidationFailedNotice(Package package, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            var validationIssues = validationSet.GetValidationIssues();

            var bodyBuilder = new StringBuilder();
            bodyBuilder.Append($@"The package [{package.PackageRegistration.Id} {package.Version}]({packageUrl}) failed validation because of the following reason(s):
");

            foreach (var validationIssue in validationIssues)
            {
                bodyBuilder.Append($@"
- {validationIssue.ParseMarkdown(announcementsUrl, twitterUrl)}");
            }

            bodyBuilder.Append($@"

Your package was not published on {Configuration.GalleryOwner.DisplayName} and is not available for consumption.

");

            if (validationIssues.Any(i => i.IssueCode == ValidationIssueCode.Unknown))
            {
                bodyBuilder.Append($"Please [contact support]({packageSupportUrl}) to help fix your package.");
            }
            else
            {
                var issuePluralString = validationIssues.Count() > 1 ? "all the issues" : "the issue";
                bodyBuilder.Append($"You can reupload your package once you've fixed {issuePluralString} with it.");
            }

            return bodyBuilder.ToString();
        }

        public override string ForSendSymbolPackageAddedNotice(SymbolPackage symbolPackage, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages, bool hasWarnings)
        {
            var warningMessagesPlaceholder = string.Empty;
            if (hasWarnings)
            {
                warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, warningMessages);
            }

            return $@"The symbol package [{symbolPackage.Id} {symbolPackage.Version}]({packageUrl}) was recently published on {Configuration.GalleryOwner.DisplayName} by {symbolPackage.Package.User.Username}. If this was not intended, please [contact support]({packageSupportUrl}).
{warningMessagesPlaceholder}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {Configuration.GalleryOwner.DisplayName} and
    [change your email notification settings]({emailSettingsUrl}).
</em>";
        }

        public override string ForSendSymbolPackageValidationFailedNotice(SymbolPackage symbolPackage, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            var validationIssues = validationSet.GetValidationIssues();

            var bodyBuilder = new StringBuilder();
            bodyBuilder.Append($@"The symbol package [{symbolPackage.Id} {symbolPackage.Version}]({packageUrl}) failed validation because of the following reason(s):
");

            foreach (var validationIssue in validationIssues)
            {
                bodyBuilder.Append($@"
- {validationIssue.ParseMarkdown(announcementsUrl, twitterUrl)}");
            }

            bodyBuilder.Append($@"

Your symbol package was not published on {Configuration.GalleryOwner.DisplayName} and is not available for consumption.

");

            if (validationIssues.Any(i => i.IssueCode == ValidationIssueCode.Unknown))
            {
                bodyBuilder.Append($"Please [contact support]({packageSupportUrl}) to help.");
            }
            else
            {
                var issuePluralString = validationIssues.Count() > 1 ? "all the issues" : "the issue";
                bodyBuilder.Append($"You can reupload your symbol package once you've fixed {issuePluralString} with it.");
            }

            return bodyBuilder.ToString();
        }

        public override string ForSendValidationTakingTooLongNotice(Package package, string packageUrl)
        {
            string body = "It is taking longer than expected for your package [{1} {2}]({3}) to get published.\n\n" +
                   "We are looking into it and there is no action on you at this time. We’ll send you an email notification when your package has been published.\n\n" +
                   "Thank you for your patience.";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                Configuration.GalleryOwner.DisplayName,
                package.PackageRegistration.Id,
                package.Version,
                packageUrl);
        }

        public override string ForSendValidationTakingTooLongNotice(SymbolPackage symbolPackage, string packageUrl)
        {
            string body = "It is taking longer than expected for your symbol package [{1} {2}]({3}) to get published.\n\n" +
                   "We are looking into it and there is no action on you at this time. We’ll send you an email notification when your symbol package has been published.\n\n" +
                   "Thank you for your patience.";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                Configuration.GalleryOwner.DisplayName,
                symbolPackage.Id,
                symbolPackage.Version,
                packageUrl);
        }
    }
}