// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Net;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public static class SecureFindImpl
    {
        public static async Task Find(IOwinContext context, SecureSearcherManager searcherManager, string tenantId)
        {
            string id = context.Request.Query["id"];
            string version = context.Request.Query["version"];

            HttpStatusCode statusCode;
            JToken result;
            if (!string.IsNullOrEmpty(id))
            {
                if (string.IsNullOrEmpty(version))
                {
                    result = FindById(searcherManager, id, tenantId, context.Request.Uri.Scheme);
                }
                else
                {
                    result = FindByIdAndVersion(searcherManager, id, version, tenantId, context.Request.Uri.Scheme);
                }

                if (result == null)
                {
                    result = new JObject { { "error", "not found" } };
                    statusCode = HttpStatusCode.NotFound;
                }
                else
                {
                    statusCode = HttpStatusCode.OK;
                }
            }
            else
            {
                result = new JObject { { "error", "id is a required parameter" } };
                statusCode = HttpStatusCode.BadRequest;
            }

            await ServiceHelpers.WriteResponse(context, statusCode, result);
        }

        static JToken FindByIdAndVersion(SecureSearcherManager searcherManager, string id, string version, string tenantId, string scheme)
        {
            IndexSearcher searcher = searcherManager.Get();
            try
            {
                string analyzedId = id.ToLowerInvariant();
                string analyzedVersion = NuGetVersion.Parse(version).ToNormalizedString();

                BooleanQuery query = new BooleanQuery();
                query.Add(new BooleanClause(new TermQuery(new Term("FullId", analyzedId)), Occur.MUST));
                query.Add(new BooleanClause(new TermQuery(new Term("Version", analyzedVersion)), Occur.MUST));

                Filter filter = searcherManager.GetFilter(tenantId, new string [] { "http://schema.nuget.org/schema#ApiAppPackage" });

                TopDocs topDocs = searcher.Search(query, filter, 1);

                if (topDocs.TotalHits > 0)
                {
                    Uri registrationBaseAddress = searcherManager.RegistrationBaseAddress[scheme];
                    JObject obj = new JObject();
                    obj["registration"] = new Uri(registrationBaseAddress, string.Format("{0}/index.json", id.ToLowerInvariant())).AbsoluteUri;

                    Document document = searcher.Doc(topDocs.ScoreDocs[0].Doc);
                    ServiceHelpers.AddField(obj, document, "packageContent", "PackageContent");
                    ServiceHelpers.AddField(obj, document, "catalogEntry", "CatalogEntry");

                    return obj;
                }
                else
                {
                    return null;
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        static JToken FindById(SecureSearcherManager searcherManager, string id, string tenantId, string scheme)
        {
            IndexSearcher searcher = searcherManager.Get();
            try
            {
                string analyzedId = id.ToLowerInvariant();

                BooleanQuery query = new BooleanQuery();
                query.Add(new BooleanClause(new TermQuery(new Term("FullId", analyzedId)), Occur.MUST));

                Filter filter = searcherManager.GetFilter(tenantId, new string [] { "http://schema.nuget.org/schema#ApiAppPackage" });

                TopDocs topDocs = searcher.Search(query, 1000);

                if (topDocs.TotalHits > 0)
                {
                    Uri registrationBaseAddress = searcherManager.RegistrationBaseAddress[scheme];
                    JObject registrationObj = new JObject();
                    string registrationRelativeAddress = string.Format("{0}/index.json", id.ToLowerInvariant());
                    registrationObj["registration"] = new Uri(registrationBaseAddress, registrationRelativeAddress).AbsoluteUri;

                    JArray data = new JArray();
                    for (int i = 0; i < topDocs.ScoreDocs.Length; i++)
                    {
                        Document document = searcher.Doc(topDocs.ScoreDocs[i].Doc);

                        JObject versionObj = new JObject();
                        ServiceHelpers.AddField(versionObj, document, "version", "Version");
                        ServiceHelpers.AddField(versionObj, document, "packageContent", "PackageContent");
                        ServiceHelpers.AddField(versionObj, document, "catalogEntry", "CatalogEntry");

                        data.Add(versionObj);
                    }

                    registrationObj["data"] = data;

                    return registrationObj;
                }
                else
                {
                    return null;
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }
    }
}