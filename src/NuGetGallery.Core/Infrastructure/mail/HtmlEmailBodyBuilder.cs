// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Validation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Web;

namespace NuGetGallery.Infrastructure.Mail
{
    /// <summary>
    /// Builds email-body messages using HTML markup.
    /// </summary>
    public class HtmlEmailBodyBuilder : IEmailBodyBuilder
    {
        public HtmlEmailBodyBuilder(ICoreMessageServiceConfiguration configuration)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        protected ICoreMessageServiceConfiguration Configuration { get; }

        public string ForReportAbuse(string galleryOwnerDisplayName, ReportPackageRequest request)
        {
            var alreadyContactedOwnersString = request.AlreadyContactedOwners ? "Yes" : "No";
            var userString = string.Empty;
            if (request.RequestingUser != null && request.RequestingUserUrl != null)
            {
                userString = string.Format(
                    CultureInfo.CurrentCulture,
                    "<p><b>User:</b> {0} ({1})<br/>{2}</p>",
                    request.RequestingUser.Username,
                    request.RequestingUser.EmailAddress,
                    request.RequestingUserUrl);
            }

            return $@"<p><b>Email:</b> {request.FromAddress.DisplayName} ({request.FromAddress.Address})</p>
<p><b>Signature:</b> {request.Signature}</p>
<p><b>Package:</b> {request.Package.PackageRegistration.Id}<br/>
{request.PackageUrl}</p>
<p><b>Version:</b> {request.Package.Version}<br/>
{request.PackageVersionUrl}</p>
{userString}
<p><b>Reason:</b><br/>
{request.Reason}</p>
<br/>
<p><b>Has the package owner been contacted?:</b><br/>
{alreadyContactedOwnersString}</p>
<br/>
<p><b>Message:</b><br/>
{request.Message}</p>
<hr/>
<p><i>Message sent from {galleryOwnerDisplayName}</i></p>";
        }

        public string ForReportMyPackage(string galleryOwnerDisplayName, ReportPackageRequest request)
        {
            var userString = string.Empty;
            if (request.RequestingUser != null && request.RequestingUserUrl != null)
            {
                userString = string.Format(
                    CultureInfo.CurrentCulture,
                    "<p><b>User:</b> {0} ({1})<br/>{2}</p>",
                    request.RequestingUser.Username,
                    request.RequestingUser.EmailAddress,
                    request.RequestingUserUrl);
            }

            return $@"<p><b>Email:</b> {request.FromAddress.DisplayName} ({request.FromAddress.Address})</p>
<p><b>Package:</b> {request.Package.PackageRegistration.Id}<br/>
{request.PackageUrl}</p>
<p><b>Version:</b> {request.Package.Version}<br/>
{request.PackageVersionUrl}</p>
{userString}
<br/>
<p><b>Reason:</b><br/>
{request.Reason}</p>
<br/>
<p><b>Message:</b><br/>
{request.Message}</p>
<hr/>
<p><i>Message sent from {galleryOwnerDisplayName}</i></p>";
        }

        public string ForSendAccountDeleteNotice(User user)
        {
            string template = @"<p>We received a request to delete your account {0}. If you did not initiate this request, please contact the {1} team immediately.</p>
<p>When your account will be deleted, we will:</p>
<ul>
<li>revoke your API key(s)</li>
<li>remove you as the owner for any package you own</li>
<li>remove your ownership from any ID prefix reservations and delete any ID prefix reservations that you were the only owner of</li>
</ul>
<p>We will not delete the NuGet packages associated with the account.</p>
<p>Thanks,</br>
The {1} Team</p>";

            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                user.Username,
                Configuration.GalleryOwner.DisplayName);
        }

        public string ForSendContactOwnersMessage(MailAddress fromAddress, Package package, string packageUrl, string message, string emailSettingsUrl)
        {
            var bodyTemplate = @"<p><i>User {0} &lt;{1}&gt; sends the following message to the owners of Package '[{2} {3}]({4})'.</i></p><br/>
<p>{5}</p><br/>
<hr/>
<em style=""font-size: 0.8em;"">
    To stop receiving contact emails as an owner of this package, sign in to the {6} and
    <a href=""{7}"">change your email notification settings</a>.
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

        public string ForSendContactSupportEmail(ContactSupportRequest request)
        {
            return string.Format(CultureInfo.CurrentCulture, @"<p><b>Email:</b> {0} ({1})<br/></p>
<p><b>Reason:</b><br/>
{2}<br/></p>
<p><b>Message:</b><br/>
{3}<br/></p>", request.RequestingUser.Username, request.RequestingUser.EmailAddress, request.SubjectLine, request.Message);
        }

        public string ForSendEmailChangeConfirmationNotice(string confirmationUrl, bool isOrganization)
        {
            var bodyTemplate = @"<p>You recently changed your {0}'s {1} email address.</p>
<p>To verify {0} new email address:<br/></p>
<p><a href=""{3}"">{2}</a><br/></p>
<p>Thanks,<br/>
The {1} Team</p>";

            return string.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                isOrganization ? "organization" : "account",
                Configuration.GalleryOwner.DisplayName,
                HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_"),
                confirmationUrl);
        }

        public string ForSendEmailChangeNoticeToPreviousEmailAddress(bool isOrganization, User user, string oldEmailAddress)
        {
            var template = @"<p>The email address associated with your {0} {1} was recently changed from <i>{2}</i> to <i>{3}</i>.<br/></p>
<p>Thanks,<br/>
The {0} Team</p>";

            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                Configuration.GalleryOwner.DisplayName,
                isOrganization ? "organization" : "account",
                oldEmailAddress,
                user.EmailAddress);
        }

        public string ForSendNewAccountEmail(User newUser, string confirmationUrl)
        {
            var isOrganization = newUser is Organization;

            return $@"<p>Thank you for {(isOrganization ? $"creating an organization on the" : $"registering with the")} {Configuration.GalleryOwner.DisplayName}.<br/>
We can't wait to see what packages you'll upload.</p>
<p>So we can be sure to contact you, please verify your email address and click the following link:</p>

<p><a href=""{confirmationUrl}"">{HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_")})</a><br/></p>
<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>";
        }

        public string ForSendOrganizationMemberRemovedNotice(Organization organization, User removedUser)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>The user '{removedUser.Username}' is no longer a member of organization '{organization.Username}'.<br/></p>
<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendOrganizationMembershipRequest(User adminUser, bool isAdmin, Organization organization, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            var membershipLevel = isAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>The user '{adminUser.Username}' would like you to become {membershipLevel} of their organization, ['{organization.Username}']({profileUrl}).</p>
<p>To learn more about organization roles, <a href=""https://go.microsoft.com/fwlink/?linkid=870439"">refer to the documentation</a>.</p>
<p>To accept the request and become {membershipLevel} of '{organization.Username}':<br/>
<a href=""{confirmationUrl}"">{confirmationUrl}</a><br/></p>
<p>To decline the request:<br/>
<a href=""{rejectionUrl}"">{rejectionUrl}</a><br/></p>
<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendOrganizationMembershipRequestCancelledNotice(Organization organization)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>The request for you to become a member of '{organization.Username}' has been cancelled.<br/></p>
<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendOrganizationMembershipRequestInitiatedNotice(User requestingUser, User pendingUser, Organization organization, bool isAdmin, string cancellationUrl)
        {
            var membershipLevel = isAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>The user '{requestingUser.Username}' has requested that user '{pendingUser.Username}' be added as {membershipLevel} of organization '{organization.Username}'. A confirmation mail has been sent to user '{pendingUser.Username}' to accept the membership request. This mail is to inform you of the membership changes to organization '{organization.Username}' and there is no action required from you.<br/></p>
<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendOrganizationMembershipRequestRejectedNotice(User pendingUser)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>The user '{pendingUser.Username}' has declined your request to become a member of your organization.<br/></p>
<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendOrganizationMemberUpdatedNotice(Membership membership, Organization organization)
        {
            var membershipLevel = membership.IsAdmin ? "an administrator" : "a collaborator";
            var member = membership.Member;

            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>The user '{member.Username}' is now {membershipLevel} of organization '{organization.Username}'.<br/></p>
<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendOrganizationTransformInitiatedNotice(User accountToTransform, User adminUser, string cancellationUrl)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>We have received a request to transform account '{accountToTransform.Username}' into an organization with user '{adminUser.Username}' as its admin.</p>

<p>To cancel the transformation:<br/>
<a href=""{cancellationUrl}"">{cancellationUrl}</a></p>

<p>If you did not request this change, please contact support by responding to this email.</p>

<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendOrganizationTransformRequest(User accountToTransform, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            return string.Format(CultureInfo.CurrentCulture, $@"<p>We have received a request to transform account <a href=""{profileUrl}"">'{accountToTransform.Username}'</a> into an organization.</p>

<p>To proceed with the transformation and become an administrator of '{accountToTransform.Username}':<br/>
<a href=""{confirmationUrl}"">{confirmationUrl}</a></p>

<p>To cancel the transformation:<br/>
<a href=""{rejectionUrl}"">{rejectionUrl}</a></p>

<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendOrganizationTransformRequestAcceptedNotice(User accountToTransform, User adminUser)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>Account '{accountToTransform.Username}' has been transformed into an organization with user '{adminUser.Username}' as its administrator. If you did not request this change, please contact support by responding to this email.</p>

<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendOrganizationTransformRequestRejectedNotice(User accountToTransform, User accountToReplyTo)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>Transformation of account '{accountToTransform.Username}' has been cancelled by user '{accountToReplyTo.Username}'.</p>
<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendPackageAddedNotice(bool hasWarnings, IEnumerable<string> warningMessages, Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl)
        {
            var warningMessagesPlaceholder = string.Empty;
            if (hasWarnings)
            {
                warningMessagesPlaceholder = "<p><br/>" + string.Join("<br/>", warningMessages) + "</p>";
            }

            return $@"<p>The package <a href=""{packageUrl}"">{package.PackageRegistration.Id} {package.Version}</a> was recently published on {Configuration.GalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please <a href=""{packageSupportUrl}"">contact support</a>.
{warningMessagesPlaceholder}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {Configuration.GalleryOwner.DisplayName} and
    <a href=""{emailSettingsUrl}"">change your email notification settings</a>.
</em>";
        }

        public string ForSendPackageAddedWithWarningsNotice(Package package, string packageUrl, string packageSupportUrl, IEnumerable<string> warningMessages)
        {
            var warningMessagesPlaceholder = "<br/>" + string.Join("<br/>", warningMessages);

            return $@"<p>The package <a href=""{packageUrl}"">{package.PackageRegistration.Id} {package.Version}</a> was recently pushed to {Configuration.GalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please <a href=""{packageSupportUrl}"">contact support</a>.</p>
<p>{warningMessagesPlaceholder}</p>";
        }

        public string ForSendPackageDeletedNotice(Package package, string packageUrl, string packageSupportUrl)
        {
            var body = @"<p>The package <a href=""{3}"">{1} {2}</a> was just deleted from {0}. If this was not intended, please [contact support]({4}).</p>

<p>Thanks,<br/>
The {0} Team</p>";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                Configuration.GalleryOwner.DisplayName,
                package.PackageRegistration.Id,
                package.Version,
                packageUrl,
                packageSupportUrl);
        }

        public string ForSendPackageOwnerAddedNotice(User newOwner, PackageRegistration package, string packageUrl)
        {
            return $@"<p>User '{newOwner.Username}' is now an owner of the package <a href=""{packageUrl}"">'{package.Id}'</a>.</p>

<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>";
        }

        public string ForSendPackageOwnerRemovedNotice(User fromUser, User toUser, PackageRegistration package)
        {
            return $@"<p>The user '{fromUser.Username}' removed {(toUser is Organization ? "your organization" : "you")} as an owner of the package '{package.Id}'.</p>

<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>";
        }

        public string ForSendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string htmlEncodedMessage, string policyMessage)
        {
            if (!string.IsNullOrEmpty(policyMessage))
            {
                policyMessage = "<br/>" + policyMessage + "<br/>";
            }

            var isToUserOrganization = toUser is Organization;

            string body = string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>The user '{fromUser.Username}' would like to add {(isToUserOrganization ? "your organization" : "you")} as an owner of the package <a href=""{packageUrl}"">'{package.Id}'</a>.</p>

<p>{policyMessage}</p>");

            if (!string.IsNullOrWhiteSpace(htmlEncodedMessage))
            {
                body += "<br/><br/>" + string.Format(CultureInfo.CurrentCulture, $@"<p>The user '{fromUser.Username}' added the following message for you:<br/><br/>
'{htmlEncodedMessage}'</p>");
            }

            body += "<br/><br/>" + string.Format(CultureInfo.CurrentCulture, $@"<p>To accept this request and {(isToUserOrganization ? "make your organization" : "become")} a listed owner of the package:<br/><br/>

<a href=""{confirmationUrl}"">{confirmationUrl}</a><br/></p>

<p>To decline:<br/>

<a href=""{rejectionUrl}"">{rejectionUrl}</a><br/></p>

<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");

            return body;
        }

        public string ForSendPackageOwnerRequestCancellationNotice(User requestingOwner, User newOwner, PackageRegistration package)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>The user '{requestingOwner.Username}' has cancelled their request for {(newOwner is Organization ? "your organization" : "you")} to be added as an owner of the package '{package.Id}'.</p>

<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendPackageOwnerRequestInitiatedNotice(User requestingOwner, User newOwner, PackageRegistration package, string cancellationUrl)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>The user '{requestingOwner.Username}' has requested that user '{newOwner.Username}' be added as an owner of the package '{package.Id}'.</p>

<p>To cancel this request:<br/>
<a href=""{cancellationUrl}"">{cancellationUrl}</a></p>

<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendPackageOwnerRequestRejectionNotice(User requestingOwner, User newOwner, PackageRegistration package)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"<p>The user '{newOwner.Username}' has declined {(requestingOwner is Organization ? "your organization's" : "your")} request to add them as an owner of the package '{package.Id}'.</p>

<p>Thanks,<br/>
The {Configuration.GalleryOwner.DisplayName} Team</p>");
        }

        public string ForSendPackageValidationFailedNotice(Package package, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            var validationIssues = validationSet.GetValidationIssues();

            var bodyBuilder = new StringBuilder();
            bodyBuilder.Append($@"<p>The package [{package.PackageRegistration.Id} {package.Version}]({packageUrl}) failed validation because of the following reason(s):</p>
");

            foreach (var validationIssue in validationIssues)
            {
                bodyBuilder.Append($@"
- {validationIssue.ParseMarkdown(announcementsUrl, twitterUrl)}");
            }

            bodyBuilder.Append($@"

<p>Your package was not published on {Configuration.GalleryOwner.DisplayName} and is not available for consumption.</p>

");

            if (validationIssues.Any(i => i.IssueCode == ValidationIssueCode.Unknown))
            {
                bodyBuilder.Append($@"<p>Please <a href=""{packageSupportUrl}"">contact support</a> to help fix your package.</p>");
            }
            else
            {
                var issuePluralString = validationIssues.Count() > 1 ? "all the issues" : "the issue";
                bodyBuilder.Append($"<p>You can reupload your package once you've fixed {issuePluralString} with it.</p>");
            }

            return bodyBuilder.ToString();
        }

        public string ForSendPasswordResetInstructions(string resetPasswordUrl, bool forgotPassword)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                forgotPassword ? CoreStrings.Emails_ForgotPassword_HtmlBody : CoreStrings.Emails_SetPassword_HtmlBody,
                resetPasswordUrl,
                Configuration.GalleryOwner.DisplayName);
        }

        public string ForSendSymbolPackageAddedNotice(SymbolPackage symbolPackage, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages, bool hasWarnings)
        {
            var warningMessagesPlaceholder = string.Empty;
            if (hasWarnings)
            {
                warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, warningMessages);
            }

            return $@"<p>The symbol package <a href=""{packageUrl}"">{symbolPackage.Id} {symbolPackage.Version}</a> was recently published on {Configuration.GalleryOwner.DisplayName} by {symbolPackage.Package.User.Username}. If this was not intended, please <a href=""{packageSupportUrl}"">contact support</a>.</p>
{warningMessagesPlaceholder}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {Configuration.GalleryOwner.DisplayName} and<br/>
    <a href=""{emailSettingsUrl}"">change your email notification settings</a>.
</em>";
        }

        public string ForSendSymbolPackageValidationFailedNotice(SymbolPackage symbolPackage, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            var validationIssues = validationSet.GetValidationIssues();

            var bodyBuilder = new StringBuilder();
            bodyBuilder.Append($@"<p>The symbol package <a href=""{packageUrl}"">{symbolPackage.Id} {symbolPackage.Version}</a> failed validation because of the following reason(s):</p>");

            foreach (var validationIssue in validationIssues)
            {
                bodyBuilder.Append($@"
- {validationIssue.ParseHtml(announcementsUrl, twitterUrl)}");
            }

            bodyBuilder.Append($@"

<p>Your symbol package was not published on {Configuration.GalleryOwner.DisplayName} and is not available for consumption.</p>

");

            if (validationIssues.Any(i => i.IssueCode == ValidationIssueCode.Unknown))
            {
                bodyBuilder.Append($@"<p>Please <a href=""{packageSupportUrl}"">contact support</a> to help.</p>");
            }
            else
            {
                var issuePluralString = validationIssues.Count() > 1 ? "all the issues" : "the issue";
                bodyBuilder.Append($"<p>You can reupload your symbol package once you've fixed {issuePluralString} with it.</p>");
            }

            return bodyBuilder.ToString();
        }

        public string ForSendValidationTakingTooLongNotice(Package package, string packageUrl)
        {
            string body = @"<p>It is taking longer than expected for your package <a href=""{3}"">{1} {2}</a> to get published.</p>" +
                   "<p>We are looking into it and there is no action on you at this time. We’ll send you an email notification when your package has been published.</p>" +
                   "<p>Thank you for your patience.</p>";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                Configuration.GalleryOwner.DisplayName,
                package.PackageRegistration.Id,
                package.Version,
                packageUrl);
        }

        public string ForSendValidationTakingTooLongNotice(SymbolPackage symbolPackage, string packageUrl)
        {
            string body = @"<p>It is taking longer than expected for your symbol package <a href=""{3}"">{1} {2}</a> to get published.</p>" +
                   "<p>We are looking into it and there is no action on you at this time. We’ll send you an email notification when your symbol package has been published.</p>" +
                   "<p>Thank you for your patience.</p>";

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