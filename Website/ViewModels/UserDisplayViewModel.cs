using System.Configuration;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class UserDisplayViewModel
    {
        public bool AllowUserRegistrations
        {
            get
            {
                return string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings.Get("Ldap:Uri"));
            }
        }

        public string UsernameOrDisplayName
        {
            get
            {
                var user = DependencyResolver.Current.GetService<IUserService>().FindByUsername(HttpContext.Current.User.Identity.Name);
                return !string.IsNullOrWhiteSpace(user.DisplayName)
                           ? user.DisplayName
                           : user.Username;
            } 
        } 
    }
}