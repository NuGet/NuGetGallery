using AE.Net.Mail;
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
        public static bool IsMailSentForContactOwner(string packageId)
        {
            try
            {
                string subject = GetLastMessageFromInBox(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);              
                return subject.Equals(ContactOwnerMailDefaultSubject + "'" + packageId + "'");
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in checking the mails. Exception : {0}", e.Message);
                return false;
            }
        }

        #region PrivateMembers

        private static string GetLastMessageFromInBox(string testAccountName,string testAccountPassword)
        {
            // Connect to the IMAP server. 
            string subject = string.Empty;
            using (ImapClient ic = new ImapClient("imap.gmail.com", EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword,
                            ImapClient.AuthMethods.Login, 993, true))
            {
                // Select inbox and get the last mail.
                ic.SelectMailbox("INBOX");
                MailMessage mm = ic.GetMessage(ic.GetMessageCount() - 1);
                subject = mm.Subject;                
            }
            return subject;
        }

        private const string ContactOwnerMailDefaultSubject = @"[NuGet Gallery] Message for owners of the package";
        #endregion PrivateMembers
    }
}
