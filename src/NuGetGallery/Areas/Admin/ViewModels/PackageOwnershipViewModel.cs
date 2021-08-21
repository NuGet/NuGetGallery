using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public enum PackageOwnershipStateType
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
        /// This user is not currently an owner so an ownership request will be sent.
        /// </summary>
        NewOwnerRequest,

        /// <summary>
        /// This user is currently an owner of the package and will be removed.
        /// </summary>
        RemoveOwner,

        /// <summary>
        /// This user current has an ownership request which will be removed.
        /// </summary>
        RemoveOwnerRequest,
    }

    public class PackageOwnershipState
    {
        public PackageOwnershipState(string username, PackageOwnershipStateType type)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Type = type;
        }

        public string Username { get; }
        public PackageOwnershipStateType Type { get; }
    }


    public class PackageRegistrationOwnershipChangeViewModel
    {
        public PackageRegistrationOwnershipChangeViewModel(string id, bool requestorHasPermissions, IReadOnlyList<PackageOwnershipState> ownershipStates)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            RequestorHasPermissions = requestorHasPermissions;
            OwnershipStates = ownershipStates ?? throw new ArgumentNullException(nameof(ownershipStates));
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
        public IReadOnlyList<PackageOwnershipState> OwnershipStates { get; }
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

    public class PackageOwnershipChangesViewModel
    {
        public PackageOwnershipChangesViewModel(
            PackageOwnershipChangesInput input,
            string requestorUsername,
            string message,
            IReadOnlyList<PackageRegistrationOwnershipChangeViewModel> packageRegistrationOwnershipChanges)
        {
            Input = input ?? throw new ArgumentNullException(nameof(requestorUsername));
            RequestorUsername = requestorUsername ?? throw new ArgumentNullException(nameof(requestorUsername));
            Message = message;
            PackageRegistrationOwnershipChanges = packageRegistrationOwnershipChanges ?? throw new ArgumentNullException(nameof(packageRegistrationOwnershipChanges));
        }

        public PackageOwnershipChangesInput Input { get; }
        public string RequestorUsername { get; }
        public string Message { get; }
        public IReadOnlyList<PackageRegistrationOwnershipChangeViewModel> PackageRegistrationOwnershipChanges { get; }
    }
}