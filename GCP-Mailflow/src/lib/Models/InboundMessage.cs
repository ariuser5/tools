namespace DCiuve.Gcp.Mailflow.Models;

/// <summary>
/// Represents an inbound email message.
/// It is created when a Pubsub message is received.
/// </summary>
public class InboundMessage
{
	public InboundMessage(string batchId, Task<ProcessedMessage> processingMessage)
	{
		BatchId = batchId;
		DetailsProcessing = processingMessage;
	}

	/// <summary>
	/// The unique identifier for the batch of messages this email belongs to.
	/// </summary>
	public string BatchId { get; set; }
	
	/// <summary>
	/// The task representing the processing of the email message.
	/// </summary>
	public Task<ProcessedMessage> DetailsProcessing { get; }
}
