namespace PortWhisperer.Core.Models;

public enum WatchEventType { New, Removed }

public record WatchEvent(WatchEventType Type, PortInfo? Info, int? Port);
