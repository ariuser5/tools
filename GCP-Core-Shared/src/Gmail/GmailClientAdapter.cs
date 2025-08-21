using Google.Apis.Gmail.v1;
using DCiuve.Gcp.ExtensionDomain.Gmail;
using Google.Apis.Http;

namespace DCiuve.Gcp.Shared.Gmail;

public class GmailClientAdapter : IGmailClient
{
    private readonly GmailService _gmailService;
	private bool _disposeInner;

	/// <summary>
	/// Initializes a new instance of the <see cref="GmailClientAdapter"/> class.
	/// </summary>
	/// <param name="gmailService">The Gmail service.</param>
	/// <param name="disposeInner">
	/// if set to <c>true</c> it disposes the inner Gmail service when this adapter is disposed.
	/// </param>
	public GmailClientAdapter(GmailService gmailService, bool disposeInner = false)
	{
		_gmailService = gmailService ?? throw new ArgumentNullException(nameof(gmailService));
		_disposeInner = disposeInner;
	}

	public string ApplicationName => _gmailService.ApplicationName;

	public IConfigurableHttpClientInitializer HttpClientInitializer => _gmailService.HttpClientInitializer;

	public Task<UsersResource.GetProfileRequest> CreateGetProfileRequest(string userId)
	{
		return Task.FromResult(_gmailService.Users.GetProfile(userId));
	}

	public Task<UsersResource.HistoryResource.ListRequest> CreateHistoryListRequest(string userId)
    {
        return Task.FromResult(_gmailService.Users.History.List(userId));
    }

    public Task<UsersResource.MessagesResource.GetRequest> CreateMessageGetRequest(string userId, string messageId)
    {
        return Task.FromResult(_gmailService.Users.Messages.Get(userId, messageId));
    }

	public Task<UsersResource.MessagesResource.ListRequest> CreateMessageListRequest(string userId)
	{
		return Task.FromResult(_gmailService.Users.Messages.List(userId));
	}

	public void Dispose()
	{
		if (_disposeInner)
		{
			_gmailService.Dispose();
		}
	}
}
