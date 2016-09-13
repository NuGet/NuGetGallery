using AnglicanGeek.MarkdownMailer;
using NuGetGallery.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;

namespace NuGetGallery.Configuration
{
    public static class MailSenderFactory
    {
        public static async Task<IMailSender> Create(IGalleryConfigurationService configService)
        {
            var configSmtpUri = (await configService.GetCurrent()).SmtpUri;
            if (configSmtpUri != null && configSmtpUri.IsAbsoluteUri)
            {
                var smtpUri = new SmtpUri(configSmtpUri);

                var mailSenderConfiguration = new MailSenderConfiguration
                {
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Host = smtpUri.Host,
                    Port = smtpUri.Port,
                    EnableSsl = smtpUri.Secure
                };

                if (!string.IsNullOrWhiteSpace(smtpUri.UserName))
                {
                    mailSenderConfiguration.UseDefaultCredentials = false;
                    mailSenderConfiguration.Credentials = new NetworkCredential(
                        smtpUri.UserName,
                        smtpUri.Password);
                }

                return new MailSender(mailSenderConfiguration);
            }
            else
            {
                var mailSenderConfiguration = new MailSenderConfiguration
                {
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                    PickupDirectoryLocation = HostingEnvironment.MapPath("~/App_Data/Mail")
                };

                return new MailSender(mailSenderConfiguration);
            }
        }
    }
}