namespace TemporalTrace.Api.Contracts;

public class TimelineWindowResponse
{
    public DateTime MinTime { get; set; }
    public DateTime MaxTime { get; set; }
    public DateTime YesterdayStartUtc { get; set; }
    public DateTime YesterdayEndUtc { get; set; }
    public bool UsedFallbackWindow { get; set; }
}