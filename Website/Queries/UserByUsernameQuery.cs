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
            IQueryable<User> qry = _entities.Users;

            if (includeRoles)
            {
                qry = qry.Include(u => u.Roles);
            }

            return qry.SingleOrDefault(u => u.Username == username);
        }
    }
}