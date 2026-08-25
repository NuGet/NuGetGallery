// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

static class ConfigureValidationTool
{
	public static int Run(string[] args)
	{
		if (args.Length != 2)
		{
			throw new ArgumentException("Expected Gallery and validation orchestrator configuration paths.");
		}

		var serviceBusHostName = Environment.GetEnvironmentVariable("SERVICE_BUS_HOST_NAME");
		if (string.IsNullOrWhiteSpace(serviceBusHostName))
		{
			throw new InvalidOperationException("SERVICE_BUS_HOST_NAME is not set.");
		}

		serviceBusHostName = serviceBusHostName.Trim();
		UpdateGalleryConfiguration(args[0], serviceBusHostName);
		UpdateOrchestratorConfiguration(args[1], serviceBusHostName);
		return 0;
	}

	private static void UpdateGalleryConfiguration(string path, string serviceBusHostName)
	{
		var document = XDocument.Load(path);
		SetAppSetting(document, "Gallery.AsynchronousPackageValidationEnabled", bool.TrueString);
		SetAppSetting(document, "Gallery.BlockingAsynchronousPackageValidationEnabled", bool.TrueString);
		SetAppSetting(document, "AzureServiceBus.Validation.ConnectionString", serviceBusHostName);
		SetAppSetting(document, "AzureServiceBus.SymbolsValidation.ConnectionString", serviceBusHostName);
		document.Save(path);
	}

	private static void SetAppSetting(XDocument document, string key, string value)
	{
		var setting = document
			.Root?
			.Elements("add")
			.SingleOrDefault(element => string.Equals((string?)element.Attribute("key"), key, StringComparison.Ordinal));
		if (setting == null)
		{
			throw new InvalidOperationException($"The Gallery setting '{key}' was not found.");
		}

		setting.SetAttributeValue("value", value);
	}

	private static void UpdateOrchestratorConfiguration(string path, string serviceBusHostName)
	{
		var root = JsonNode.Parse(File.ReadAllText(path))
			?? throw new InvalidOperationException("The validation orchestrator configuration is empty.");

		SetJsonValue(root, "ServiceBus", "ConnectionString", serviceBusHostName);
		SetJsonValue(root, "Email", "ServiceBus", "ConnectionString", serviceBusHostName);
		File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
	}

	private static void SetJsonValue(JsonNode root, string section, string property, string value)
	{
		var sectionNode = root[section]
			?? throw new InvalidOperationException($"The orchestrator section '{section}' was not found.");
		sectionNode[property] = value;
	}

	private static void SetJsonValue(JsonNode root, string section, string subsection, string property, string value)
	{
		var subsectionNode = root[section]?[subsection]
			?? throw new InvalidOperationException($"The orchestrator section '{section}:{subsection}' was not found.");
		subsectionNode[property] = value;
	}
}
