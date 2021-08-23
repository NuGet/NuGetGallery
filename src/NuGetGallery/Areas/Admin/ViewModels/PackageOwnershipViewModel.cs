using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public enum PackageOwnershipState
    {
        /// <summary>
        /// This user is an owner of the package and will not be modified.
        /// </summary>
        ExistingOwner,

        /// <summary>
        /// This user has an existing ownership request for the package and will not be modified.
        /// </summary>
        ExistingOwnerRequest,

        /// <summary>
        /// This user is already an owner of the package so no request needs to be sent.
        /// </summary>
        AlreadyOwner,

        /// <summary>
        /// This user already has an existing ownership request for the package so no request needs to be sent.
        /// </summary>
        AlreadyOwnerRequest,

        /// <summary>
        /// This user is not currently an owner but the requestor has access to this user so it will be immediately added as an owner.
        /// </summary>
        NewOwner,

        /// <summary>
        /// This user is not currently an owner so an ownership request will be sent.
        /// </summary>
        NewOwnerRequest,

        /// <summary>
        /// This user is currently an owner of the package and will be removed.
        /// </summary>
        RemoveOwner,

        /// <summary>
        /// This user currently has an ownership request which will be removed.
        /// </summary>
        RemoveOwnerRequest,

        /// <summary>
        /// This user is not currently an owner and does not have an ownership request. No change will occur for this package and user combination.
        /// </summary>
        RemoveNoOp,
    }

    public class PackageRegistrationOwnershipChangeModel
    {
        public PackageRegistrationOwnershipChangeModel(string id, bool requestorHasPermissions, IReadOnlyDictionary<string, PackageOwnershipState> usernameToState)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            RequestorHasPermissions = requestorHasPermissions;
            UsernameToState = usernameToState ?? throw new ArgumentNullException(nameof(usernameToState));
        }

        /// <summary>
        /// The package ID this ownership change relates to.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Whether or not the request has permissions to make ownership changes on this package.
        /// </summary>
        public bool RequestorHasPermissions { get; }

        /// <summary>
        /// The state of all current, added, and removed owners on this package registration.
        /// </summary>
        public IReadOnlyDictionary<string, PackageOwnershipState> UsernameToState { get; }
    }

    public class PackageOwnershipChangesInput
    {
        [Required(ErrorMessage = "You must provide at least one package ID.")]
        public string PackageIds { get; set; }

        [Required(ErrorMessage = "You must provide a requestor username.")]
        public string Requestor { get; set; }

        public string AddOwners { get; set; }

        public string RemoveOwners { get; set; }

        public string Message { get; set; }
    }

    public class PackageOwnershipChangesModel
    {
        public PackageOwnershipChangesModel(
            PackageOwnershipChangesInput input,
            IReadOnlyList<string> addOwners,
            IReadOnlyList<string> removeOwners,
            IReadOnlyList<PackageRegistrationOwnershipChangeModel> changes)
        {
            Input = input;
            AddOwners = addOwners;
            RemoveOwners = removeOwners;
            Changes = changes;
        }

        public PackageOwnershipChangesInput Input { get; }
        public string Requestor => Input.Requestor;
        public IEnumerable<string> PackageIds => Changes.Select(x => x.Id);
        public IReadOnlyList<string> AddOwners { get; }
        public IReadOnlyList<string> RemoveOwners { get; }
        public string Message => Input.Message;
        public IReadOnlyList<PackageRegistrationOwnershipChangeModel> Changes { get; }
    }
}