using System.Security.Principal;

namespace NuGetGallery.Services
{
    public class ActionRequiringAccountPermissions
    {
        public PermissionRole RequiredAccountPermissionRole { get; }

        public ActionRequiringAccountPermissions(PermissionRole requiredAccountPermissionRole)
        {
            RequiredAccountPermissionRole = requiredAccountPermissionRole;
        }

        public void CheckIsAllowed(User currentUser, User account)
        {
            if (!PermissionsService.IsActionAllowed())
        }

        public void CheckIsAllowed(IPrincipal currentPrincipal, User account)
        {

        }
    }
}