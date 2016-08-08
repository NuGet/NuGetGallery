SELECT	[PackageRegistrations].[Id] 'Id',
		[Users].[UserName] 'UserName'
FROM	[PackageRegistrations] (NOLOCK)

INNER JOIN	[PackageRegistrationOwners] (NOLOCK)
ON			[PackageRegistrationOwners].[PackageRegistrationKey] = [PackageRegistrations].[Key]

INNER JOIN	[Users] (NOLOCK)
ON			[Users].[Key] = [PackageRegistrationOwners].[UserKey]