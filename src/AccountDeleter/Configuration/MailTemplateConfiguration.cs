

namespace NuGetGallery.AccountDeleter
{
    public class MailTemplateConfiguration
    {
        /// <summary>
        /// Subject template for mail. Filled in using a templater.
        /// </summary>
        public string SubjectTemplate { get; set; }

        /// <summary>
        /// Message template for mail. Filled in using a templater.
        /// </summary>
        public string MessageTemplate { get; set; }
    }
}
