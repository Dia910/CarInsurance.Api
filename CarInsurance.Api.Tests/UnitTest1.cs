using CarInsurance.Api.Controllers;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Models;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Xunit;

namespace CarInsurance.Api.Tests;

public class UnitTest1 : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CarService _service;
    private readonly CarsController _controller;

    public UnitTest1()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _service = new CarService(_context);
        _controller = new CarsController(_service);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var owner = new Owner { Name = "Test Owner", Email = "test@example.com" };
        _context.Owners.Add(owner);
        _context.SaveChanges();

        var car = new Car
        {
            Vin = "TEST123",
            Make = "Test",
            Model = "Car",
            YearOfManufacture = 2020,
            OwnerId = owner.Id
        };
        _context.Cars.Add(car);
        _context.SaveChanges();


        var policy = new InsurancePolicy
        {
            CarId = car.Id,
            Provider = "TestInsurance",
            StartDate = new DateOnly(2024, 1, 1),
            EndDate = new DateOnly(2024, 12, 31)
        };
        _context.Policies.Add(policy);
        _context.SaveChanges();
    }

    [Fact]
    public async Task IsInsuranceValid_ValidCarAndDate_ReturnsOk()
    {
        var carId = _context.Cars.First().Id;
        var date = "2024-06-01";

        var result = await _controller.IsInsuranceValid(carId, date);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<InsuranceValidityResponse>(okResult.Value);
        Assert.True(response.Valid);
        Assert.Equal(carId, response.CarId);
        Assert.Equal(date, response.Date);
    }

    [Fact]
    public async Task IsInsuranceValid_NonExistentCar_ReturnsNotFound()
    {
        var nonExistentCarId = 99999L;
        var date = "2024-06-01";

        var result = await _controller.IsInsuranceValid(nonExistentCarId, date);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Contains("not found", notFoundResult.Value?.ToString()?.ToLower());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task IsInsuranceValid_EmptyOrNullDate_ReturnsBadRequest(string date)
    {
        var carId = _context.Cars.First().Id;

        var result = await _controller.IsInsuranceValid(carId, date);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("required", badRequestResult.Value?.ToString()?.ToLower());
    }

    [Theory]
    [InlineData("invalid-date")]
    [InlineData("2024-13-01")]  // luna inv
    [InlineData("2024-02-30")]  // zi inv
    [InlineData("32/12/2024")]  // format 
    [InlineData("2024/12/32")]  
    [InlineData("abcd-ef-gh")]  
    public async Task IsInsuranceValid_InvalidDateFormat_ReturnsBadRequest(string date)
    {
        var carId = _context.Cars.First().Id;

        var result = await _controller.IsInsuranceValid(carId, date);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("Invalid date format", badRequestResult.Value?.ToString());
    }

    [Theory]
    [InlineData("1899-12-31")]  // <min
    [InlineData("2101-01-01")]  // >max
    public async Task IsInsuranceValid_ImpossibleDates_ReturnsBadRequest(string date)
    {
        var carId = _context.Cars.First().Id;

        var result = await _controller.IsInsuranceValid(carId, date);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("Date must be between", badRequestResult.Value?.ToString());
    }

    [Theory]
    [InlineData("2023-12-31", false)]  // inainte
    [InlineData("2024-01-01", true)]   // start
    [InlineData("2024-06-15", true)]   
    [InlineData("2024-12-31", true)]   // end
    [InlineData("2025-01-01", false)]  // dupa
    public async Task IsInsuranceValid_BoundaryDates_ReturnsCorrectValidity(string date, bool expectedValid)
    {
        var carId = _context.Cars.First().Id;

        var result = await _controller.IsInsuranceValid(carId, date);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<InsuranceValidityResponse>(okResult.Value);
        Assert.Equal(expectedValid, response.Valid);
    }

    [Fact]
    public async Task IsInsuranceValid_ValidDateFormat_AcceptsVariousFormats()
    {
        var carId = _context.Cars.First().Id;

        var validDates = new[] { "2024-06-01", "2024-6-1", "2024-06-1", "2024-6-01" };
        foreach (var date in validDates)
        {
            var result = await _controller.IsInsuranceValid(carId, date);
            Assert.IsType<OkObjectResult>(result.Result);
        }
    }

    public void Dispose() => _context.Dispose();
}
