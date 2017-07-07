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
    /// Returns if a given package has a ReadMe.
    /// </summary>
    /// <param name="formData">A ReadMeRequest with the ReadMe data from the form.</param>
    /// <returns>Whether there is a ReadMe to upload.</returns>
    public static Boolean HasReadMe(ReadMeRequest formData)
        {
            if (formData == null)
            {
                return false;
            }
            else if (formData.ReadMeType == null)
            {
                return false;
            } else
            {
                switch (formData.ReadMeType)
                {
                    case "Url":
                        return formData.ReadMeUrl != null && formData.ReadMeUrl != "";
                    case "File":
                        return formData.ReadMeFile != null;
                    case "Written":
                        return formData.ReadMeWritten != null && formData.ReadMeWritten != "";
                    default: return false;
                }
            }
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
            switch (formData.ReadMeType)
            {
                case "Url":
                    return ReadMeUrlToFileStream(formData.ReadMeUrl);
                case "File":
                    return GetStreamFromFile(formData.ReadMeFile);
                default: //Written
                    return GetStreamFromWritten(formData.ReadMeWritten);
            }          
        }

        /// <summary>
        /// Converts a ReadMe's url to a file stream.
        /// </summary>
        /// <param name="readMeUrl">A link to the raw ReadMe.md file</param>
        /// <returns>A stream to allow the file to be read</returns>
    private static Stream ReadMeUrlToFileStream(string readMeUrl)
        {
            if (readMeUrl.IndexOf("http://") != 0 && readMeUrl.IndexOf("https://") != 0)
            {
                readMeUrl = "http://" + readMeUrl;
            }
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