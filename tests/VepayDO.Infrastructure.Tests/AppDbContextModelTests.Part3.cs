using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Tests;

public sealed partial class AppDbContextModelTests
{

    private static AppDbContext CreateModel()
    {
        var dataSource = AppDataSource.Build(
            "Host=localhost;Port=5432;Database=model;Username=model;Password=model");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(dataSource, AppDataSource.ConfigureEnums)
            .Options;

        return new AppDbContext(options);
    }
}
