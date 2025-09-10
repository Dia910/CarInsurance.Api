using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Models;

public class PolicyExpirationLog
{
    public long Id { get; set; }

    public long PolicyId { get; set; }
    public InsurancePolicy Policy { get; set; } = default!;

    [Required]
    public DateOnly ExpiredDate { get; set; }

    [Required]
    public DateTime LoggedAt { get; set; }

    public string? Message { get; set; }
}