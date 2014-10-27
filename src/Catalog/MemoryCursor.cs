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

        public override Task Load()
        {
            return Task.Run(() => { });
        }

        public override Task Save()
        {
            return Task.Run(() => { });
        }
    }
}
