# Mailflow (`mailflow`)

Command-line tool for Gmail management with email fetching and real-time monitoring.

## Prerequisites

1. Set up Google Cloud credentials:
   ```powershell
   $env:GCP_CREDENTIALS_PATH = "C:\path\to\your\credentials.json"
   ```

2. Build the project:
   ```powershell
   cd GCP-Mailflow\src
   dotnet build
   ```

## Usage

### Fetch Emails

```powershell
# Basic usage
.\mailflow.exe fetch

# With filters
.\mailflow.exe fetch --unread --from "boss@company.com" --max 5

# Output formats
.\mailflow.exe fetch --output json
.\mailflow.exe fetch --output csv
```

### Monitor Emails

**Pull Subscription Mode (Recommended):**
```powershell
# First, setup Gmail watch (one-time)
.\psub.exe watch gmail --project-id "your-project" --topic-id "gmail-notifications"

# Then monitor (pulls messages from Pub/Sub)
.\mailflow.exe subscribe --name "monitor" --pull --topic "projects/your-project/topics/gmail-notifications"
```

**Polling Mode:**
```powershell
.\mailflow.exe subscribe --name "monitor" --interval 30
```

## Common Options

### Fetch Options
- `--unread` - Only unread emails
- `--from "email"` - Filter by sender
- `--subject "text"` - Filter by subject
- `--max N` - Maximum emails (default: 10)
- `--output json|csv` - Output format

### Subscribe Options
- `--name "id"` - Subscription name (required)
- `--pull` - Use pull subscription mode (Pub/Sub)
- `--topic "projects/..."` - Pub/Sub topic for pull
- `--interval N` - Polling seconds (default: 30)
- `--duration "1h"` - Run time limit

## Examples

```powershell
# Monitor urgent emails for 2 hours
.\mailflow.exe subscribe --name "urgent" --subject "URGENT" --duration "2h"

# Export unread emails to JSON
.\mailflow.exe fetch --unread --output json > emails.json

# Monitor specific sender with pull subscription
.\mailflow.exe subscribe --name "alerts" --from "alerts@company.com" --pull
```
