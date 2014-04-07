using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver.Metadata
{
    public class Gallery : IGallery
    {
        IDictionary<string, Registration> _registrations;

        public Gallery()
        {
            _registrations = new Dictionary<string, Registration>();
        }

        public async Task<Registration> GetRegistration(string id)
        {
            return await Task.Run<Registration>(() => {

                Registration registration;
                if (_registrations.TryGetValue(id, out registration))
                {
                    return registration;
                }
                throw new Exception(string.Format("{0} is not available from this gallery", id));
            });
        }

        public void AddPackage(Package package)
        {
            Registration registration;
            if (!_registrations.TryGetValue(package.Id, out registration))
            {
                registration = new Registration { Id = package.Id };
                _registrations.Add(package.Id, registration);
            }
            registration.Packages.Add(package);
        }

        public void WriteTo(TextWriter writer)
        {
            foreach (Registration registration in _registrations.Values)
            {
                registration.WriteTo(writer);
            }
        }
    }
}
