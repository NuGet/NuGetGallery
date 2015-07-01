// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using AE.Net.Mail;
using Xunit.Abstractions;
using MailMessage = System.Net.Mail.MailMessage;

namespace NuGetGallery.FunctionalTests.Fluent.AccountManagement
{
    /// <summary>
    /// Helper class Check for mail notifications from Gallery.
    /// </summary>
    public class MailHelper
        : HelperBase
    {
        private const string _contactOwnerMailDefaultSubject = @"[NuGet Gallery] Message for owners of the package";
        private const string _abuseReportMailDefaultSubject = @"[NuGet Gallery] Support Request for '{0}' version {1} (Reason: {2})";
        private const string _contactSupportMailDefaultSubject = @"[NuGet Gallery] Owner Support Request for '{0}' version {1} (Reason: {2})";
        private const string _accountConfirmationMailSubject = @"[NuGet Gallery] Please verify your account";
        private const string _galleryTeamSignatureInMailBOdy = "Thanks, The NuGet Gallery Team";

        public MailHelper()
            : this(ConsoleTestOutputHelper.New)
        {
        }

        public MailHelper(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        /// <summary>
        /// Given a package Id, check if a contact owner mail has been sent to it.
        /// </summary>
        public bool IsMailSentForContactOwner(string packageId, out string subject)
        {
            try
            {
                subject = GetLastMessageFromInBox().Subject;
                return (MatchRemoveWhitespace(subject, _contactOwnerMailDefaultSubject + " '" + packageId + "'") ||
                        MatchRemoveWhitespace(subject, _contactOwnerMailDefaultSubject + " '" + packageId + "' [Sender Copy]"));
            }
            catch (Exception e)
            {
                WriteLine("Exception in checking the mails. Exception : {0}", e.Message);
                subject = null;
                return false;
            }
        }

        /// <summary>
        /// Checks if a new user registration mail has been sent.
        /// </summary>
        public bool IsMailSentForNewUserRegistration(out string subject)
        {
            try
            {
                subject = GetLastMessageFromInBox().Subject;
                return subject.Contains(_accountConfirmationMailSubject);
            }
            catch (Exception e)
            {
                WriteLine("Exception in checking the mails. Exception : {0}", e.Message);
                subject = null;
                return false;
            }
        }

        public bool IsMailSentForAbuseReport(string packageName, string version, string reason, out string subject)
        {
            try
            {
                subject = GetLastMessageFromInBox().Subject;
                return MatchRemoveWhitespace(subject, string.Format(_abuseReportMailDefaultSubject, packageName, version, reason));
            }
            catch (Exception e)
            {
                WriteLine("Exception in checking the mails. Exception : {0}", e.Message);
                subject = null;
                return false;
            }
        }

        public bool IsMailSentForContactSupport(string packageName, string version, string reason, out string subject)
        {
            try
            {
                subject = GetLastMessageFromInBox().Subject;
                return MatchRemoveWhitespace(subject, string.Format(_contactSupportMailDefaultSubject, packageName, version, reason));
            }
            catch (Exception e)
            {
                WriteLine("Exception in checking the mails. Exception : {0}", e.Message);
                subject = null;
                return false;
            }
        }

        /// <summary>
        /// For the given user name, retrieves the confirmation token from the mail.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public string GetAccountConfirmationUrl(string userName)
        {
            try
            {
                string message = GetLastMessageFromInBox().Body;

                if (message.IndexOf(userName, StringComparison.Ordinal) == -1 || message.IndexOf(_galleryTeamSignatureInMailBOdy, StringComparison.Ordinal) == -1)
                    return null;

                int userNameEndIndex = message.IndexOf(userName) + userName.Length + 1;
                int signatureStartIndex = message.IndexOf(_galleryTeamSignatureInMailBOdy);
                int lengthOfConfirmationToken = signatureStartIndex - userNameEndIndex;

                return message.Substring(userNameEndIndex, lengthOfConfirmationToken);
            }
            catch (Exception e)
            {
                WriteLine("Exception in checking the mails. Exception : {0}", e.Message);
                return null;
            }
        }

        // Most of the subject headers we need to validate have line breaks and other extra whitespace.
        // The library we use is bad at handling this, which is why we strip all whitespace before validating.
        public static bool MatchRemoveWhitespace(string input1, string input2)
        {
            for (var i = 0; i < 33; i++)
            {
                input1 = input1.Replace(Convert.ToChar(i).ToString(), String.Empty);
                input2 = input2.Replace(Convert.ToChar(i).ToString(), String.Empty);
            }
            return (input1 == input2);
        }

        private MailMessage GetLastMessageFromInBox(string folderName = "INBOX")
        {
            // Connect to the IMAP server.
            MailMessage message;
            WriteLine(EnvironmentSettings.TestAccountName);
            WriteLine(EnvironmentSettings.TestAccountPassword);
            using (var ic = new ImapClient(EnvironmentSettings.TestEmailServerHost, EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword, AuthMethods.Login, 993, true))
            {
                // Select folder and get the last mail.
                ic.SelectMailbox(folderName);
                message = ic.GetMessage(ic.GetMessageCount() - 1);

            }
            return message;
        }

    }
}
