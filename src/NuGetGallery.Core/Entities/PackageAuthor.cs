namespace NuGetGallery
{
    public class PackageAuthor : IEntity
    {
        public Package Package { get; set; }
        public int PackageKey { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        public string Name { get; set; }
        public int Key { get; set; }
    }
}