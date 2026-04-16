using System.ComponentModel.DataAnnotations;

namespace MoneroShameList.Models;

public class ShameEntry
{
    public int Id { get; set; }
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(500)] public string Website { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    [MaxLength(1000)] public string WhyShamed { get; set; } = "";
    [MaxLength(500)] public string? ContactUrl { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
