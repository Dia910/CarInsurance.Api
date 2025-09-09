using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Models;

public class InsurancePolicy : IValidatableObject
{
    public long Id { get; set; }

    public long CarId { get; set; }
    public Car Car { get; set; } = default!;

    public string? Provider { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [BindRequired]
    [Required(ErrorMessage = "End date is required.")]
    public DateOnly EndDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate <= StartDate)
        {
            yield return new ValidationResult(
                "End date must be greater than start date.",
                new[] { nameof(EndDate) });
        }
    }
}
