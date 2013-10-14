namespace NuGetGallery
{
    public class ConfirmationViewModel
    {
        public string UnconfirmedEmailAddress { get; set; }

        public bool ConfirmingNewAccount { get; set; }

        public bool SuccessfulConfirmation { get; set; }

        public bool SentEmail { get; set; }

        public bool WrongUsername { get; set; }

        public bool DuplicateEmailAddress { get; set; }
    }
}
