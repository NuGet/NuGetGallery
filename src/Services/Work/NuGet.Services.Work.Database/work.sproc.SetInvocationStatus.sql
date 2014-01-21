CREATE PROCEDURE [work].[SetInvocationStatus]
	@Id uniqueidentifier,
	@Version int,
	@Status int,
	@Result int,
	@InstanceName nvarchar(100)
AS
	-- Add a new row for the specified Invocation indicating its new status
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
			@Status AS [Status],
			@Result AS [Result],
            [ResultMessage],
			@InstanceName AS [UpdatedBy],
            [LogUrl],
			DequeueCount,
			IsContinuation,
			Complete,
            [LastDequeuedAt],
            [LastSuspendedAt],
            [CompletedAt],
			QueuedAt,
			[NextVisibleAt],
			SYSUTCDATETIME() AS [UpdatedAt],
            [JobInstanceName]
	FROM	[work].ActiveInvocations
	WHERE	[Id] = @Id AND [Version] = @Version
