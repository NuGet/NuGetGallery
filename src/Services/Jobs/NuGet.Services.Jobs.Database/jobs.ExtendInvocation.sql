CREATE PROCEDURE [jobs].[ExtendInvocation]
	@InvocationId uniqueidentifier,
	@Version int,
	@InstanceName nvarchar(100),
	@ExtendTo datetimeoffset
AS
	-- Add a new row for the specified Invocation indicating its new visibility time
	INSERT INTO [private].InvocationsStore(
			[InvocationId],
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
	SELECT	InvocationId,
			Job, 
			Source, 
			Payload, 
			[Status],
			[Result],
			@InstanceName AS [UpdatedBy],
			IsContinuation,
			DequeueCount,
			Complete,
			Dequeued,
			QueuedAt,
			@ExtendTo AS [NextVisibleAt],
			SYSDATETIMEOFFSET() AS [UpdatedAt]
	FROM	[jobs].ActiveInvocations
	WHERE	[InvocationId] = @InvocationId AND [Version] = @Version
