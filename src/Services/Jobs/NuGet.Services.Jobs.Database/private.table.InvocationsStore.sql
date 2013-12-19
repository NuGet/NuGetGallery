-- Master table that stores all invocations
-- APPEND ONLY! Rows in this table should never be updated. Only INSERTED (and for clean-up purposes, DELETED)
-- Use SPROCS!
CREATE TABLE [private].[InvocationsStore]
(
    [Version] int NOT NULL PRIMARY KEY IDENTITY,
    [InvocationId] uniqueidentifier NOT NULL,
    [Job] nvarchar(50) NOT NULL,
    [Source] nvarchar(50) NOT NULL,
    [Payload] nvarchar(max) NULL,
    [Status] nvarchar(50) NOT NULL,
    [Result] nvarchar(50) NOT NULL,
    [DequeueCount] int NOT NULL,
    [UpdatedBy] nvarchar(100) NOT NULL,
    [ResultMessage] nvarchar(100) NULL,
    [LogUrl] nvarchar(100) NULL,
    [IsContinuation] bit NOT NULL,

    [QueuedAt] datetimeoffset NOT NULL,
    [LastDequeuedAt] datetimeoffset NULL,
    [LastSuspendedAt] datetimeoffset NULL,
    [CompletedAt] datetimeoffset NULL,
    [NextVisibleAt] datetimeoffset NOT NULL, 
    [Complete] CHAR(10) NOT NULL, 
    [UpdatedAt] DATETIMEOFFSET NOT NULL, 
    [Dequeued] BIT NOT NULL, 
    [RowVersion] ROWVERSION NOT NULL 
)
