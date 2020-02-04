# NuGet.VerifyMicrosoftPackage

This project contains a tool which can be used to verify Microsoft package metadata before the packages are pushed
to nuget.org. The goal is for Microsoft teams to find packaging problems earlier in the game and avoid painful
release-time package fix-ups.

There are some requirements for packages released by Microsoft teams. This tool allows you to run the verification on
the client side before pushing to nuget.org so that problems can be caught earlier.

Today, this tool only verifies the Microsoft-specific metadata requirements of the package, i.e. information in the
.nuspec. Other requirements like author signing are implemented by the tool. For the author signature verification
case, you can use the following nuget.exe command:

```
.\nuget.exe verify *.nupkg -Signatures -CertificateFingerprint 3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE
```

Other missing verifications include:

1. Package size limits
1. DLLs should have authenticode
1. nuget.org-specific validations like pre-release labels can't start with a number

We hope to add more validations to the tool in the mid-term and move this tool's validations to `nuget.exe verify` in
the long term.

## Script

You can use the following PowerShell script to download and run the verification tool.

```powershell
$url = "https://raw.githubusercontent.com/NuGet/NuGetGallery/master/src/VerifyMicrosoftPackage/verify.ps1"
Invoke-WebRequest $url -OutFile verify.ps1
.\verify.ps1 *.nupkg
```

The command line arguments provided to `verify.ps1` are passed through to a command line tool
(`NuGet.VerifyMicrosoftPackage.exe`) downloaded during the execution of the script. The help text for this tool is
below.

## Help Text

```
NuGet.VerifyMicrosoftPackage 0.0.1

Usage: NuGet.VerifyMicrosoftPackage [arguments] [options]

Arguments:
  PATHS  One or more file paths to a package (.nupkg).

Options:
  -v | --version            Show version information.
  -? | -h | --help          Show help information.
  --recursive               Evaluate wildcards recursively into child directories.
  --rule-set                A path to a JSON rule set file. See the default below.
  --write-default-rule-set  Write the default rule set to the provided --rule-set file path.

This tool determines if a .nupkg meets the metadata requirements for Microsoft packages
on nuget.org. Relative paths and wildcards in the file name are supported. Globbing and
wildcards in the directory are not supported.

The default rule set used for validation is the following:

Readable .NET Name      | JSON Name
----------------------- | ----------
AllowedAuthors          | authors
AllowedCopyrightNotices | copy
ErrorMessageFormat      | error
IsLicenseUrlRequired    | licUrlReq
IsProjectUrlRequired    | projUrlReq
RequiredCoOwnerUsername | u

If question marks ('?') or weird characters appear below, consider using --write-default-rule-set.

{
  "u": "Microsoft",
  "copy": [
    "(c) Microsoft Corporation. All rights reserved.",
    "&#169; Microsoft Corporation. All rights reserved.",
    "© Microsoft Corporation. All rights reserved.",
    "© Microsoft Corporation. Tüm hakları saklıdır.",
    "© Microsoft Corporation. Todos os direitos reservados.",
    "© Microsoft Corporation. Alle Rechte vorbehalten.",
    "© Microsoft Corporation. Všechna práva vyhrazena.",
    "© Microsoft Corporation. Todos los derechos reservados.",
    "© Microsoft Corporation. Wszelkie prawa zastrzeżone.",
    "© Microsoft Corporation. Tous droits réservés.",
    "© Microsoft Corporation。 保留所有权利。",
    "© Microsoft Corporation. Tutti i diritti riservati.",
    "© корпорация Майкрософт. Все права защищены.",
    "© Microsoft Corporation。 著作權所有，並保留一切權利。"
  ],
  "authors": [
    "Microsoft"
  ],
  "licUrlReq": true,
  "projUrlReq": true,
  "error": "The package is not compliant with metadata requirements for Microsoft packages on NuGet.org. Go to https://aka.ms/Microsoft-NuGet-Compliance for more information.\r\nPolicy violations: {0}"
}
```