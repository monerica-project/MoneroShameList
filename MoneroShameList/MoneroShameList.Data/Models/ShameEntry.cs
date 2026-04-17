using System.ComponentModel.DataAnnotations;

namespace MoneroShameList.Models;

public enum ShameCategory
{
    NeverAdded,
    DelistedXmr,
    AllowsOtherCrypto,
    Converted,
    Scammer
}

public class ShameEntry
{
    public int Id { get; set; }
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(220)] public string Slug { get; set; } = "";
    [MaxLength(500)] public string Website { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    [MaxLength(1000)] public string WhyShamed { get; set; } = "";
    [MaxLength(500)] public string? ContactUrl { get; set; }
    [MaxLength(500)] public string? MoneroAlternativeUrl { get; set; }
    public string? AdminComment { get; set; }
    public ShameCategory Category { get; set; } = ShameCategory.NeverAdded;
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}