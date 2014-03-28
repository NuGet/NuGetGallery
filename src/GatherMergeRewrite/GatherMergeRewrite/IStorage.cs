using JsonLD.Core;
using JsonLDIntegration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading.Tasks;
using VDS.RDF;

namespace GatherMergeRewrite
{
    public interface IStorage
    {
        Task Save(string contentType, string name, string content);
        Task<string> Load(string name);
    }
}
