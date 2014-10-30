using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;

namespace NuGet.Canton
{
    /// <summary>
    /// Non-thread safe cursor.
    /// </summary>
    public class CantonCursor
    {
        private readonly string _key;
        private DateTime _position;
        private List<string> _dependantCursors;
        private readonly CloudStorageAccount _account;
        private JObject _metadata;

        public CantonCursor(CloudStorageAccount account, string key)
        {
            if (account == null)
            {
                throw new ArgumentNullException("account");
            }

            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            _account = account;
            _metadata = new JObject();
            _dependantCursors = new List<string>(0);
            _key = key;

            Load().Wait();
        }

        private async Task Load()
        {
            CloudTableClient client = _account.CreateCloudTableClient();

            var table = client.GetTableReference(CantonConstants.CursorTable);
            var op = TableOperation.Retrieve<CursorEntry>("cursors", Key);
            var result = await table.ExecuteAsync(op);

            if (result.Result == null)
            {
                _position = CantonConstants.MinSupportedDateTime;
                await Save();
            }
            else
            {
                CursorEntry entry = (CursorEntry)result.Result;
                _position = entry.Position;

                if (!String.IsNullOrEmpty(entry.DependantCursors))
                {
                    _dependantCursors = new List<string>(entry.DependantCursors.Split('|'));
                }

                if (!String.IsNullOrEmpty(entry.Metadata))
                {
                    _metadata = JObject.Parse(entry.Metadata);
                }

                if (entry.LockId != Guid.Empty && entry.LockExpiration > DateTime.UtcNow)
                {
                    throw new Exception("Unable to use locked cursor");
                }
            }
        }

        /// <summary>
        /// Saves the cursor to storage. Make sure ALL items with this position have been completed first.
        /// </summary>
        public async Task Save()
        {
            var client = _account.CreateCloudTableClient();
            var table = client.GetTableReference(CantonConstants.CursorTable);

            CursorEntry entry = new CursorEntry(this);

            // TODO: Add locking
            var op = TableOperation.InsertOrReplace(entry);

            await table.ExecuteAsync(op);
        }

        public string Key
        {
            get
            {
                return _key;
            }
        }

        public DateTime Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
            }
        }

        public List<string> DependantCursors
        {
            get
            {
                return _dependantCursors;
            }
            set
            {
                _dependantCursors = value;
            }
        }

        public JObject Metadata
        {
            get
            {
                return _metadata;
            }
            set
            {
                _metadata = value;
            }
        }
    }
}
