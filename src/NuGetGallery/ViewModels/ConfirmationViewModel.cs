namespace NuGetGallery
{
    public class ConfirmationViewModel
    {
        public string DoAction { get; set; }

        public string UnconfirmedEmailAddress { get; set; }

        public bool ConfirmingNewAccount { get; set; }

        public bool SuccessfulConfirmation { get; set; }

        public bool SentEmail { get; set; }
    }
}