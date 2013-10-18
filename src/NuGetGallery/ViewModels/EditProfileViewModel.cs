using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class EditProfileViewModel
    {
        public string Username { get; set; }

        public string EmailAddress { get; set; }

        public string PendingNewEmailAddress { get; set; }

        [Display(Name = "Receive Email Notifications")]
        public bool EmailAllowed { get; set; }
    }
}