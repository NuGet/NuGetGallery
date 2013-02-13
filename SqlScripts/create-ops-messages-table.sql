CREATE TABLE OpsMessages
(
	[Key] int IDENTITY(1,1) NOT NULL,
	Task nvarchar(MAX) NULL,
	Text nvarchar(MAX) NOT NULL,
	Timestamp datetime NOT NULL DEFAULT (getutcdate()),
	PRIMARY KEY CLUSTERED ([Key] ASC)
)