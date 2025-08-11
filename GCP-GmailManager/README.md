# Gmail CLI (`gmailcli`)

A command-line tool for fetching emails and subscribing to email notifications from Gmail using the Google Gmail API.

## Features

- **Fetch Command**: Retrieve emails from Gmail with powerful filtering options
- **Subscribe Command**: Monitor Gmail for new emails with polling or push notifications
- **Multiple Output Formats**: Console, JSON, and CSV output support
- **Advanced Filtering**: Filter by sender, subject, date range, labels, and more
- **Authentication**: Secure OAuth2 authentication using Google Cloud credentials

## Prerequisites

1. **Google Cloud Credentials**: Set up a Google Cloud project and download the service account credentials
2. **Environment Variable**: Set `GCP_CREDENTIALS_PATH` to point to your credentials JSON file
3. **For Push Notifications**: If using `--push` option, you must first prime the Pub/Sub topic using the GCP-PubSubPrimer tool

```powershell
$env:GCP_CREDENTIALS_PATH = "C:\path\to\your\credentials.json"
```

### Setting Up Push Notifications

There are two ways to set up push notifications:

#### Option 1: Automatic Setup (Recommended for Simple Use Cases)

Use the `--setup-watch` option to let Gmail Manager handle the Gmail watch request automatically:

```powershell
cd "GCP-GmailManager\src\cli"
.\gmailcli.exe subscribe --name "auto-monitor" --push --setup-watch --topic "projects/your-project-id/topics/gmail-notifications"
```

#### Option 2: Manual Setup with PubSubPrimer (Recommended for Production)

⚠️ **One-time Setup Required**: Use the GCP-PubSubPrimer tool to set up the Gmail watch first, then use Gmail Manager:

```powershell
# First, use the GCP-PubSubPrimer to set up Gmail watch
.\pub-sub-prime.exe --service gmail --project-id "your-project-id" --topic-id "gmail-notifications"

# Then you can use push notifications in Gmail Manager
cd "..\..\..\GCP-GmailManager\src\cli"
.\gmailcli.exe subscribe --name "push-monitor" --push --topic "projects/your-project-id/topics/gmail-notifications"
```

## Building the Project

```powershell
cd "GCP-GmailManager\src"
dotnet build
```

## Running the Application

There are several ways to run the Gmail Manager:

### Option 1: Using the built executable (Recommended)
After building, you can run the executable directly:
```powershell
# Navigate to the CLI project directory and run
cd "GCP-GmailManager\src\cli"
.\gmailcli.exe fetch --help
.\gmailcli.exe subscribe --help
```

### Option 2: Using `dotnet run`
Navigate to the CLI project directory and use `dotnet run`:
```powershell
cd "GCP-GmailManager\src\cli"
dotnet run -- fetch --help
dotnet run -- subscribe --help
```

**Note**: All examples in this README use the executable directly for simplicity.

## Usage

### Fetch Command

Retrieve emails from Gmail based on specified criteria.

#### Basic Examples

```powershell
# Fetch the latest 10 emails
.\gmailcli.exe fetch

# Fetch only unread emails
.\gmailcli.exe fetch --unread

# Fetch emails from a specific sender
.\gmailcli.exe fetch --from "sender@example.com"

# Fetch emails with specific subject
.\gmailcli.exe fetch --subject "Important"

# Fetch emails from the last week
.\gmailcli.exe fetch --after "2025-08-04"

# Get detailed email content
.\gmailcli.exe fetch --verbose

# Export to JSON
.\gmailcli.exe fetch --output json > emails.json

# Export to CSV
.\gmailcli.exe fetch --output csv > emails.csv
```

#### Advanced Examples

```powershell
# Complex Gmail query
.\gmailcli.exe fetch --query "is:unread from:notifications@github.com"

# Fetch emails with multiple filters
.\gmailcli.exe fetch --from "team@company.com" --subject "Weekly Report" --after "2025-08-01" --before "2025-08-07"

# Fetch more emails
.\gmailcli.exe fetch --max 50

# Include spam and trash
.\gmailcli.exe fetch --include-spam
```

### Subscribe Command

Monitor Gmail for new emails and get notified when they arrive.

#### Basic Examples

```powershell
# Monitor all new emails (polling every 30 seconds)
.\gmailcli.exe subscribe --name "all-emails"

# Monitor unread emails only
.\gmailcli.exe subscribe --name "unread-monitor" --unread

# Monitor emails from specific sender
.\gmailcli.exe subscribe --name "boss-emails" --from "boss@company.com"

# Run for a specific duration
.\gmailcli.exe subscribe --name "temp-monitor" --duration "1h"

# Custom polling interval (every 2 minutes)
.\gmailcli.exe subscribe --name "slow-monitor" --interval 120
```

#### Advanced Examples

```powershell
# Monitor with webhook notifications
.\gmailcli.exe subscribe --name "webhook-monitor" --webhook "https://your-webhook-url.com/notify"

# Monitor with push notifications (automatic Gmail watch setup)
.\gmailcli.exe subscribe --name "auto-push-monitor" --push --setup-watch --topic "projects/your-project/topics/gmail-notifications"

# Monitor with push notifications (using pre-primed topic)
# IMPORTANT: First prime the topic using GCP-PubSubPrimer tool!
.\gmailcli.exe subscribe --name "push-monitor" --push --topic "projects/your-project/topics/gmail-notifications"

# Complex monitoring with multiple filters and automatic setup
.\gmailcli.exe subscribe --name "complex-monitor" --from "alerts@system.com" --subject "ERROR" --unread --verbose --push --setup-watch --topic "projects/your-project/topics/gmail-notifications"

# Monitor for a specific time period
.\gmailcli.exe subscribe --name "daily-monitor" --duration "24h" --interval 60
```

## Command Options

### Fetch Command Options

| Option | Description | Example |
|--------|-------------|---------|
| `-q, --query` | Gmail query string | `--query "is:unread"` |
| `-m, --max` | Maximum emails to fetch (default: 10) | `--max 50` |
| `-u, --unread` | Fetch only unread emails | `--unread` |
| `-f, --from` | Filter by sender | `--from "user@example.com"` |
| `-s, --subject` | Filter by subject | `--subject "Meeting"` |
| `--after` | Filter emails after date | `--after "2025-08-01"` |
| `--before` | Filter emails before date | `--before "2025-08-31"` |
| `-l, --labels` | Filter by label IDs | `--labels "INBOX,IMPORTANT"` |
| `--include-spam` | Include spam/trash | `--include-spam` |
| `-o, --output` | Output format (console/json/csv) | `--output json` |
| `--page-token` | Pagination token | `--page-token "token123"` |
| `-v, --verbose` | Show detailed content | `--verbose` |

### Subscribe Command Options

| Option | Description | Example |
|--------|-------------|---------|
| `-n, --name` | **Required** Subscription name | `--name "my-monitor"` |
| `-t, --topic` | Pub/Sub topic for push notifications | `--topic "projects/my-project/topics/gmail"` |
| `-i, --interval` | Polling interval in seconds (default: 30) | `--interval 60` |
| `-q, --query` | Gmail query string | `--query "is:unread"` |
| `-u, --unread` | Monitor only unread emails | `--unread` |
| `-f, --from` | Monitor emails from sender | `--from "alerts@company.com"` |
| `-s, --subject` | Monitor emails with subject | `--subject "Alert"` |
| `--after` | Monitor emails after date | `--after "2025-08-01"` |
| `--before` | Monitor emails before date | `--before "2025-08-31"` |
| `-l, --labels` | Monitor emails with labels | `--labels "INBOX,IMPORTANT"` |
| `-m, --max` | Max emails per check (default: 50) | `--max 100` |
| `--webhook` | Webhook URL for notifications | `--webhook "https://api.company.com/webhook"` |
| `--duration` | Run duration (e.g., '1h', '30m', '2d') | `--duration "2h"` |
| `-v, --verbose` | Show detailed output | `--verbose` |
| `--push` | Use push notifications (requires topic priming) | `--push` |
| `--setup-watch` | Setup Gmail watch automatically (alternative to PubSubPrimer) | `--setup-watch` |

## Important Notes

### Push Notifications Setup

📌 **Two Options Available**: You can set up push notifications in two ways:

**Option 1: Automatic Setup (`--setup-watch`)**
- Use `--setup-watch` flag to let Gmail Manager handle everything
- Simpler for testing and development
- Gmail Manager creates and cleans up the watch request automatically

**Option 2: Manual Setup with PubSubPrimer**
- Use GCP-PubSubPrimer to set up the Gmail watch first
- Recommended for production environments
- Gives you more control over the watch configuration
- Allows multiple applications to share the same watch

1. **For automatic setup**:
   ```powershell
   # Gmail Manager handles the watch setup for you
   cd "GCP-GmailManager\src\cli"
   .\gmailcli.exe subscribe --name "my-monitor" --push --setup-watch --topic "projects/your-project-id/topics/gmail-notifications"
   ```

2. **For manual setup (production recommended)**:
   ```powershell
   # Step 1: Prime the topic first
   cd "..\GCP-PubSubPrimer\src\cli"
   .\pub-sub-prime.exe --service gmail --project-id "your-project-id" --topic-id "gmail-notifications"
   
   # Step 2: Use Gmail Manager
   cd "..\..\..\GCP-GmailManager\src\cli"
   .\gmailcli.exe subscribe --name "my-monitor" --push --topic "projects/your-project-id/topics/gmail-notifications"
   ```

**Why two options?** The automatic setup (`--setup-watch`) is convenient for testing, but the manual approach with PubSubPrimer gives you better control and is recommended for production environments where you might want to share the same Gmail watch across multiple applications.

## Duration Format

For the `--duration` option, use these formats:
- `30s` - 30 seconds
- `5m` - 5 minutes  
- `2h` - 2 hours
- `1d` - 1 day

## Gmail Query Syntax

The `--query` option supports Gmail's powerful search syntax:

```
# Common examples
is:unread                    # Unread emails
from:user@example.com        # From specific sender
subject:"Meeting invite"     # Subject contains text
has:attachment              # Has attachments
after:2025/08/01            # After specific date
before:2025/08/31           # Before specific date
label:important             # Has specific label
is:starred                  # Starred emails
```

## Project Structure

```
GCP-GmailManager/
├── src/
│   ├── GCP-Gmail.sln
│   ├── lib/                    # Gmail library
│   │   ├── Gcp.Gmail.csproj
│   │   ├── Models/
│   │   │   ├── EmailMessage.cs
│   │   │   ├── EmailFilter.cs
│   │   │   └── EmailSubscription.cs
│   │   └── Services/
│   │       ├── EmailFetcher.cs
│   │       └── EmailSubscriber.cs
│   └── cli/                    # Command-line interface
│       ├── gmail-manager.csproj
│       ├── Program.cs
│       └── Commands/
│           ├── FetchCommand.cs
│           ├── FetchOptions.cs
│           ├── SubscribeCommand.cs
│           └── SubscribeOptions.cs
```

## Dependencies

- **Google.Apis.Gmail.v1**: Gmail API client
- **CommandLineParser**: Command-line argument parsing
- **DCiuve.Tools.Gcp.Auth**: Google Cloud authentication
- **DCiuve.Tools.Logging**: Logging infrastructure

## Error Handling

The tool provides detailed error messages and logging:

- **Authentication errors**: Check your `GCP_CREDENTIALS_PATH` and credentials file
- **API errors**: Verify your Google Cloud project has Gmail API enabled
- **Permission errors**: Ensure your credentials have the required Gmail scopes
- **Push notification errors**: Make sure you've primed the Pub/Sub topic using GCP-PubSubPrimer first
- **Topic not found errors**: Verify the topic exists and the topic name format is correct (`projects/PROJECT_ID/topics/TOPIC_NAME`)

## Examples in Practice

### Daily Email Reports
```powershell
# Get daily summary of unread emails
.\gmailcli.exe fetch --unread --output json | ConvertFrom-Json | Measure-Object | Select-Object Count

# Monitor for urgent emails during work hours
.\gmailcli.exe subscribe --name "urgent-monitor" --subject "URGENT" --duration "8h"
```

### Email Backup
```powershell
# Export all emails from last month to CSV
.\gmailcli.exe fetch --after "2025-07-01" --before "2025-07-31" --max 1000 --output csv > backup.csv
```

### Integration with Scripts
```powershell
# Get new emails as JSON for processing
$emails = .\gmailcli.exe fetch --unread --output json | ConvertFrom-Json
foreach ($email in $emails) {
    Write-Host "New email from: $($email.from) - Subject: $($email.subject)"
}
```
