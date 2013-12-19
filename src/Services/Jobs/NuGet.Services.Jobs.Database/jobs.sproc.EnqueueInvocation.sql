CREATE PROCEDURE [jobs].[EnqueueInvocation]
	@Job nvarchar(50),
	@Source nvarchar(50),
	@Payload nvarchar(MAX) = NULL,
	@InstanceName nvarchar(100),
	@NextVisibleAt datetimeoffset
AS
	-- Insert a row with a completely new Id
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
	OUTPUT inserted.*
	VALUES(
		NEWID(),				-- Id
		@Job,					-- Job
		@Source,				-- Source
		@Payload,				-- Payload
		'Queued',				-- Status
		'Incomplete',			-- Result
		@InstanceName,			-- UpdatedBy
		0,						-- IsContinuation
		0,						-- DequeueCount
		0,						-- Complete
		0,						-- Dequeued
		SYSDATETIMEOFFSET(),	-- QueuedAt
		@NextVisibleAt,			-- NextVisibleAt
		SYSDATETIMEOFFSET())	-- UpdatedAt