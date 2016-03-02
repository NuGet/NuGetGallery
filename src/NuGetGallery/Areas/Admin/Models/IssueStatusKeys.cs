namespace NuGetGallery.Areas.Admin.Models
{
    internal class IssueStatusKeys
    {
        internal const int New = 0;
        internal const int Working = 1;
        internal const int WaitingForCustomer = 2;
        internal const int Resolved = 3;

        /// <summary>
        /// Does not exist in database.
        /// This is a logical key to represent any unresolved issue status.
        /// </summary>
        /// <remark>
        /// If one day we have 99 real issue status keys in the db, we can easily bump this one up in code.
        /// </remark>
        internal const int Unresolved = 99;
    }
}