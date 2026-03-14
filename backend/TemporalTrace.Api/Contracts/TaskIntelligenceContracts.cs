namespace TemporalTrace.Api.Contracts;

public class DecisionReplayEventResponse
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
}

public class DecisionReplayResponse
{
    public int TaskId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<DecisionReplayEventResponse> Events { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}

public class BranchScoreResultResponse
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public double LeadTimeImpact { get; set; }
    public double RiskScore { get; set; }
    public double EffortCost { get; set; }
    public double OverallScore { get; set; }
    public bool IsRecommended { get; set; }
    public List<string> Reasons { get; set; } = [];
}

public class BranchScoreResponse
{
    public int TaskId { get; set; }
    public DateTime TargetTime { get; set; }
    public List<BranchScoreResultResponse> Branches { get; set; } = [];
}

public class DailyStandupResponse
{
    public DateTime TargetDate { get; set; }
    public List<string> CompletedToday { get; set; } = [];
    public List<string> InProgressToday { get; set; } = [];
    public List<string> BlockedToday { get; set; } = [];
    public List<string> Highlights { get; set; } = [];
    public string Narrative { get; set; } = string.Empty;
}
