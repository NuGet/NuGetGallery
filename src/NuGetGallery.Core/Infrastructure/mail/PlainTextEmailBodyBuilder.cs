// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Web;
using NuGet.Services.Validation;

namespace NuGetGallery.Infrastructure.Mail
{
    /// <summary>
    /// Builds plain-text email-body messages, without any markup.
    /// </summary>
    public class PlainTextEmailBodyBuilder : IEmailBodyBuilder
    {
        public PlainTextEmailBodyBuilder(ICoreMessageServiceConfiguration appConfiguration)
        {
            Configuration = appConfiguration;
        }

        protected ICoreMessageServiceConfiguration Configuration { get; }

        public virtual string ForReportAbuse(string galleryOwnerDisplayName, ReportPackageRequest request)
        {
            var alreadyContactedOwnersString = request.AlreadyContactedOwners ? "Yes" : "No";
            var userString = string.Empty;
            if(request.RequestingUser != null && request.RequestingUserUrl != null)
            {
                userString = string.Format(
                    CultureInfo.CurrentCulture,
                    "{2}User: {0} ({1}){2}{3}",
                    request.RequestingUser.Username,
                    request.RequestingUser.EmailAddress,
                    Environment.NewLine,
                    request.RequestingUserUrl);
            }

            return $@"Email: {request.FromAddress.DisplayName} ({request.FromAddress.Address})

Signature: {request.Signature}

Package: {request.Package.PackageRegistration.Id}
{request.PackageUrl}

Version: {request.Package.Version}
{request.PackageVersionUrl}
{userString}

Reason:
{request.Reason}

Has the package owner been contacted?
{alreadyContactedOwnersString}

Message:
{request.Message}


Message sent from {galleryOwnerDisplayName}";
        }

        public virtual string ForReportMyPackage(string galleryOwnerDisplayName, ReportPackageRequest request)
        {
            var userString = string.Empty;
            if (request.RequestingUser != null && request.RequestingUserUrl != null)
            {
                userString = string.Format(
                    CultureInfo.CurrentCulture,
                    "{2}User: {0} ({1}){2}{3}",
                    request.RequestingUser.Username,
                    request.RequestingUser.EmailAddress,
                    Environment.NewLine,
                    request.RequestingUserUrl);
            }

            return $@"Email: {request.FromAddress.DisplayName} ({request.FromAddress.Address})

Package: {request.Package.PackageRegistration.Id}
{request.PackageUrl}

Version: {request.Package.Version}
{request.PackageVersionUrl}
{userString}

Reason:
{request.Reason}

Message:
{request.Message}


Message sent from {galleryOwnerDisplayName}";
        }

        public virtual string ForSendAccountDeleteNotice(User user)
        {
            string template = @"We received a request to delete your account {0}. If you did not initiate this request, please contact the {1} team immediately.
{2}When your account will be deleted, we will:{2}
 - revoke your API key(s)
 - remove you as the owner for any package you own 
 - remove your ownership from any ID prefix reservations and delete any ID prefix reservations that you were the only owner of 

{2}We will not delete the NuGet packages associated with the account.

Thanks,
{2}The {1} Team";

            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                user.Username,
                Configuration.GalleryOwner.DisplayName,
                Environment.NewLine);
        }

        public virtual string ForSendContactOwnersMessage(MailAddress fromAddress, Package package, string packageUrl, string message, string emailSettingsUrl)
        {
            var bodyTemplate = @"User {0} &lt;{1}&gt; sends the following message to the owners of Package '{2} {3}' ({4}).

{5}

-----------------------------------------------
    To stop receiving contact emails as an owner of this package, sign in to the {6} and
    change your email notification settings: {7}";

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

        public virtual string ForSendContactSupportEmail(ContactSupportRequest request)
        {
            return string.Format(CultureInfo.CurrentCulture, @"Email: {0} ({1})

Reason:
{2}

Message:
{3}
", request.RequestingUser.Username, request.RequestingUser.EmailAddress, request.SubjectLine, request.Message);
        }

        public virtual string ForSendEmailChangeConfirmationNotice(string confirmationUrl, bool isOrganization)
        {
            var bodyTemplate = @"You recently changed your {0}'s {1} email address.

To verify {0} new email address:

[{2}]({3})

Thanks,
The {1} Team";

            return string.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                isOrganization ? "organization" : "account",
                Configuration.GalleryOwner.DisplayName,
                HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_"),
                confirmationUrl);
        }

        public virtual string ForSendEmailChangeNoticeToPreviousEmailAddress(bool isOrganization, User user, string oldEmailAddress)
        {
            var template = @"The email address associated with your {0} {1} was recently changed from {2} to {3}.

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

        public virtual string ForSendNewAccountEmail(User newUser, string confirmationUrl)
        {
            var isOrganization = newUser is Organization;

            return $@"Thank you for {(isOrganization ? $"creating an organization on the" : $"registering with the")} {Configuration.GalleryOwner.DisplayName}.
We can't wait to see what packages you'll upload.

So we can be sure to contact you, please verify your email address using the following link:

{HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_")}

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team";
        }

        public virtual string ForSendOrganizationMemberRemovedNotice(Organization organization, User removedUser)
        {
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"The user '{removedUser.Username}' is no longer a member of organization '{organization.Username}'.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendOrganizationMembershipRequest(User adminUser, bool isAdmin, Organization organization, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            var membershipLevel = isAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"The user '{adminUser.Username}' would like you to become {membershipLevel} of their organization, '{organization.Username}' ({profileUrl}).

To learn more about organization roles, refer to the documentation (https://go.microsoft.com/fwlink/?linkid=870439).

To accept the request and become {membershipLevel} of '{organization.Username}':

{confirmationUrl}

To decline the request:

{rejectionUrl}

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendOrganizationMembershipRequestCancelledNotice(Organization organization)
        {
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"The request for you to become a member of '{organization.Username}' has been cancelled.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendOrganizationMembershipRequestInitiatedNotice(User requestingUser, User pendingUser, Organization organization, bool isAdmin, string cancellationUrl)
        {
            var membershipLevel = isAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"The user '{requestingUser.Username}' has requested that user '{pendingUser.Username}' be added as {membershipLevel} of organization '{organization.Username}'. A confirmation mail has been sent to user '{pendingUser.Username}' to accept the membership request. This mail is to inform you of the membership changes to organization '{organization.Username}' and there is no action required from you.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendOrganizationMembershipRequestRejectedNotice(User pendingUser)
        {
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"The user '{pendingUser.Username}' has declined your request to become a member of your organization.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendOrganizationMemberUpdatedNotice(Membership membership, Organization organization)
        {
            var membershipLevel = membership.IsAdmin ? "an administrator" : "a collaborator";
            var member = membership.Member;

            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"The user '{member.Username}' is now {membershipLevel} of organization '{organization.Username}'.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendOrganizationTransformInitiatedNotice(User accountToTransform, User adminUser, string cancellationUrl)
        {
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"We have received a request to transform account '{accountToTransform.Username}' into an organization with user '{adminUser.Username}' as its admin.

To cancel the transformation:

[{cancellationUrl}]({cancellationUrl})

If you did not request this change, please contact support by responding to this email.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendOrganizationTransformRequest(User accountToTransform, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"We have received a request to transform account '{accountToTransform.Username}' ({profileUrl}) into an organization.

To proceed with the transformation and become an administrator of '{accountToTransform.Username}':
{confirmationUrl}

To cancel the transformation:
{rejectionUrl}

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendOrganizationTransformRequestAcceptedNotice(User accountToTransform, User adminUser)
        {
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"Account '{accountToTransform.Username}' has been transformed into an organization with user '{adminUser.Username}' as its administrator. If you did not request this change, please contact support by responding to this email.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendOrganizationTransformRequestRejectedNotice(User accountToTransform, User accountToReplyTo)
        {
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"Transformation of account '{accountToTransform.Username}' has been cancelled by user '{accountToReplyTo.Username}'.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendPackageAddedNotice(bool hasWarnings, IEnumerable<string> warningMessages, Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl)
        {
            var warningMessagesPlaceholder = string.Empty;
            if (hasWarnings)
            {
                warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, warningMessages);
            }

            return $@"The package {package.PackageRegistration.Id} {package.Version} ({packageUrl}) was recently published on {Configuration.GalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please contact support: {packageSupportUrl}.
{warningMessagesPlaceholder}

-----------------------------------------------
    To stop receiving emails as an owner of this package, sign in to the {Configuration.GalleryOwner.DisplayName} and
    change your email notification settings: {emailSettingsUrl}";
        }

        public virtual string ForSendPackageAddedWithWarningsNotice(Package package, string packageUrl, string packageSupportUrl, IEnumerable<string> warningMessages)
        {
            var warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, warningMessages);

            return $@"The package {package.PackageRegistration.Id} {package.Version} ({packageUrl}) was recently pushed to {Configuration.GalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please contact support: {packageSupportUrl}.
{warningMessagesPlaceholder}
";
        }

        public virtual string ForSendPackageDeletedNotice(Package package, string packageUrl, string packageSupportUrl)
        {
            var body = @"The package {1} {2} ({3}) was just deleted from {0}. If this was not intended, please contact support: {4}.

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

        public virtual string ForSendPackageOwnerAddedNotice(User newOwner, PackageRegistration package, string packageUrl)
        {
            return $@"User '{newOwner.Username}' is now an owner of the package '{package.Id}' ({packageUrl}).

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team";
        }

        public virtual string ForSendPackageOwnerRemovedNotice(User fromUser, User toUser, PackageRegistration package)
        {
            return $@"The user '{fromUser.Username}' removed {(toUser is Organization ? "your organization" : "you")} as an owner of the package '{package.Id}'.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team";
        }

        public virtual string ForSendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string htmlEncodedMessage, string policyMessage)
        {
            if (!string.IsNullOrEmpty(policyMessage))
            {
                policyMessage = Environment.NewLine + policyMessage + Environment.NewLine;
            }

            var isToUserOrganization = toUser is Organization;

            string body = string.Format(
                CultureInfo.CurrentCulture, 
                $@"The user '{fromUser.Username}' would like to add {(isToUserOrganization ? "your organization" : "you")} as an owner of the package '{package.Id}' ({packageUrl}).

{policyMessage}");

            if (!string.IsNullOrWhiteSpace(htmlEncodedMessage))
            {
                body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"The user '{fromUser.Username}' added the following message for you:

'{htmlEncodedMessage}'");
            }

            body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"To accept this request and {(isToUserOrganization ? "make your organization" : "become")} a listed owner of the package:
{confirmationUrl}

To decline:
{rejectionUrl}");

            body += Environment.NewLine + Environment.NewLine + $@"Thanks,
The {Configuration.GalleryOwner.DisplayName} Team";

            return body;
        }

        public virtual string ForSendPackageOwnerRequestCancellationNotice(User requestingOwner, User newOwner, PackageRegistration package)
        {
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"The user '{requestingOwner.Username}' has cancelled their request for {(newOwner is Organization ? "your organization" : "you")} to be added as an owner of the package '{package.Id}'.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendPackageOwnerRequestInitiatedNotice(User requestingOwner, User newOwner, PackageRegistration package, string cancellationUrl)
        {
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"The user '{requestingOwner.Username}' has requested that user '{newOwner.Username}' be added as an owner of the package '{package.Id}'.

To cancel this request:
{cancellationUrl}

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendPackageOwnerRequestRejectionNotice(User requestingOwner, User newOwner, PackageRegistration package)
        {
            return string.Format(
                CultureInfo.CurrentCulture, 
                $@"The user '{newOwner.Username}' has declined {(requestingOwner is Organization ? "your organization's" : "your")} request to add them as an owner of the package '{package.Id}'.

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team");
        }

        public virtual string ForSendPackageValidationFailedNotice(Package package, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            var validationIssues = validationSet.GetValidationIssues();

            var bodyBuilder = new StringBuilder();
            bodyBuilder.Append($@"The package {package.PackageRegistration.Id} {package.Version} ({packageUrl}) failed validation because of the following reason(s):
");

            foreach (var validationIssue in validationIssues)
            {
                bodyBuilder.Append($@"
- {validationIssue.ParsePlainText(announcementsUrl, twitterUrl)}");
            }

            bodyBuilder.Append($@"

Your package was not published on {Configuration.GalleryOwner.DisplayName} and is not available for consumption.

");

            if (validationIssues.Any(i => i.IssueCode == ValidationIssueCode.Unknown))
            {
                bodyBuilder.Append($"Please contact support ({packageSupportUrl}) to help fix your package.");
            }
            else
            {
                var issuePluralString = validationIssues.Count() > 1 ? "all the issues" : "the issue";
                bodyBuilder.Append($"You can reupload your package once you've fixed {issuePluralString} with it.");
            }

            return bodyBuilder.ToString();
        }

        public virtual string ForSendPasswordResetInstructions(string resetPasswordUrl, bool forgotPassword)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                forgotPassword ? CoreStrings.Emails_ForgotPassword_PlainTextBody : CoreStrings.Emails_SetPassword_PlainTextBody,
                resetPasswordUrl,
                Configuration.GalleryOwner.DisplayName);
        }

        public virtual string ForSendSymbolPackageAddedNotice(SymbolPackage symbolPackage, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages, bool hasWarnings)
        {
            var warningMessagesPlaceholder = string.Empty;
            if (hasWarnings)
            {
                warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, warningMessages);
            }

            return $@"The symbol package {symbolPackage.Id} {symbolPackage.Version} ({packageUrl}) was recently published on {Configuration.GalleryOwner.DisplayName} by {symbolPackage.Package.User.Username}. If this was not intended, please contact support ({packageSupportUrl}).
{warningMessagesPlaceholder}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {Configuration.GalleryOwner.DisplayName} and
    change your email notification settings: {emailSettingsUrl}.
</em>";
        }

        public virtual string ForSendSymbolPackageValidationFailedNotice(SymbolPackage symbolPackage, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            var validationIssues = validationSet.GetValidationIssues();

            var bodyBuilder = new StringBuilder();
            bodyBuilder.Append($@"The symbol package {symbolPackage.Id} {symbolPackage.Version} ({packageUrl}) failed validation because of the following reason(s):
");

            foreach (var validationIssue in validationIssues)
            {
                bodyBuilder.Append($@"
- {validationIssue.ParsePlainText(announcementsUrl, twitterUrl)}");
            }

            bodyBuilder.Append($@"

Your symbol package was not published on {Configuration.GalleryOwner.DisplayName} and is not available for consumption.

");

            if (validationIssues.Any(i => i.IssueCode == ValidationIssueCode.Unknown))
            {
                bodyBuilder.Append($"Please contact support ({packageSupportUrl}) to help.");
            }
            else
            {
                var issuePluralString = validationIssues.Count() > 1 ? "all the issues" : "the issue";
                bodyBuilder.Append($"You can reupload your symbol package once you've fixed {issuePluralString} with it.");
            }

            return bodyBuilder.ToString();
        }

        public virtual string ForSendValidationTakingTooLongNotice(Package package, string packageUrl)
        {
            string body = "It is taking longer than expected for your package {1} {2} ({3}) to get published.\n\n" +
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

        public virtual string ForSendValidationTakingTooLongNotice(SymbolPackage symbolPackage, string packageUrl)
        {
            string body = "It is taking longer than expected for your symbol package {1} {2} ({3}) to get published.\n\n" +
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

        private void AppendGalleryOwnerHtmlFooter(StringBuilder body)
        {
            body.AppendFormat(
                CultureInfo.InvariantCulture,
                @"

Message sent from {0}",
                Configuration.GalleryOwner.DisplayName);
        }
    }
}