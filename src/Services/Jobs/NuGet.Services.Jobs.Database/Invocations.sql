CREATE TABLE [dbo].[Invocations]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY,
	[Job] nvarchar(50) NOT NULL,
	[Source] nvarchar(50) NOT NULL,
	[Payload] nvarchar(max) NULL,
	[Status] nvarchar(50) NOT NULL,
	[Result] nvarchar(50) NOT NULL,
	[DequeueCount] int NOT NULL,
	[LastInstanceName] nvarchar(100) NOT NULL,
	[ResultMessage] nvarchar(100) NULL,
	[LogUrl] nvarchar(100) NULL,
	[IsContinuation] bit NOT NULL,

	[QueuedAt] datetimeoffset NOT NULL,
	[LastDequeuedAt] datetimeoffset NULL,
	[LastSuspendedAt] datetimeoffset NULL,
	[CompletedAt] datetimeoffset NULL,
	[NextVisibleAt] datetimeoffset NOT NULL, 
    [Complete] BIT NOT NULL, 
    [LastUpdatedAt] DATETIMEOFFSET NOT NULL, 
    [Dequeued] BIT NOT NULL, 
    [RowVersion] ROWVERSION NOT NULL 
)
