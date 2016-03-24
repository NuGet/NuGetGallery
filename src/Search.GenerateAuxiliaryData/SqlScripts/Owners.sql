SELECT	[PackageRegistrations].[Id] 'Id',
		[Users].[UserName] 'UserName'
FROM	[PackageRegistrations]

INNER JOIN	[PackageRegistrationOwners]
ON			[PackageRegistrationOwners].[PackageRegistrationKey] = [PackageRegistrations].[Key]

INNER JOIN	[Users]
ON			[Users].[Key] = [PackageRegistrationOwners].[UserKey]