using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;

namespace NuGetGallery
{
    public static class TokenHelper
    {
        /// <summary>
        /// Gets credential using the Service Principal.  If the resource is in a different tenant, this is how to access it.
        /// The ServicePrincipal needs to be a "Storage Table/Blob/Queue Data Contributor" role on the storage account.  Owner isn't enough.
        /// </summary>
        /// <returns>ClientCertificatCredential to be used to communicate with Storage.</returns>
        public static ClientCertificateCredential GetCredentialUsingServicePrincipal(string appID, string subjectAlternativeName, string tenantId, string authorityHost)
        {
            X509Certificate2 clientCert;

            // Azure.Identity library doesn't support referencing cert by Store + Subject name, so we need to load it ourselves.
            using (X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);

                X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindBySubjectName, subjectAlternativeName, false);

                if (certs.Count == 0)
                {
                    throw new InvalidOperationException($"Unable to find Synthetics service principal client cert with subject name '{subjectAlternativeName}'");
                }

                // As an exception to comment in GetKeyVaultCertsAsync method, this X509Certificate2 object does not have to be disposed
                // because it is referencing a platform certificate from CurrentUser certificate store, so no temporary files are created for this object.
                clientCert = certs.Cast<X509Certificate2>().OrderBy(x => x.NotAfter).Last();
            }

            return new ClientCertificateCredential(tenantId, appID, clientCert, new ClientCertificateCredentialOptions { AuthorityHost = new Uri(authorityHost), SendCertificateChain = true });
        }
    }
}
