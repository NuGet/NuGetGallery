CREATE PROCEDURE [work].[SuspendInvocation]
	@Id uniqueidentifier,
	@Version int,
	@Payload nvarchar(MAX),
	@SuspendUntil datetime2,
    @LogUrl nvarchar(200),
	@InstanceName nvarchar(100)
AS
	-- Add a new row for the specified Invocation indicating it has been suspended
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
			@Payload AS Payload, 
			6 AS [Status], -- Suspended
			[Result],
            [ResultMessage],
			@InstanceName AS [UpdatedBy],
            @LogUrl AS [LogUrl],
			DequeueCount,
			1 AS IsContinuation,
			[Complete],
            [LastDequeuedAt],
            SYSUTCDATETIME() AS [LastSuspendedAt],
            [CompletedAt],
			QueuedAt,
			@SuspendUntil AS [NextVisibleAt],
			SYSUTCDATETIME() AS [UpdatedAt],
            [JobInstanceName]
	FROM	[work].ActiveInvocations
	WHERE	[Id] = @Id AND [Version] = @Version