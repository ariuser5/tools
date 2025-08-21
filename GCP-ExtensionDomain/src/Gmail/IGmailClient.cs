using Google.Apis.Gmail.v1;
using Google.Apis.Http;

namespace DCiuve.Gcp.ExtensionDomain.Gmail;

public interface IGmailClient : IGcpExtensionService
{
	/// <summary>
	/// Gets the HTTP client initializer for the Gmail API.
	/// </summary>
	public IConfigurableHttpClientInitializer HttpClientInitializer { get; }
	
	/// <summary>
	/// Creates a request to list the messages for a specific user.
	/// </summary>
	/// <param name="userId">The ID of the user.</param>
	/// <returns></returns>
	public Task<UsersResource.MessagesResource.ListRequest> CreateMessageListRequest(string userId);
	
	/// <summary>
	/// Creates a request to list the history of changes for a specific user.
	/// </summary>
	/// <param name="userId">The ID of the user.</param>
	/// <returns></returns>
	public Task<UsersResource.HistoryResource.ListRequest> CreateHistoryListRequest(string userId);

	/// <summary>
	/// Creates a request to retrieve a specific email message.
	/// </summary>
	/// <param name="userId">The ID of the user.</param>
	/// <param name="messageId">The ID of the message.</param>
	/// <returns></returns>
	public Task<UsersResource.MessagesResource.GetRequest> CreateMessageGetRequest(string userId, string messageId);

	/// <summary>
	/// Creates a request to retrieve the profile information for a specific user.
	/// </summary>
	/// <param name="userId">The ID of the user.</param>
	/// <returns></returns>
	public Task<UsersResource.GetProfileRequest> CreateGetProfileRequest(string userId);
}
