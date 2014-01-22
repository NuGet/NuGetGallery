-- Master table that stores all invocations
-- APPEND ONLY! Rows in this table should never be updated. Only INSERTED (and for clean-up purposes, DELETED)
-- Use SPROCS!
CREATE TABLE [private].[InvocationsStore]
(
    [Version] BIGINT NOT NULL PRIMARY KEY IDENTITY,
    [Id] uniqueidentifier NOT NULL,
    [Job] nvarchar(50) NOT NULL,
    [Source] nvarchar(50) NOT NULL,
    [Payload] nvarchar(max) NULL,
    [Status] INT NOT NULL,
    [Result] INT NOT NULL,
    [ResultMessage] nvarchar(MAX) NULL,
    [UpdatedBy] nvarchar(100) NOT NULL,
    [LogUrl] nvarchar(200) NULL,

    [DequeueCount] int NOT NULL,

    [IsContinuation] bit NOT NULL,
    [Complete] bit NOT NULL, 

    [LastDequeuedAt] DATETIME2 NULL,
    [LastSuspendedAt] DATETIME2 NULL,
    [CompletedAt] DATETIME2 NULL,
    [QueuedAt] DATETIME2 NOT NULL,
    [NextVisibleAt] DATETIME2 NOT NULL, 
    [UpdatedAt] DATETIME2 NOT NULL, 
    [JobInstanceName] NVARCHAR(50) NULL
)
