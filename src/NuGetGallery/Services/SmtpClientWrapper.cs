using System.Net;
using System.Net.Mail;

namespace NuGetGallery.Services
{
    public class SmtpClientWrapper : ISmtpClient
    {
        readonly SmtpClient smtpClient;

        public SmtpClientWrapper(SmtpClient smtpClient)
        {
            this.smtpClient = smtpClient;
        }

        public ICredentialsByHost Credentials
        {
            get { return smtpClient.Credentials; }
            set { smtpClient.Credentials = value; }
        }

        public SmtpDeliveryMethod DeliveryMethod
        {
            get { return smtpClient.DeliveryMethod; }
            set { smtpClient.DeliveryMethod = value; }
        }

        public bool EnableSsl
        {
            get { return smtpClient.EnableSsl; }
            set { smtpClient.EnableSsl = value; }
        }

        public void Dispose()
        {
            smtpClient.Dispose();
        }

        public string Host
        {
            get { return smtpClient.Host; }
            set { smtpClient.Host = value; }
        }

        public string PickupDirectoryLocation
        {
            get { return smtpClient.PickupDirectoryLocation; }
            set { smtpClient.PickupDirectoryLocation = value; }
        }

        public int Port
        {
            get { return smtpClient.Port; }
            set { smtpClient.Port = value; }
        }

        public void Send(MailMessage message)
        {
            smtpClient.Send(message);
        }

        public bool UseDefaultCredentials
        {
            get { return smtpClient.UseDefaultCredentials; }
            set { smtpClient.UseDefaultCredentials = value; }
        }
    }
}