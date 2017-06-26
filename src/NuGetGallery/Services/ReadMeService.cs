using NuGetGallery.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;

namespace NuGetGallery.Services
{
    public class ReadMeService
    {
        public ReadMeService()
        {

        }
        
        /// <summary>
        /// Finds the highest priority ReadMe file stream and returns it. Highest priority is an uploaded file,
        /// then a repository URL inputted via the website, then a repository URL entered through the nuspec.
        /// </summary>
        /// <param name="formData">The current package's form data submitted through the verify page</param>
        /// <param name="packageMetadata">The package metadata from the nuspec file</param>
        /// <returns>A stream with the encoded ReadMe file</returns>
        public Stream GetReadMeStream(VerifyPackageRequest formData, PackageMetadata packageMetadata)
        {
            if (formData.ReadMe != null)
            {
                return formData.ReadMe[0].InputStream;
            }
            else if (formData.Edit.RepositoryUrl != null)
            {
                string readMeUrl = ParseRepositoryUrl(formData.Edit.RepositoryUrl);
                return ReadMeUrlToFileStream(readMeUrl);
            }
            else
            {
                string readMeUrl = ParseRepositoryUrl(packageMetadata.RepoUrl.ToEncodedUrlStringOrNull());
                return ReadMeUrlToFileStream(packageMetadata.RepoUrl.ToEncodedUrlStringOrNull());
            }
            
        }

        /// <summary>
        /// Takes in the repository URL and parses it, returning a link directly to the Readme.md file.
        /// </summary>
        /// <param name="repositoryUrl">A link to the repository</param>
        /// <returns>A link to the raw readme.md file</returns>
        private string ParseRepositoryUrl(string repositoryUrl)
        {
            return repositoryUrl;
        }

        /// <summary>
        /// Converts a ReadMe's url to a file stream.
        /// </summary>
        /// <param name="readMeUrl">A link to the raw ReadMe.md file</param>
        /// <returns>A stream to allow the file to be read</returns>
        private Stream ReadMeUrlToFileStream(string readMeUrl)
        {
            var webRequest = WebRequest.Create(readMeUrl);
            using (var response = webRequest.GetResponse())
            using (var content = response.GetResponseStream())
                return content;
        }
    }
}