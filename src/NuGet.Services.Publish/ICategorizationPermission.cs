
namespace NuGet.Services.Publish
{
    public interface ICategorizationPermission
    {
        bool IsAllowedToSpecifyCategory(string id);
    }
}