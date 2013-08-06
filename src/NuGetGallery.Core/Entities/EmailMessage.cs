namespace NuGetGallery
{
    public class EmailMessage : IEntity
    {
        public EmailMessage()
            : this(null, null)
        {
        }

        public EmailMessage(
            string subject,
            string body)
        {
            Body = body;
            Subject = subject;
        }

        public string Body { get; set; }
        public User FromUser { get; set; }
        public int? FromUserKey { get; set; }
        public bool Sent { get; set; }
        public string Subject { get; set; }
        public User ToUser { get; set; }
        public int ToUserKey { get; set; }
        public int Key { get; set; }
    }
}