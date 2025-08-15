# Development Tools Collection

A collection of command-line tools for Google Cloud Platform integration and other utilities.

## Projects

### GCP Tools
- **[GCP-Auth](./GCP-Auth/)** - Google Cloud authentication library
- **[GCP-ExtensionDomain](./GCP-ExtensionDomain/)** - Shared interfaces for GCP services
- **[GCP-Mailflow](./GCP-Mailflow/)** - Gmail CLI for email management and monitoring
- **[GCP-PubSubPrimer](./GCP-PubSubPrimer/)** - Gmail watch setup for push notifications

### Other Tools
- **[AskLlama](./AskLlama/)** - AI question answering CLI
- **[GithubApiClient](./GithubApiClient/)** - GitHub API client

### Shared
- **[DCiuve-Shared](./DCiuve-Shared/)** - Console logger with colors

## Quick Start

1. Set up Google Cloud credentials:
   ```powershell
   $env:GCP_CREDENTIALS_PATH = "C:\path\to\your\credentials.json"
   ```

2. Build projects:
   ```powershell
   dotnet build
   ```

3. Example: Monitor Gmail
   ```powershell
   # Setup (one-time)
   .\pub-sub-prime.exe watch gmail --project-id "your-project" --topic-id "gmail-notifications"
   
   # Monitor emails
   .\mailflow.exe subscribe --name "monitor" --push --topic "projects/your-project/topics/gmail-notifications"
   ```

---

TODO: 
1. Rename gmailcli to mailflow. ✅ DONE
2. See if you can split the two subscribe methods: polling and push;
