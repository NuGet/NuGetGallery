
using System.Threading.Tasks;
namespace NuGet.Services.Publish
{
    public interface ICategorizationPermission
    {
        Task<bool> IsAllowedToSpecifyCategory(string id);
    }
}