CREATE PROCEDURE [jobs].[SetInvocationStatus]
	@Id uniqueidentifier,
	@Version int,
	@Status nvarchar(50),
	@Result nvarchar(50),
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
			[UpdatedBy],
			[IsContinuation],
			[DequeueCount],
			[Complete],
			[Dequeued],
			[QueuedAt], 
			[NextVisibleAt],
			[UpdatedAt])
	OUTPUT	inserted.*
	SELECT	Id,
			Job, 
			Source, 
			Payload, 
			@Status AS [Status],
			@Result AS [Result],
			@InstanceName AS [UpdatedBy],
			IsContinuation,
			DequeueCount,
			Complete,
			Dequeued,
			QueuedAt,
			[NextVisibleAt],
			SYSDATETIMEOFFSET() AS [UpdatedAt]
	FROM	[jobs].ActiveInvocations
	WHERE	[Id] = @Id AND [Version] = @Version
