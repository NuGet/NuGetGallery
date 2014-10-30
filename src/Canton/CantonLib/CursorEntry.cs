using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class CursorEntry : TableEntity
    {
        private const string _partition = "cursors";

        /// <summary>
        /// Position of the cursor.
        /// </summary>
        public DateTime Position { get; set; }

        /// <summary>
        /// Unique id given by the caller to ensure they have the lock.
        /// </summary>
        public Guid LockId { get; set; }

        /// <summary>
        /// Date the lock expires.
        /// </summary>
        public DateTime LockExpiration { get; set; }

        /// <summary>
        /// Additional metadata for this cursor in json format.
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// Pipe delimited list of dependant cursors.
        /// </summary>
        public string DependantCursors { get; set; }

        public CursorEntry(CantonCursor cursor)
            : base(_partition, cursor.Key)
        {
            Position = cursor.Position;
            LockExpiration = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
            LockId = Guid.Empty;
            Metadata = cursor.Metadata.ToString();
            DependantCursors = String.Join("|", cursor.DependantCursors);
        }

        public CursorEntry()
            : base()
        {
            PartitionKey = Guid.NewGuid().ToString();
            RowKey = Guid.NewGuid().ToString();
        }
    }
}
