namespace NuGetGallery.Areas.Admin.Models
{
    public class IssueStatusKeys
    {
        public const int New = 0;
        public const int Working = 1;
        public const int WaitingForCustomer = 2;
        public const int Resolved = 3;

        /// <summary>
        /// Does not exist in database.
        /// This is a logical key to represent any unresolved issue status.
        /// </summary>
        /// <remark>
        /// If one day we have 99 real issue status keys in the db, we can easily bump this one up in code.
        /// </remark>
        public const int Unresolved = 99;
    }
}