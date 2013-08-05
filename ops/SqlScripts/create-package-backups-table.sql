CREATE TABLE PackageBackups
(
	[Key] int IDENTITY(1,1) NOT NULL,
	Id nvarchar(1024) NULL,
	Version nvarchar(1024) NULL,
	Hash nvarchar(1024) NULL,
	error nvarchar(max) NULL,
	PRIMARY KEY CLUSTERED ([Key] ASC)
)


