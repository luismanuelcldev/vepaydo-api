using System.Globalization;
using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Charges;

public sealed partial class ChargeServiceTests
{

    [Fact]
    public async Task Import_reports_invalid_rows_and_inserts_the_valid_ones()
    {
        var harness = new Harness();
        var rows = new List<ChargeImportRow>
        {
            Row(1, "Buena", 100m),
            Row(2, "Mala", 0m),
            Row(3, "Otra", 200m),
        };

        var result = await harness.Service.ImportAsync(rows, allOrNothing: false, Meta(), CancellationToken.None);

        result.Imported.Should().Be(2);
        result.Skipped.Should().Be(1);
        result.Errors.Should().ContainSingle().Which.RowNumber.Should().Be(2);
        harness.Repo.Added.Should().HaveCount(2);
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("import");
    }

    [Fact]
    public async Task Import_all_or_nothing_writes_nothing_on_any_error()
    {
        var harness = new Harness();
        var rows = new List<ChargeImportRow>
        {
            Row(1, "Buena", 100m),
            Row(2, "Mala", -5m),
        };

        var result = await harness.Service.ImportAsync(rows, allOrNothing: true, Meta(), CancellationToken.None);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(2);
        result.Errors.Should().ContainSingle();
        harness.Repo.Added.Should().BeEmpty();
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("import");
    }

    [Fact]
    public async Task Create_retries_with_a_new_code_when_the_unique_index_wins_the_race()
    {
        var harness = new Harness();
        harness.Repo.ConflictsBeforeSuccess = 1;

        var view = await harness.Service.CreateAsync(
            new CreateChargeInput(null, null, "Servicio de marzo", 1500m, "DOP", new DateOnly(2026, 3, 1), null),
            Meta(),
            CancellationToken.None);

        view.Code.Should().Be("SER-000043");
        harness.Repo.Added.Should().ContainSingle();
    }

    [Fact]
    public async Task List_with_an_unknown_status_fails_closed()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.ListAsync(new ChargeQuery("NOPE", null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }
}
