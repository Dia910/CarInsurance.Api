using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Services;

public class CarService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<List<CarDto>> ListCarsAsync()
    {
        return await _db.Cars.Include(c => c.Owner)
            .Select(c => new CarDto(c.Id, c.Vin, c.Make, c.Model, c.YearOfManufacture,
                                    c.OwnerId, c.Owner.Name, c.Owner.Email))
            .ToListAsync();
    }

    public async Task<bool> IsInsuranceValidAsync(long carId, DateOnly date)
    {
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car {carId} not found");

        return await _db.Policies.AnyAsync(p =>
            p.CarId == carId &&
            p.StartDate <= date &&
            p.EndDate >= date
        );
    }

    public async Task<ClaimDto> CreateClaimAsync(long carId, CreateClaimRequest request)
    {
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car {carId} not found");

        if (!DateOnly.TryParse(request.ClaimDate, out var claimDate))
            throw new ArgumentException("Invalid date format");

        var claim = new Claim
        {
            CarId = carId,
            ClaimDate = claimDate,
            Description = request.Description,
            Amount = request.Amount
        };

        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();

        return new ClaimDto(claim.Id, claim.ClaimDate.ToString("yyyy-MM-dd"),
                           claim.Description, claim.Amount);
    }

    public async Task<List<HistoryItemDto>> GetCarHistoryAsync(long carId)
    {
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car {carId} not found");

        var history = new List<HistoryItemDto>();

        // Adaug? policy-urile
        var policies = await _db.Policies
            .Where(p => p.CarId == carId)
            .ToListAsync();

        foreach (var policy in policies)
        {
            history.Add(new HistoryItemDto("Policy", policy.StartDate.ToString("yyyy-MM-dd"),
                                         $"Insurance policy with {policy.Provider}", null, policy.Provider));
            history.Add(new HistoryItemDto("Policy", policy.EndDate.ToString("yyyy-MM-dd"),
                                         $"Insurance policy with {policy.Provider} expired", null, policy.Provider));
        }

        // Adaug? claim-urile
        var claims = await _db.Claims
            .Where(c => c.CarId == carId)
            .ToListAsync();

        foreach (var claim in claims)
        {
            history.Add(new HistoryItemDto("Claim", claim.ClaimDate.ToString("yyyy-MM-dd"),
                                         claim.Description, claim.Amount, null));
        }

        return history.OrderBy(h => h.Date).ToList();
    }
}
