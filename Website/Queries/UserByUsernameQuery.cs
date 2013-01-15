using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public interface IUserByUsernameQuery
    {
        User Execute(
            string username,
            bool includeRoles = true);
    }

    public class UserByUsernameQuery : IUserByUsernameQuery
    {
        private readonly IEntitiesContext _entities;

        public UserByUsernameQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public User Execute(
            string username,
            bool includeRoles = true)
        {
            IQueryable<User> query = _entities.Users;

            if (includeRoles)
            {
                query = query.Include(u => u.Roles);
            }

            return query.SingleOrDefault(u => u.Username == username);
        }
    }
}