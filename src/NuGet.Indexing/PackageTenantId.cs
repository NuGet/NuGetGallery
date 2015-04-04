using Lucene.Net.Documents;
using Lucene.Net.Index;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public static class PackageTenantId
    {
        public static IEnumerable<string> GetDistintTenantId(IndexReader reader)
        {
            HashSet<string> result = new HashSet<string>();

            for (int i = 0; i < reader.MaxDoc; i++)
            {
                if (reader.IsDeleted(i))
                {
                    continue;
                }

                Document document = reader[i];

                string tenantId = document.Get("TenantId");

                if (tenantId != null)
                {
                    result.Add(tenantId);
                }
            }

            return result;
        }
    }
}
