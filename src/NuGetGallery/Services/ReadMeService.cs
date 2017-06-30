using System.IO;
using System.Net;
using System.Web;
using System;
using NuGetGallery.RequestModels;
using System.Text.RegularExpressions;

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
    public static Stream GetReadMeStream(ReadMeRequest formData)
        {
            // Uploaded ReadMe file
            if (formData.ReadMeFile != null)
            {
                return GetStreamFromFile(formData.ReadMeFile);
            }
            // ReadMe Url
            else if (formData.ReadMeUrl != null)
            {
                string readMeUrl = GetReadMeUrlFromRepositoryUrl(formData.ReadMeUrl);
                return ReadMeUrlToFileStream(readMeUrl);
            }
            //ReadMe Written
            else
            {
                return GetStreamFromWritten(formData.ReadMeWritten);
            }
            
        }

        /// <summary>
        /// Takes in the repository URL and parses it, returning a link directly to the Readme.md file.
        /// </summary>
        /// <param name="repositoryUrl">A link to the repository</param>
        /// <returns>A link to the raw readme.md file</returns>
    public static string GetReadMeUrlFromRepositoryUrl(string repositoryUrl)
        {
            if (!repositoryUrl.Contains("http://") && !repositoryUrl.Contains("https://"))
            {
                repositoryUrl = "http://" + repositoryUrl;
            }
            Uri repositoryUri = new Uri(repositoryUrl);
            Regex regex = new Regex(@"(http(s)?:\/\/)?([a-zA-Z0-9]+\.)?github\.com\/([a-zA-Z0-9])+\/([a-zA-Z0-9])+(\/)?$");
            if (repositoryUri.Host.Contains("github.com") && regex.IsMatch(repositoryUrl))
            {
                if (!repositoryUrl.EndsWith("/"))
                {
                    repositoryUrl += "/";
                }
                return repositoryUrl + "blob/master/README.md";
            } else
            {
                return "";
            }
        }

        /// <summary>
        /// Converts a ReadMe's url to a file stream.
        /// </summary>
        /// <param name="readMeUrl">A link to the raw ReadMe.md file</param>
        /// <returns>A stream to allow the file to be read</returns>
    private static Stream ReadMeUrlToFileStream(string readMeUrl)
        {
            var webRequest = WebRequest.Create(readMeUrl);
            var response = webRequest.GetResponse();
            return response.GetResponseStream();
        }

    public static Stream GetStreamFromWritten(string writtenText)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(writtenText);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

    public static Stream GetStreamFromFile(HttpPostedFileBase file)
        {
            return file.InputStream;
        }
    }
}