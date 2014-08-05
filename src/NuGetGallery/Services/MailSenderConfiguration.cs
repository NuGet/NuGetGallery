using System.Net;
using System.Net.Mail;

namespace NuGetGallery.Services
{
    public class MailSenderConfiguration
    {
        public ICredentialsByHost Credentials { get; set; }
        public SmtpDeliveryMethod? DeliveryMethod { get; set; }
        public bool? EnableSsl { get; set; }
        public string Host { get; set; }
        public string PickupDirectoryLocation { get; set; }
        public int? Port { get; set; }
        public bool? UseDefaultCredentials { get; set; }
    }
}