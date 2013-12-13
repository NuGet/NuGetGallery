using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Storage
{
    public struct TableStorageKey : IEquatable<TableStorageKey>
    {
        public string PartitionKey { get; private set; }
        public string RowKey { get; private set; }

        public TableStorageKey(string partitionKey, string rowKey) : this()
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public override bool Equals(object obj)
        {
            return Equals((TableStorageKey)obj);
        }

        public bool Equals(TableStorageKey other)
        {
            return String.Equals(PartitionKey, other.PartitionKey, StringComparison.Ordinal) &&
                String.Equals(RowKey, other.RowKey, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(PartitionKey)
                .Add(RowKey)
                .CombinedHash;
        }
    }
}
