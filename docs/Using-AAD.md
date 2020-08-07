# Using Azure Active Directory authentication

You can configure the NuGetGallery to use Azure Active Directory to manage your accounts.

## Create an Azure Active Directory application registration

1. On the portal, open the ["App registrations" blade](https://ms.portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationsListBlade). Click on "New registration".
1. For "Supported account types", select "Accounts in any organizational directory (Any Azure AD directory - Multitenant) and personal Microsoft accounts (e.g. Skype, Xbox)"

> ⚠ **NOTE**: This authenticates using the v2 common workflow and any AAD/personal account will be able to create an account and publish packages to your on-prem gallery. If you want to authenticate with only specific directory (the "Single tenant" option), you will need to make a code change later.

1. For "Redirect URI", select "Web" and input `https://<Your domain>/users/account/authenticate/return`
1. Press "Register" to create the application
1. On the "Overview" pane, note down the "Application (client) ID"
1. Navigate to the "Authentication" pane. Under the "Implicit grant" section, enable "ID tokens". Press "Save".
1. Navigate to "Certificates & secrets" pane and create a new client secret. Note the value of your client secret

## Configure the Gallery

Let's configure the NuGetGallery to use your Azure Active Directory application registration:

1. Open the NuGetGallery solution in Visual Studio
1. Modify the "Web.config" file in the NuGetGallery project
1. Modify the `Auth.AzureActiveDirectoryV2.Enabled` setting to `true`
1. Modify the `Auth.AzureActiveDirectoryV2.ClientId` setting to the application ID you copied earlier
1. Modify the `Auth.AzureActiveDirectoryV2.ClientSecret` setting to the client secret you copied earlier

If when you created your Azure Active Directory app registration you selected the "Single tenant" option, update [`AzureActiveDirectoryV2AuthenticatorConfiguration`](https://github.com/NuGet/NuGetGallery/blob/0659deed143f0b58868fa691ec22f46f1d57cba6/src/NuGetGallery.Services/Authentication/Providers/AzureActiveDirectoryV2/AzureActiveDirectoryV2AuthenticatorConfiguration.cs#L53) to set the authority tenant ID to your AAD Tenant ID:

```csharp
openIdOptions.Authority = String.Format(CultureInfo.InvariantCulture, AzureActiveDirectoryV2Authenticator.Authority, "<Your AAD Tenant ID>");
```