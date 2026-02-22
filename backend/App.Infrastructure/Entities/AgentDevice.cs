namespace App.Infrastructure.Entities;

public class AgentDevice
{
    public Guid Id { get; set; }

    public string Hostname { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public DateTime LastSeenAt { get; set; }

    public bool IsActive { get; set; }

    public int CellId { get; set; }

    //   public Cell Cell { get; set; }

}
