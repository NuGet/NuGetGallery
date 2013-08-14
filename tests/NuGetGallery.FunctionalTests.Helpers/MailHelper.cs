﻿using AE.Net.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.FunctionTests.Helpers
{
    /// <summary>
    /// Helper class Check for mail notifications from Gallery.
    /// </summary>
    public class MailHelper
    {
        /// <summary>
        /// Given a package Id, check if a contact owner mail has been sent to it.
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="subject"></param>
        /// <returns></returns>
        public static bool IsMailSentForContactOwner(string packageId, out string subject)
        {
            try
            {
                subject = GetLastMessageFromInBox(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword,"INBOX").Subject;              
                return subject.Equals(ContactOwnerMailDefaultSubject + "'" + packageId + "'");
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in checking the mails. Exception : {0}", e.Message);
                subject = null;
                return false;
            }
        }

        /// <summary>
        /// Checks if a new user registration mail has been sent.
        /// </summary>
        /// <param name="subject"></param>
        /// <returns></returns>
        public static bool IsMailSentForNewUserRegistration(out string subject)
        {
            try
            {
                subject = GetLastMessageFromInBox(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword, "INBOX").Subject;
                return subject.Contains(AccountConfirmationMailSubject);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in checking the mails. Exception : {0}", e.Message);
                subject = null;
                return false;
            }
        }

        /// <summary>
        /// For the given user name, retrieves the confirmation token from the mail.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public static string GetAccountConfirmationUrl(string userName)
        {
            try
            {
                string message = GetLastMessageFromInBox(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword, "INBOX").Body;

                if (message.IndexOf(userName) == -1 || message.IndexOf(GalleryTeamSignatureInMailBOdy) == -1)
                    return null;
                int userNameEndIndex = message.IndexOf(userName) + userName.Length + 1;
                int signatureStartIndex = message.IndexOf(GalleryTeamSignatureInMailBOdy);
                int lengthOfConfirmationToken = signatureStartIndex - userNameEndIndex;
                
                return message.Substring(userNameEndIndex,lengthOfConfirmationToken);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in checking the mails. Exception : {0}", e.Message);
                return null;
            }
        }

        #region PrivateMembers

        private static MailMessage GetLastMessageFromInBox(string testAccountName,string testAccountPassword,string folderName="INBOX")
        {
            // Connect to the IMAP server. 
            MailMessage message = null;
            Console.WriteLine(EnvironmentSettings.TestAccountName);
            Console.WriteLine(EnvironmentSettings.TestAccountPassword);
            using (ImapClient ic = new ImapClient(EnvironmentSettings.TestEmailServerHost, EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword,
                            ImapClient.AuthMethods.Login, 993, true))
            {
                // Select folder and get the last mail.
                ic.SelectMailbox(folderName);
                message= ic.GetMessage(ic.GetMessageCount() - 1);
                               
            }
            return message;
        }

        private const string ContactOwnerMailDefaultSubject = @"[NuGet Gallery] Message for owners of the package";
        private const string AccountConfirmationMailSubject = @"[NuGet Gallery] Please verify your account";
        private const string GalleryTeamSignatureInMailBOdy = "Thanks, The NuGet Gallery Team";
        #endregion PrivateMembers
    }
}
