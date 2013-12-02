using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Storage
{
    public interface IWriteOnlyTable<TEntity>
    {
        Task Upsert(TEntity entity);
        Task InsertOrIgnore(TEntity entity);
        Task Merge(TEntity entity);
    }
}
