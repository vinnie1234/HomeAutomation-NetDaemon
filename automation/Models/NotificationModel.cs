namespace Automation.Models;

public class NotificationModel
{
    public NotificationModel(string name, string value, DateTimeOffset lastSendNotification)
    {
        Name = name;
        Value = value;
        LastSendNotification = lastSendNotification;
    }

    public string Name { get; }
    
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string Value { get; set; }
    public DateTimeOffset LastSendNotification { get; }
}