// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class SearchSideBySideMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly SearchSideBySideViewModel _model;
        private readonly string _searchUrl;

        public SearchSideBySideMessage(
            IMessageServiceConfiguration configuration,
            SearchSideBySideViewModel model,
            string searchUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _searchUrl = searchUrl ?? throw new ArgumentNullException(nameof(searchUrl));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            MailAddress[] replyTo = null;
            if (!string.IsNullOrWhiteSpace(_model.EmailAddress)
                && Regex.IsMatch(
                    _model.EmailAddress.Trim(),
                    GalleryConstants.EmailValidationRegex,
                    RegexOptions.None,
                    GalleryConstants.EmailValidationRegexTimeout))
            {
                replyTo = new[] { new MailAddress(_model.EmailAddress.Trim()) };
            }

            return new EmailRecipients(
                new[] { _configuration.GalleryOwner },
                cc: null,
                bcc: null,
                replyTo: replyTo);
        }

        public override string GetSubject() => $"[{_configuration.GalleryOwner.DisplayName}] Search Feedback";

        protected override string GetMarkdownBody()
        {
            var sb = new StringBuilder();

            sb.AppendLine("The following feedback has come from the search side-by-side page.");
            sb.AppendLine();

            var encodedSearchTerm = HttpUtility.HtmlEncode(_model.SearchTerm.Trim());
            sb.AppendFormat("**Search Query:** [{0}]({1})", encodedSearchTerm, _searchUrl);
            sb.AppendLine();
            sb.AppendLine();

            Append(sb, "Old Hits:", _model.OldHits);
            Append(sb, "New Hits:", _model.NewHits);

            Append(sb, SearchSideBySideViewModel.BetterSideLabel, _model.BetterSide);
            Append(sb, SearchSideBySideViewModel.MostRelevantPackageLabel, _model.MostRelevantPackage);
            Append(sb, SearchSideBySideViewModel.ExpectedPackagesLabel, _model.ExpectedPackages);
            Append(sb, SearchSideBySideViewModel.CommentsLabel, _model.Comments, extraLine: true);
            Append(sb, "Email:", _model.EmailAddress);

            return sb.ToString();
        }

        private void Append(
            StringBuilder sb,
            string label,
            object value,
            bool extraLine = false)
        {
            Append(sb, label, value?.ToString(), extraLine);
        }

        private void Append(
            StringBuilder sb,
            string label,
            string value,
            bool extraLine = false)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                sb.AppendFormat("**{0}**", label);

                if (extraLine)
                {
                    sb.AppendLine();
                }
                else
                {
                    sb.Append(' ');
                }

                sb.AppendLine(HttpUtility.HtmlEncode(value.Trim()));
                sb.AppendLine();
            }
        }
    }
}