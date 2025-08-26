# GCP-PubSubPrimer (`psub`)

Sets up Gmail watch requests for pull/push notifications.

## Purpose

Creates Gmail watches that send notifications to Pub/Sub topics. This is required before using pull/push notifications in other tools.

## Usage

### Setup Gmail Watch
```powershell
.\psub.exe watch gmail --project-id "your-project" --topic-id "gmail-notifications"
```

### Cancel Gmail Watch
```powershell
.\psub.exe cancel gmail
```

### Environment Variables
```powershell
$env:GCP_CREDENTIALS_PATH = "C:\path\to\credentials.json"
$env:GCP_PUBSUB_PROJECTID = "your-project-id"
$env:GCP_PUBSUB_TOPICID = "gmail-notifications"
```

## Options

**Watch Command:**
- `-p, --project-id`: GCP Project ID
- `-t, --topic-id`: Pub/Sub Topic ID  
- `-f, --force`: Force new watch creation
- `-v, --verbose`: Debug output (0-3)

**Cancel Command:**
- `-v, --verbose`: Debug output (0-3)

## Workflow

1. **One-time setup** with PubSubPrimer:
   ```powershell
   .\psub.exe watch gmail --project-id "my-project" --topic-id "gmail-notifications"
   ```

2. **Use in other tools**:
   ```powershell
   .\mailflow.exe subscribe --pull --topic "projects/my-project/topics/gmail-notifications"
   ```
