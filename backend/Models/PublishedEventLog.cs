namespace N2.Circulation.Api.Models;

public sealed class PublishedEventLog
{
    public Guid Id { get; set; }

    public string SourceService { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; }
}