using System.ComponentModel.DataAnnotations;
using System.Web.Routing;

namespace NuGetGallery
{
    public class SignInRequest
    {
        private static readonly RouteValueDictionary _defaultPostTo = new RouteValueDictionary()
    {
        {"controller", "Authentication"},
        {"action", "LogOn"},
        {"area", ""}
    }; 

        [ScaffoldColumn(false)]
        public RouteValueDictionary PostToRoute { get; set; }
        [ScaffoldColumn(false)]
        public string SubmitButtonTitle { get; set; }
        [ScaffoldColumn(false)]
        public bool ShowLostPassword { get; set; }

        [Required]
        [Display(Name = "Username or Email")]
        [Hint("Enter your username or email address.")]
        public string UserNameOrEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Hint("Passwords must be at least 7 characters long.")]
        public string Password { get; set; }

        public SignInRequest()
        {
            SubmitButtonTitle = "Log On";
            ShowLostPassword = true;
            PostToRoute = _defaultPostTo;
        }
    }
}