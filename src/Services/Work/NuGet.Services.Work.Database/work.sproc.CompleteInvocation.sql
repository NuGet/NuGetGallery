CREATE PROCEDURE [work].[CompleteInvocation]
	@Id uniqueidentifier,
	@Version int,
	@Result int,
	@ResultMessage nvarchar(MAX),
    @LogUrl nvarchar(200),
	@InstanceName nvarchar(100)
AS
	-- Add a new row for the specified Invocation indicating its new state and completion marker
	INSERT INTO [private].InvocationsStore(
            [Id],
            [Job],
            [Source],
            [Payload],
            [Status],
            [Result],
            [ResultMessage],
            [UpdatedBy],
            [LogUrl],
            [DequeueCount],
            [IsContinuation],
            [Complete],
            [LastDequeuedAt],
            [LastSuspendedAt],
            [CompletedAt],
            [QueuedAt],
            [NextVisibleAt],
            [UpdatedAt],
            [JobInstanceName])
	OUTPUT	inserted.*
	SELECT	Id,
			Job, 
			Source, 
			Payload, 
			4 AS [Status], -- Executed
			@Result AS [Result],
			@ResultMessage AS [ResultMessage],
			@InstanceName AS [UpdatedBy],
            @LogUrl AS [LogUrl],
            [DequeueCount],
			IsContinuation,
			1 AS [Complete],
            [LastDequeuedAt],
            [LastSuspendedAt],
            SYSUTCDATETIME() AS [CompletedAt],
			QueuedAt,
			[NextVisibleAt],
			SYSUTCDATETIME() AS [UpdatedAt],
            [JobInstanceName]
	FROM	[work].ActiveInvocations
	WHERE	[Id] = @Id AND [Version] = @Version
