# Auto Update App

## Overview

Monitors and manages system updates for Home Assistant addons and the NetDaemon application itself.

## Purpose

The Auto Update App provides automated monitoring of system updates, intelligent notification handling, and coordinated update management to keep the home automation system current while minimizing disruption.

## Key Features

- **Update Detection**: Monitors for available updates across multiple system components
- **Notification System**: Discord alerts for pending updates with detailed information
- **Restart Management**: Handles restart notifications and confirmations
- **Update Scheduling**: Smart timing for update installations based on system usage

## Technical Implementation

### Update Monitoring
```csharp
private async Task AutoUpdate()
{
    var updates = await GetAvailableUpdates();
    if (updates.Any())
    {
        await NotifyUpdatesAvailable(updates);
        await ScheduleUpdateInstallation();
    }
}
```

### Update Detection Sources
- **Home Assistant Core**: Monitors for HA core updates
- **Add-ons**: Tracks individual addon updates
- **NetDaemon**: Monitors NetDaemon framework updates
- **Custom Components**: Tracks HACS and other custom component updates

## Configuration

### Update Scheduling
- **Maintenance Windows**: Preferred times for update installations
- **Critical Updates**: Immediate notification and scheduling for security updates
- **Non-Critical Updates**: Batched notifications and scheduled installations
- **User Confirmation**: Optional manual approval for major updates

### Notification Settings
- **Discord Integration**: Rich notifications with update details
- **Phone Notifications**: High-priority alerts for critical updates
- **Update Summaries**: Daily/weekly update status reports

## Smart Features

### Intelligent Scheduling
- **Usage Pattern Analysis**: Schedules updates during low-usage periods
- **Holiday Awareness**: Adjusts update timing during vacation periods
- **Sleep Integration**: Coordinates with sleep manager for optimal timing
- **Dependency Management**: Handles update dependencies and ordering

### Update Categorization
- **Security Updates**: Immediate priority with fast-track scheduling
- **Feature Updates**: Normal priority with user notification
- **Beta Updates**: Optional updates with explicit confirmation required
- **Maintenance Updates**: Low priority with automatic scheduling

## Restart Management

### Pre-Restart Actions
1. **State Preservation**: Saves current system state
2. **Notification**: Alerts users of impending restart
3. **Grace Period**: Provides time for manual intervention
4. **Dependency Check**: Ensures all dependent services are ready

### Post-Restart Actions
1. **Health Check**: Verifies system functionality after restart
2. **State Restoration**: Restores preserved system state
3. **Update Confirmation**: Confirms successful update installation
4. **Error Reporting**: Reports any issues encountered during update

## Notification Examples

### Update Available
- **Title**: "System Updates Available"
- **Content**: Detailed list of available updates with descriptions
- **Actions**: Install now, schedule later, view details

### Restart Required
- **Title**: "System Restart Required"
- **Content**: Updates requiring restart with estimated downtime
- **Actions**: Restart now, schedule restart, cancel updates

## Error Handling

- **Failed Updates**: Automatic rollback and error reporting
- **Network Issues**: Retry logic with exponential backoff
- **Dependency Conflicts**: Conflict resolution and user notification
- **Disk Space**: Storage space validation before update downloads

## Integration Points

- **Sleep Manager**: Coordinates update timing with sleep schedules
- **Holiday Manager**: Adjusts behavior during vacation periods
- **Notification System**: Provides rich update notifications
- **System Monitoring**: Tracks update success and system health

## Safety Features

- **Rollback Capability**: Automatic rollback on update failures
- **Backup Creation**: Automated backups before major updates
- **Health Monitoring**: Continuous health checks during updates
- **Emergency Override**: Manual update abort capability

## Usage Patterns

### Automatic Mode
- Detects updates automatically
- Schedules installations during optimal windows
- Provides notifications for user awareness
- Handles restarts with minimal user intervention

### Manual Mode
- Notifies of available updates
- Waits for explicit user approval
- Provides detailed update information
- Allows custom scheduling of installations