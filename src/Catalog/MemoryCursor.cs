using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class MemoryCursor : ReadWriteCursor
    {
        public static MemoryCursor Min = new MemoryCursor(DateTime.MinValue.ToUniversalTime());
        public static MemoryCursor Max = new MemoryCursor(DateTime.MaxValue.ToUniversalTime());

        public MemoryCursor(DateTime value)
        {
            Value = value;
        }

        public MemoryCursor()
        {
            // TODO: Complete member initialization
        }

        public override Task Load()
        {
            return Task.FromResult(0);
        }

        public override Task Save()
        {
            return Task.FromResult(0);
        }
    }
}
