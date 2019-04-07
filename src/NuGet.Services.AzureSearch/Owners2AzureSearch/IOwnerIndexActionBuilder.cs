using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    public interface IOwnerIndexActionBuilder
    {
        Task<IndexActions> UpdateOwnersAsync(string packageId, string[] owners);
    }
}