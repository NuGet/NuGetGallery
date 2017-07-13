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
    /// Takes in a string containing a markdown file and converts it to HTML.
    /// </summary>
    /// <param name="readMe">A string containing a markdown file's contents</param>
    /// <returns>A string containing the HTML version of the markdown</returns>
    private static string ConvertMarkDownToHTML(string readMe)
        {
            return CommonMark.CommonMarkConverter.Convert(readMe);
        }

    /// <summary>
    /// Takes in a Stream representing a readme file in markdown, converts it to HTML and 
    /// returns a Stream representing the HTML version of the readme.
    /// </summary>
    /// <param name="readMeMarkdownStream">Stream containing a readMe in markdown</param>
    /// <returns>A stream with the HTML version of the readMe</returns>
    public static Stream GetReadMeHTMLStream(Stream readMeMarkdownStream)
        {
            using (var reader = new StreamReader(readMeMarkdownStream))
            {
                string readMeHtml = ConvertMarkDownToHTML(reader.ReadToEnd());
                return GetStreamFromWritten(readMeHtml);
            }
        }

    /// <summary>
    /// Takes in a ReadMeRequest with a markdown ReadMe file, converts it to HTML
    /// and returns a stream with the data.
    /// </summary>
    /// <param name="readMeRequest">The readMe type and mardown file</param>
    /// <returns>A stream representing the ReadMe.html file</returns>
    public static Stream GetReadMeHTMLStream(ReadMeRequest readMeRequest)
        {
            return GetReadMeHTMLStream(GetReadMeStream(readMeRequest));
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
    public static Stream ReadMeUrlToFileStream(string readMeUrl)
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