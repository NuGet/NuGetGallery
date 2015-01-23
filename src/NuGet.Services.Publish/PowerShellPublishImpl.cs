using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using NuGet.Services.Metadata.Catalog;
using VDS.RDF.Parsing.Contexts;

namespace NuGet.Services.Publish
{
    public class PowerShellPublishImpl : PublishImpl
    {
        private bool _wasPsd1Present = false;
        private List<string> _missingRequiredFields = new List<string>();
        private Exception _exception;

        public PowerShellPublishImpl(IRegistrationOwnership registrationOwnership) : base(registrationOwnership)
        {
        }

        /// <summary>
        /// Checks whether the file is a top-level PowerShell module manifest from which we will extract metadata
        /// </summary>
        /// <param name="fullName">File name</param>
        /// <returns></returns>
        protected override bool IsMetadataFile(string fullName)
        {
            var isMetadataFile = (fullName.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase) && String.Equals(Path.GetFileName(fullName), fullName, StringComparison.OrdinalIgnoreCase));
            _wasPsd1Present = _wasPsd1Present || isMetadataFile;

            return isMetadataFile;
        }

        /// <summary>
        /// Extracts PowerShell metadata from psd1 stream
        /// </summary>
        /// <param name="fullname">File name</param>
        /// <param name="stream">Contents of module manifest</param>
        /// <returns>JSON metadata collection</returns>
        protected override JObject CreateMetadataObject(string fullname, Stream stream)
        {
            try
            {
                StreamReader reader = new StreamReader(stream);

                dynamic result = new JObject();

                Token[] tokens;
                ParseError[] errors;
                var scriptBlockAst = Parser.ParseInput(reader.ReadToEnd(), out tokens, out errors);

                var hashtableAst =
                    ((scriptBlockAst?.EndBlock?.Statements?[0] as PipelineAst)?.PipelineElements?[0] as
                        CommandExpressionAst)?.Expression as HashtableAst;
                var hashtable = PowerShellHashtableVisitor.GetHashtable(hashtableAst);

                VerifyRequiredFields(hashtable);

                result.Author = hashtable?["Author"];
                result.ModuleVersion = hashtable?["ModuleVersion"];
                result.CompanyName = hashtable?["CompanyName"];
                result.GUID = hashtable?["GUID"];
                result.PowerShellHostVersion = hashtable?["PowerShellHostVersion"];
                result.DotNetFrameworkVersion = hashtable?["DotNetFrameworkVersion"];
                result.CLRVersion = hashtable?["CLRVersion"];
                result.ProcessorArchitecture = hashtable?["ProcessorArchitecture"];
                result.CmdletsToExport = ConvertObjectToJArray(hashtable?["CmdletsToExport"]);
                result.FunctionsToExport = ConvertObjectToJArray(hashtable?["FunctionsToExport"]);
                result.DscResourcesToExport = ConvertObjectToJArray(hashtable?["DscResourcesToExport"]);
                var privateData = hashtable?["PrivateData"] as Hashtable;
                var psData = privateData?["PSData"] as Hashtable;
                result.LicenseUri = psData?["LicenseUri"];
                result.IconUri = psData?["IconUri"];
                result.Tags = ConvertObjectToJArray(psData?["Tags"]);
                result.ProjectUri = psData?["ProjectUri"];
                result.ReleaseNotes = psData?["ReleaseNotes"];

                return result;
            }
            catch (UnexpectedElementException ex)
            {
                _exception = ex;
                return null;
            }
        }

        private void VerifyRequiredFields(Hashtable hashtable)
        {
            if (String.IsNullOrEmpty(hashtable?["ModuleVersion"] as string))
            {
                _missingRequiredFields.Add("ModuleVersion");
            }

            if (String.IsNullOrEmpty(hashtable?["Author"] as string))
            {
                _missingRequiredFields.Add("Author");
            }

            if (String.IsNullOrEmpty(hashtable?["GUID"] as string))
            {
                _missingRequiredFields.Add("GUID");
            }
        }

        protected override Uri GetItemType()
        {
            return Schema.DataTypes.PowerShellPackage;
        }

        /// <summary>
        /// Creates a JArray from an object, if null creates an empty JArray
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        private object ConvertObjectToJArray(object o)
        {
            if (o != null)
            {
                return new JArray(o);
            }
            else
            {
                return new JArray();
            }
        }

        /// <summary>
        /// Rejects the module if no PowerShell module manifest was present
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="nupkgStream"></param>
        /// <returns></returns>
        protected override string Validate(IDictionary<string, JObject> metadata, Stream nupkgStream)
        {
            if (!_wasPsd1Present)
            {
                return "A psd1 PowerShell module manifest was missing.";
            }

            if (_missingRequiredFields.Count > 0)
            {
                return String.Format("The psd1 PowerShell module manifest is missing required fields: {0}.", String.Join(", ", _missingRequiredFields));
            }

            return _exception?.Message;
        }
    }
}