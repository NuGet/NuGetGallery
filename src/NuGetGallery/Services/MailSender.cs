using System;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using MarkdownSharp;

namespace NuGetGallery.Services
{
    public class MailSender : IMailSender
    {
        readonly ISmtpClient smtpClient;

        public MailSender()
            : this(new SmtpClientWrapper(new SmtpClient()), null)
        {
        }

        public MailSender(MailSenderConfiguration configuration)
            : this(new SmtpClientWrapper(new SmtpClient()), configuration)
        {
        }

        public MailSender(SmtpClient smtpClient)
            : this(new SmtpClientWrapper(smtpClient), null)
        {
        }

        internal MailSender(
            ISmtpClient smtpClient,
            MailSenderConfiguration configuration)
        {
            if (smtpClient == null)
                throw new ArgumentNullException("smtpClient");

            if (configuration != null)
                ConfigureSmtpClient(smtpClient, configuration);

            this.smtpClient = smtpClient;
        }

        static internal void ConfigureSmtpClient(
            ISmtpClient smtpClient,
            MailSenderConfiguration configuration)
        {
            if (configuration.Host != null)
                smtpClient.Host = configuration.Host;
            if (configuration.Port.HasValue)
                smtpClient.Port = configuration.Port.Value;
            if (configuration.EnableSsl.HasValue)
                smtpClient.EnableSsl = configuration.EnableSsl.Value;
            if (configuration.DeliveryMethod.HasValue)
                smtpClient.DeliveryMethod = configuration.DeliveryMethod.Value;
            if (configuration.UseDefaultCredentials.HasValue)
                smtpClient.UseDefaultCredentials = configuration.UseDefaultCredentials.Value;
            if (configuration.Credentials != null)
                smtpClient.Credentials = configuration.Credentials;
            if (configuration.PickupDirectoryLocation != null)
                smtpClient.PickupDirectoryLocation = configuration.PickupDirectoryLocation;
        }

        public void Send(
            string fromAddress,
            string toAddress,
            string subject,
            string markdownBody)
        {
            Send(
                new MailAddress(fromAddress),
                new MailAddress(toAddress),
                subject,
                markdownBody);
        }

        public void Send(
            MailAddress fromAddress,
            MailAddress toAddress,
            string subject,
            string markdownBody)
        {
            var mailMessage = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = markdownBody
            };

            Send(mailMessage);
        }

        public void Send(MailMessage mailMessage)
        {
            if (smtpClient.DeliveryMethod == SmtpDeliveryMethod.SpecifiedPickupDirectory
                && !Directory.Exists(smtpClient.PickupDirectoryLocation))
                Directory.CreateDirectory(smtpClient.PickupDirectoryLocation);

            string markdownBody = mailMessage.Body;
            string htmlBody = new Markdown().Transform(markdownBody);

            AlternateView textView = AlternateView.CreateAlternateViewFromString(
                markdownBody,
                null,
                MediaTypeNames.Text.Plain);
            mailMessage.AlternateViews.Add(textView);

            AlternateView htmlView = AlternateView.CreateAlternateViewFromString(
                htmlBody,
                null,
                MediaTypeNames.Text.Html);
            mailMessage.AlternateViews.Add(htmlView);

            smtpClient.Send(mailMessage);
        }

        public void Dispose()
        {
            smtpClient.Dispose();
        }
    }
}