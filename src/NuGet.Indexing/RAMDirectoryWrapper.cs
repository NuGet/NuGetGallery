using Lucene.Net.Store;
using System;

namespace NuGet.Indexing
{
    /// <summary>
    /// This class wraps the RAMDirectory so we can correct the return of FileModified function
    /// </summary>
    public class RAMDirectoryWrapper: RAMDirectory
    {
        public RAMDirectoryWrapper() : base() { }

        public RAMDirectoryWrapper(Directory seedDirectory) : base(seedDirectory) { }

        /// <summary>
        /// Returns the time (as a long) the named file was last modified in a Windows FileTime in UTC
        /// </summary>
        /// <param name="name">File Name</param>
        /// <returns>A long that represents a UTC Windows FileTime</returns>
        /// <remarks>The implementation here is to keep in line with the implementation in AzureDirectory for use in the <see cref="AzureDirectorySynchronizer"/>.
        /// See https://github.com/azure-contrib/AzureDirectory/blob/master/AzureDirectory/AzureDirectory.cs#L147 for AzureDirectory implementation</remarks>
        public override long FileModified(string name)
        {
            // The RAMDirectory implementation of FileModified creates a dateTime, converts to localTime, and then uses ticks to get milliseconds.
            // Undo this conversion here so we can get the file modified time in ticks back (accurate to the millisecond, we lose some precision here)
            // Then operate on this to get back a "standard" utc windows filetime.
            // See https://lucenenet.apache.org/docs/3.0.3/d7/df5/_r_a_m_directory_8cs_source.html line 114 for more details.

            var originalTicks = base.FileModified(name) * TimeSpan.TicksPerMillisecond;

            var newTime = new DateTime(originalTicks, DateTimeKind.Local);

            return newTime.ToUniversalTime().ToFileTimeUtc();
        }
    }
}
