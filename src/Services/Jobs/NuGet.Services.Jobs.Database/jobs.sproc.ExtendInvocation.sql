CREATE PROCEDURE [jobs].[ExtendInvocation]
	@Id uniqueidentifier,
	@Version int,
	@InstanceName nvarchar(100),
	@ExtendTo datetimeoffset
AS
	-- Add a new row for the specified Invocation indicating its new visibility time
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
	WHERE	[Id] = @Id AND [Version] = @Version
