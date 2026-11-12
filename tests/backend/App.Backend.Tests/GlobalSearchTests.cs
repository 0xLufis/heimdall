using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using App.Backend.Api.Controllers.V1;
using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace App.Backend.Tests;

/// <summary>
/// Global search: ranking rules, the five entity kinds, tenant scoping and the HTTP endpoint.
/// These run on the in-memory provider (case-insensitive LIKE instead of PostgreSQL ILIKE).
/// </summary>
public class GlobalSearchTests
{
    // ------------------------------------------------------------------ ranking

    [Fact]
    public void Ranker_OrdersExactPrefixWordSubstringTypo()
    {
        var names = new[] { "Microservo unit", "Main servomotor", "Main servo drive", "Servo", "Servo Motor 15kW", "Sevro amplifier", "Conveyor" };
        var ranked = names
            .Select(n => (Name: n, Score: SearchRanker.ScoreRecord(new[] { "servo" }, new[] { n })))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Name)
            .ToArray();

        // exact > prefix > whole word > start of a word > inside a word > typo; "Conveyor" does not match
        Assert.Equal(new[] { "Servo", "Servo Motor 15kW", "Main servo drive", "Main servomotor", "Microservo unit", "Sevro amplifier" }, ranked);
    }

    [Fact]
    public void Ranker_RequiresEveryToken_AndTakesTheBestField()
    {
        Assert.True(SearchRanker.ScoreRecord(new[] { "weld", "line2" }, new[] { "OP20 Weld Cell", "Line2" }) > 0);
        Assert.Equal(0, SearchRanker.ScoreRecord(new[] { "weld", "line9" }, new[] { "OP20 Weld Cell", "Line2" }));

        var containedInFirst = SearchRanker.ScoreRecord(new[] { "op20" }, new[] { "Line op20 buffer conveyor", "unrelated" });
        var exactInSecond = SearchRanker.ScoreRecord(new[] { "op20" }, new[] { "Line op20 buffer conveyor", "OP20" });
        Assert.True(exactInSecond > containedInFirst);
    }

    [Theory]
    [InlineData("op30", "OP20")]
    [InlineData("line9", "Line2")]
    [InlineData("l01-op030", "L01-OP020")]
    [InlineData("ab", "ac")]
    public void Ranker_NeverTypoMatchesIdentifiersOrShortTokens(string token, string field)
    {
        Assert.Equal(0, SearchRanker.ScoreRecord(new[] { token }, new[] { field }));
    }

    [Theory]
    [InlineData("siemns", "Synthetic Siemens S7-1500 CPU")]
    [InlineData("seimens", "Siemens")]
    [InlineData("bekchoff", "Beckhoff AX5000 Servo Drive")]
    public void Ranker_TypoMatchesScoreBelowLiteralMatches(string token, string field)
    {
        var score = SearchRanker.ScoreRecord(new[] { token }, new[] { field });
        Assert.InRange(score, 0.01, SearchRanker.SubstringFloor - 0.001);
        Assert.Equal(0, SearchRanker.ScoreRecord(new[] { token }, new[] { field }, fuzzy: false));
    }

    [Fact]
    public void Ranker_EditDistanceCountsATranspositionAsOneEdit()
    {
        Assert.Equal(1, SearchRanker.EditDistance("sevro", "servo"));
        Assert.Equal(1, SearchRanker.EditDistance("siemns", "siemens"));
        Assert.Equal(3, SearchRanker.EditDistance("kitten", "sitting"));
        Assert.Equal(0, SearchRanker.EditDistance("same", "same"));
    }

    [Fact]
    public void Ranker_TokenizeLowercasesAndCaps()
    {
        Assert.Equal(new[] { "l01-op020", "weld" }, SearchRanker.Tokenize("  L01-OP020\tWeld "));
        Assert.Empty(SearchRanker.Tokenize("   "));
        Assert.Equal(SearchRanker.MaxTokens, SearchRanker.Tokenize(string.Join(' ', Enumerable.Range(0, 30))).Length);
    }

    // ------------------------------------------------------------------ service

    private static readonly Guid StationA = Guid.Parse("d6c1202e-5e5b-5e17-a16d-94f3b7a70001");
    private static readonly Guid TicketA = Guid.Parse("f2af7680-8fd7-5e88-9d15-bafc1f0b11d5");
    private static readonly Guid TicketB = Guid.Parse("55a4802b-9a6d-5b7e-bebe-44f98bdad573");

    private static AppDbContext NewContext(string? name = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name ?? $"GlobalSearch_{Guid.NewGuid():N}").Options);

    internal static void Seed(AppDbContext db)
    {
        var siemens = new Manufacturer { Id = Guid.NewGuid(), Name = "Siemens" };
        var beckhoff = new Manufacturer { Id = Guid.NewGuid(), Name = "Beckhoff" };
        db.Manufacturers.AddRange(siemens, beckhoff);

        var stationA = new Machine
        {
            Id = StationA, Name = "L01-OP020", DisplayName = "L01-OP020 – Robo-RFID Pallet Scanner", CustomIdentifier = "LINE-01-OP020",
            PinnedObjectHandle = "L01-OP020", MachineType = "ConveyorTransfer", GroupId = "Line 01", Technology = "Test", OrganizationId = "org-a"
        };
        db.Machines.AddRange(
            stationA,
            new Machine
            {
                Id = Guid.NewGuid(), Name = "L01-OP030", DisplayName = "L01-OP030 – Giga-Gluer Dispenser", CustomIdentifier = "LINE-01-OP030",
                PinnedObjectHandle = "L01-OP030", MachineType = "Dispenser", GroupId = "Line 01", Technology = "Dispensing", OrganizationId = "org-a"
            },
            new Machine
            {
                Id = Guid.NewGuid(), Name = "L02-OP020", DisplayName = "L02-OP020 – Cooling-Plate Applicator", CustomIdentifier = "LINE-02-OP020",
                PinnedObjectHandle = "L02-OP020", MachineType = "Dispenser", GroupId = "Line 02", Technology = "Dispensing", OrganizationId = "org-b"
            });

        db.ClientPcs.AddRange(
            new ClientPc { Id = Guid.NewGuid(), Name = "IPC-L01-OP060-PLC", Hostname = "IPC-L01-OP060-PLC", IpAddress = "192.168.101.15", MacAddress = "02:65:54:01:65:05", MachineIdentifier = "HW-IPC-L01-OP060-PLC", OrganizationId = "org-a" },
            new ClientPc { Id = Guid.NewGuid(), Name = "IPC-L02-OP060-PLC", Hostname = "IPC-L02-OP060-PLC", IpAddress = "192.168.102.15", MacAddress = "02:65:54:02:66:05", MachineIdentifier = "HW-IPC-L02-OP060-PLC", OrganizationId = "org-b" });

        db.MaintenanceTickets.AddRange(
            new MaintenanceTicket { Id = TicketA, Title = "Servo Drive Fault", Description = "Observed intermittent servo drive fault during production cycle.", Status = "Open", Priority = "Critical", AssignedTo = "Rusty Cogsworth", Machine = stationA, OrganizationId = "org-a", CreatedAt = DateTimeOffset.UtcNow.AddHours(-2) },
            new MaintenanceTicket { Id = TicketB, Title = "Laser Calibration Required", Description = "Laser power drift during production on the seam welder.", Status = "Pending", Priority = "Low", AssignedTo = "Otto Mation", OrganizationId = "org-b", CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) });

        db.InventoryItems.AddRange(
            new HardwareComponent { Id = Guid.NewGuid(), Name = "SPARE-SIEM-S7-1500-0073", DisplayName = "Synthetic S7-1500 CPU 1516-3 PN/DP (Spare) #073", SerialNumber = "SN-FAKE-SIEM-0073", StorageLocation = "Warehouse Aisle C – Shelf 2-4", Manufacturer = siemens, OrganizationId = "org-a" },
            new HardwareComponent { Id = Guid.NewGuid(), Name = "SPARE-BECK-AX5000-0314", DisplayName = "Synthetic AX5000 Servo Drive 12A (Spare) #314", SerialNumber = "SN-FAKE-BECK-0314", StorageLocation = "Warehouse Aisle F – Shelf 3-5", Manufacturer = beckhoff, OrganizationId = "org-b" },
            new SoftwareAsset { Id = Guid.NewGuid(), Name = "COMP-SW-Opcenter-MES-L01-OP020-1", DisplayName = "Opcenter MES Edge Connector (L01-OP020)", SerialNumber = "SN-SYNTH-CMP-002-1", Manufacturer = siemens, OrganizationId = "org-a" },
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Generic Spare Fuse 10A", DisplayName = "Shared consumable", OrganizationId = null });

        db.AuthOrganizations.AddRange(new AuthOrganization { Id = "org-a", Name = "Line 01" }, new AuthOrganization { Id = "org-b", Name = "Line 02" });
        db.AuthUsers.AddRange(
            new AuthUser { Id = "usr-tensor", Name = "Tensor Flowski", Email = "tensor.flowski@fake-factory.internal", Role = HeimdallRoles.LeadEngineer },
            new AuthUser { Id = "usr-pap", Name = "Orsolya Pap", Email = "orsolya.pap@heimdall.dev", Role = HeimdallRoles.Engineer },
            new AuthUser { Id = "usr-root", Name = "Root System Administrator", Email = "sysadmin@heimdall.dev", Role = HeimdallRoles.SystemAdmin });
        db.AuthMembers.AddRange(
            new AuthMember { Id = "m-1", OrganizationId = "org-a", UserId = "usr-tensor" },
            new AuthMember { Id = "m-2", OrganizationId = "org-b", UserId = "usr-pap" });

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    private static async Task<GlobalSearchResponse> SearchAsync(
        string text, string? organizationId = null, bool users = true, int limit = 10, string[]? kinds = null, GlobalSearchOptions? options = null)
    {
        using var db = NewContext();
        Seed(db);
        db.CurrentOrganizationId = organizationId;
        return await new GlobalSearchService(db, options).SearchAsync(new GlobalSearchQuery { Text = text, Limit = limit, CanSearchUsers = users, Kinds = kinds });
    }

    [Fact]
    public async Task Search_FindsStationsByIdentifierFragmentAndKeepsThemOutOfInventory()
    {
        var result = await SearchAsync("op020");

        Assert.Equal(new[] { "L01-OP020", "L02-OP020" }, result.Stations.Select(s => s.Name).OrderBy(n => n));
        // The station rows live in the inventory table as well: they must not come back as inventory hits.
        Assert.Equal(new[] { "COMP-SW-Opcenter-MES-L01-OP020-1" }, result.Inventory.Select(i => i.Name));
        Assert.Equal("SoftwareAsset", result.Inventory[0].ItemType);
        Assert.All(result.Stations, s => Assert.True(s.Score > 0));
    }

    [Theory]
    [InlineData("LINE-01-OP020")]   // custom identifier
    [InlineData("l01 op020")]       // two tokens instead of the hyphenated name
    [InlineData("p020 robo")]       // infix + word of the display name
    public async Task Search_RanksTheExactStationFirst(string query)
    {
        var result = await SearchAsync(query, organizationId: "org-a");
        Assert.Equal("L01-OP020", result.Stations.First().Name);
    }

    [Theory]
    [InlineData("IPC-L01-OP060-PLC")]
    [InlineData("192.168.101.15")]
    [InlineData("168.101")]
    [InlineData("02:65:54:01:65:05")]
    [InlineData("54:01:65")]
    [InlineData("hw-ipc-l01")]
    public async Task Search_FindsControllersByHostnameIpAndMac(string query)
    {
        var result = await SearchAsync(query);
        Assert.Equal(new[] { "IPC-L01-OP060-PLC" }, result.Controllers.Select(c => c.Hostname));
    }

    [Theory]
    [InlineData("TKT-F2AF7680")]
    [InlineData("f2af")]
    [InlineData("servo fault")]
    [InlineData("rusty")]
    [InlineData("l01-op020 servo")]   // station name + title
    [InlineData("intermittent")]      // description only
    public async Task Search_FindsTicketsByNumberTitleStationAssigneeAndDescription(string query)
    {
        var result = await SearchAsync(query);
        var ticket = Assert.Single(result.Tickets);
        Assert.Equal("TKT-F2AF7680", ticket.TicketNumber);
        Assert.Equal("Servo Drive Fault", ticket.Title);
        Assert.Equal("L01-OP020", ticket.StationName);
        Assert.Equal("Rusty Cogsworth", ticket.AssignedTechnicianName);
    }

    [Theory]
    [InlineData("SN-FAKE-SIEM-0073", "SPARE-SIEM-S7-1500-0073")]
    [InlineData("s7-1500", "SPARE-SIEM-S7-1500-0073")]
    [InlineData("aisle c", "SPARE-SIEM-S7-1500-0073")]
    [InlineData("beckhoff ax5000", "SPARE-BECK-AX5000-0314")]   // manufacturer name + model
    public async Task Search_FindsInventoryBySerialModelLocationAndManufacturer(string query, string expected)
    {
        var result = await SearchAsync(query);
        Assert.Equal(expected, result.Inventory.First().Name);
    }

    [Fact]
    public async Task Search_FindsUsersByNameAndEmail()
    {
        Assert.Equal(new[] { "usr-tensor" }, (await SearchAsync("tensor")).Users.Select(u => u.Id));
        Assert.Equal(new[] { "usr-pap" }, (await SearchAsync("orsolya.pap@")).Users.Select(u => u.Id));
        Assert.Equal(HeimdallRoles.LeadEngineer, (await SearchAsync("flowski")).Users.Single().Role);
    }

    [Fact]
    public async Task Search_IsScopedToTheOrganizationOfTheContext()
    {
        // "l0" is part of station and controller names of both organizations, "production" of both
        // ticket descriptions and "spare" of inventory rows of both organizations.
        var orgA = await SearchAsync("l0", organizationId: "org-a", limit: 50);
        Assert.Equal(new[] { "L01-OP020", "L01-OP030" }, orgA.Stations.Select(s => s.Name).OrderBy(n => n));
        Assert.Equal(new[] { "IPC-L01-OP060-PLC" }, orgA.Controllers.Select(c => c.Hostname));
        Assert.Equal(new[] { "Servo Drive Fault" }, (await SearchAsync("production", organizationId: "org-a")).Tickets.Select(t => t.Title));
        // Organization-less rows are shared, as with the other tenant filters.
        Assert.Equal(new[] { "Generic Spare Fuse 10A", "SPARE-SIEM-S7-1500-0073" },
            (await SearchAsync("spare", organizationId: "org-a")).Inventory.Select(i => i.Name).OrderBy(n => n));

        var orgB = await SearchAsync("l0", organizationId: "org-b", limit: 50);
        Assert.Equal(new[] { "L02-OP020" }, orgB.Stations.Select(s => s.Name));
        Assert.Equal(new[] { "IPC-L02-OP060-PLC" }, orgB.Controllers.Select(c => c.Hostname));
        Assert.Equal(new[] { "Laser Calibration Required" }, (await SearchAsync("production", organizationId: "org-b")).Tickets.Select(t => t.Title));
        Assert.Equal(new[] { "Generic Spare Fuse 10A", "SPARE-BECK-AX5000-0314" },
            (await SearchAsync("spare", organizationId: "org-b")).Inventory.Select(i => i.Name).OrderBy(n => n));

        // A ticket of another organization is not reachable through the station it refers to either.
        Assert.Empty((await SearchAsync("l01-op020", organizationId: "org-b")).Tickets);

        var unscoped = await SearchAsync("l0", organizationId: null, limit: 50);
        Assert.Equal(3, unscoped.Stations.Count);
        Assert.Equal(2, unscoped.Controllers.Count);
        Assert.Equal(2, (await SearchAsync("production", organizationId: null)).Tickets.Count);
        Assert.Equal(3, (await SearchAsync("spare", organizationId: null)).Inventory.Count);
    }

    [Fact]
    public async Task Search_UsersAreLimitedToMembersOfTheOrganization()
    {
        // "or" is in every user's name (TensOR, ORsolya, AdministratOR).
        Assert.Equal(new[] { "usr-tensor" }, (await SearchAsync("or", organizationId: "org-a")).Users.Select(u => u.Id));
        Assert.Equal(new[] { "usr-pap" }, (await SearchAsync("or", organizationId: "org-b")).Users.Select(u => u.Id));
        // A caller without an organization (fail-closed sentinel) sees nobody.
        Assert.Empty((await SearchAsync("or", organizationId: "__heimdall_no_organization__")).Users);
        // Platform administrators are unscoped.
        Assert.Equal(3, (await SearchAsync("or", organizationId: null)).Users.Count);
    }

    [Fact]
    public async Task Search_LeavesUsersOutWhenTheCallerMayNotSeeTheDirectory()
    {
        var result = await SearchAsync("tensor", users: false);
        Assert.Empty(result.Users);
        Assert.DoesNotContain(SearchKinds.Users, result.Kinds);

        var explicitKind = await SearchAsync("tensor", users: false, kinds: new[] { SearchKinds.Users });
        Assert.Empty(explicitKind.Users);
        Assert.Empty(explicitKind.Kinds);
    }

    [Fact]
    public async Task Search_HonoursKindsAndLimit()
    {
        var result = await SearchAsync("l0", kinds: new[] { SearchKinds.Stations, SearchKinds.Tickets }, limit: 2);
        Assert.Equal(new[] { SearchKinds.Stations, SearchKinds.Tickets }, result.Kinds);
        Assert.Equal(2, result.Stations.Count);
        Assert.Empty(result.Controllers);
        Assert.Empty(result.Inventory);
        Assert.Empty(result.Users);
    }

    [Fact]
    public async Task Search_ToleratesTyposInWordsButNotInIdentifiers()
    {
        var typo = await SearchAsync("sevro");
        Assert.Equal(new[] { "Servo Drive Fault" }, typo.Tickets.Select(t => t.Title));
        Assert.Equal(new[] { "SPARE-BECK-AX5000-0314" }, typo.Inventory.Select(i => i.Name));
        Assert.True(typo.Tickets[0].Score < SearchRanker.SubstringFloor);
        Assert.Equal("full", typo.TypoTolerance);

        // A misspelled manufacturer still leads to its parts.
        Assert.Contains("SPARE-SIEM-S7-1500-0073", (await SearchAsync("siemns")).Inventory.Select(i => i.Name));

        // OP030 exists, OP040 does not: nothing may be "corrected" to a neighbouring station.
        Assert.Equal(new[] { "L01-OP030" }, (await SearchAsync("op030")).Stations.Select(s => s.Name));
        Assert.Empty((await SearchAsync("op040")).Stations);
    }

    [Fact]
    public async Task Search_TypoMatchingIsAFallbackForKindsWithoutALiteralMatch()
    {
        using var db = NewContext();
        db.InventoryItems.AddRange(
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Sevro amplifier" },
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Main servo drive" },
            new HardwareComponent { Id = Guid.NewGuid(), Name = "Servo" });
        db.SaveChanges();
        var service = new GlobalSearchService(db);

        // Literal matches exist: they are returned in relevance order and nothing is "corrected".
        var literal = await service.SearchAsync(new GlobalSearchQuery { Text = "servo", Limit = 10 });
        Assert.Equal(new[] { "Servo", "Main servo drive" }, literal.Inventory.Select(i => i.Name));

        // No literal match: rows within typo distance are returned, ranked below any literal score
        // ("sevro" is two edits away from "serwo", which is too far for a five-letter word).
        var typo = await service.SearchAsync(new GlobalSearchQuery { Text = "serwo", Limit = 10 });
        Assert.Equal(new[] { "Main servo drive", "Servo" }, typo.Inventory.Select(i => i.Name).OrderBy(n => n));
        Assert.All(typo.Inventory, i => Assert.True(i.Score < SearchRanker.SubstringFloor));
    }

    [Fact]
    public async Task Search_ReportsPartialTypoToleranceForLargeKindsWithoutTrigrams()
    {
        // Three inventory rows could match; with a scan window of one row and no pg_trgm only part of them is checked.
        var options = new GlobalSearchOptions { TypoScanLimit = 1, TrigramsAvailable = false };
        var result = await SearchAsync("sevro", kinds: new[] { SearchKinds.Inventory }, options: options);
        Assert.Equal("partial", result.TypoTolerance);

        // Literal matching is unaffected by the window.
        var literal = await SearchAsync("servo", kinds: new[] { SearchKinds.Inventory }, options: options);
        Assert.Equal(new[] { "SPARE-BECK-AX5000-0314" }, literal.Inventory.Select(i => i.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public async Task Search_ReturnsNothingForQueriesShorterThanTwoCharacters(string? query)
    {
        var result = await SearchAsync(query!);
        Assert.Empty(result.Stations);
        Assert.Empty(result.Controllers);
        Assert.Empty(result.Tickets);
        Assert.Empty(result.Users);
        Assert.Empty(result.Inventory);
    }

    [Theory]
    [InlineData("%%")]
    [InlineData("__")]
    [InlineData("\\\\")]
    [InlineData("'; DROP TABLE x; --")]
    public async Task Search_TreatsLikeWildcardsAndQuotesAsLiteralText(string query)
    {
        var result = await SearchAsync(query);
        Assert.Empty(result.Stations);
        Assert.Empty(result.Controllers);
        Assert.Empty(result.Tickets);
        Assert.Empty(result.Users);
        Assert.Empty(result.Inventory);
    }

    [Fact]
    public async Task Search_CapsTheQueryLength()
    {
        var result = await SearchAsync(new string('x', 5000));
        Assert.Equal(GlobalSearchService.MaxQueryLength, result.Query.Length);
    }

    // ------------------------------------------------------------------ controller

    private static IAuthorizationService BuildAuthorization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(o =>
        {
            o.AddPolicy(AuthorizationPolicies.SystemAdministration, p => p.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.PlantDirector));
            o.AddPolicy(AuthorizationPolicies.ItAdministration, p => p.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.ItAdmin, HeimdallRoles.ItSiteAdmin));
            o.AddPolicy(AuthorizationPolicies.EngineeringAdministration, p => p.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.SeniorEngineeringManager));
        });
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static SearchController ControllerFor(AppDbContext db, string role)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "Test"));
        return new SearchController(db, BuildAuthorization())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };
    }

    private static GlobalSearchResponse Body(ActionResult<GlobalSearchResponse> result) =>
        Assert.IsType<GlobalSearchResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Theory]
    [InlineData(HeimdallRoles.Technician, false)]
    [InlineData(HeimdallRoles.Engineer, false)]
    [InlineData(HeimdallRoles.Operator, false)]
    [InlineData(HeimdallRoles.EngineeringAdmin, true)]
    [InlineData(HeimdallRoles.ItAdmin, true)]
    [InlineData(HeimdallRoles.SystemAdmin, true)]
    public async Task Controller_ReturnsUsersOnlyToRolesThatManageUsers(string role, bool expectUsers)
    {
        using var db = NewContext();
        Seed(db);

        var body = Body(await ControllerFor(db, role).Search("tensor"));

        Assert.Equal(expectUsers, body.Users.Count == 1);
        Assert.Equal(expectUsers, body.Kinds.Contains(SearchKinds.Users));
    }

    [Fact]
    public async Task Controller_ParsesKindAliasesAndRejectsUnknownKinds()
    {
        using var db = NewContext();
        Seed(db);
        var controller = ControllerFor(db, HeimdallRoles.Technician);

        var body = Body(await controller.Search("op020", kinds: "machine, parts ,stations", limit: 1));
        Assert.Equal(new[] { SearchKinds.Stations, SearchKinds.Inventory }, body.Kinds);
        Assert.Single(body.Stations);
        Assert.Empty(body.Controllers);

        var rejected = await controller.Search("op020", kinds: "stations,secrets");
        Assert.IsType<BadRequestObjectResult>(rejected.Result);
    }

    [Fact]
    public void Controller_RequiresAuthentication()
    {
        Assert.NotEmpty(typeof(SearchController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
    }
}

/// <summary>
/// The endpoint behind the real pipeline: Better-Auth session lookup, tenant scope middleware,
/// authorization policies and JSON contract.
/// </summary>
public class GlobalSearchEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GlobalSearchEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.AuthSessions.Any(s => s.Token == "search-tok-technician-a")) return;

        GlobalSearchTests.Seed(db);
        db.AuthUsers.AddRange(
            new AuthUser { Id = "usr-tech-a", Name = "Relay McSolenoid", Email = "relay@fake-factory.internal", Role = HeimdallRoles.Technician },
            new AuthUser { Id = "usr-engadmin-a", Name = "Vectoria Embeddings", Email = "vectoria@fake-factory.internal", Role = HeimdallRoles.EngineeringAdmin });
        db.AuthMembers.AddRange(
            new AuthMember { Id = "m-tech-a", OrganizationId = "org-a", UserId = "usr-tech-a" },
            new AuthMember { Id = "m-engadmin-a", OrganizationId = "org-a", UserId = "usr-engadmin-a" });
        db.AuthSessions.AddRange(
            new AuthSession { Id = "s-tech-a", Token = "search-tok-technician-a", UserId = "usr-tech-a", ActiveOrganizationId = "org-a", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) },
            new AuthSession { Id = "s-engadmin-a", Token = "search-tok-engadmin-a", UserId = "usr-engadmin-a", ActiveOrganizationId = "org-a", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        db.SaveChanges();
    }

    private async Task<(HttpStatusCode Status, GlobalSearchResponse? Body, string Raw)> GetAsync(string url, string? token)
    {
        var client = _factory.CreateClient();
        if (token != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync(url);
        var raw = await response.Content.ReadAsStringAsync();
        var body = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<GlobalSearchResponse>() : null;
        return (response.StatusCode, body, raw);
    }

    [Fact]
    public async Task Technician_GetsOnlyRowsOfTheActiveOrganization_AndNoUsers()
    {
        var (status, body, raw) = await GetAsync("/api/v1/search?q=l0&limit=50", "search-tok-technician-a");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new[] { "L01-OP020", "L01-OP030" }, body!.Stations.Select(s => s.Name).OrderBy(n => n));
        Assert.Equal(new[] { "IPC-L01-OP060-PLC" }, body.Controllers.Select(c => c.Hostname));
        Assert.Equal(new[] { "Servo Drive Fault" }, body.Tickets.Select(t => t.Title));
        Assert.Equal(new[] { "COMP-SW-Opcenter-MES-L01-OP020-1" }, body.Inventory.Select(i => i.Name));
        Assert.Empty(body.Users);
        Assert.DoesNotContain("users", body.Kinds);
        // Nothing of the other organization leaks into the payload in any field.
        Assert.DoesNotContain("L02-", raw);
        Assert.DoesNotContain("orsolya", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UserManager_FindsOnlyMembersOfTheActiveOrganization()
    {
        var (status, body, _) = await GetAsync("/api/v1/search?q=or&kinds=users&limit=50", "search-tok-engadmin-a");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new[] { "usr-engadmin-a", "usr-tech-a", "usr-tensor" }, body!.Users.Select(u => u.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task Response_UsesTheFieldNamesTheFrontendMappersRead()
    {
        var (_, _, raw) = await GetAsync("/api/v1/search?q=servo", "search-tok-technician-a");

        foreach (var field in new[] { "\"ticketNumber\":\"TKT-F2AF7680\"", "\"stationName\":\"L01-OP020\"", "\"assignedTechnicianName\":\"Rusty Cogsworth\"", "\"typoTolerance\":", "\"score\":" })
        {
            Assert.Contains(field, raw);
        }
    }

    [Fact]
    public async Task UnknownKind_IsABadRequest()
    {
        var (status, _, _) = await GetAsync("/api/v1/search?q=servo&kinds=everything", "search-tok-technician-a");
        Assert.Equal(HttpStatusCode.BadRequest, status);
    }
}

/// <summary>
/// The SQL paths against a real PostgreSQL server: ILIKE without any extension, then pg_trgm.
/// Opt-in: set HEIMDALL_TEST_POSTGRES to an administrative connection string (it must be allowed
/// to create and drop a throwaway database), e.g.
/// <c>Host=localhost;Port=5432;Username=postgres;Password=...;Database=postgres</c>.
/// </summary>
public class GlobalSearchPostgresTests
{
    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HEIMDALL_TEST_POSTGRES")))
            {
                Skip = "Set HEIMDALL_TEST_POSTGRES to run against PostgreSQL.";
            }
        }
    }

    private static AppDbContext Context(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options);

    [Fact]
    public void Migration_IsDiscoveredAndOrderedLast()
    {
        // No connection is opened: the migration list comes from the assembly.
        using var db = Context("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused");
        Assert.Equal("20261111103000_SearchTrigramIndexes", db.Database.GetMigrations().Last());
    }

    [PostgresFact]
    public async Task Search_WorksWithPlainIlike_AndUsesTrigramsOnceTheMigrationSqlIsApplied()
    {
        var admin = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("HEIMDALL_TEST_POSTGRES")!);
        var database = $"heimdall_search_it_{Guid.NewGuid():N}";
        var target = new Npgsql.NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = database, Pooling = false }.ConnectionString;

        await using var adminDb = Context(admin.ConnectionString);
        await adminDb.Database.ExecuteSqlRawAsync($"CREATE DATABASE {database}");
        try
        {
            await using var db = Context(target);
            await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA backend; CREATE SCHEMA auth;");
            await db.Database.EnsureCreatedAsync();
            // The Better-Auth tables are not part of the EF schema.
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS auth.organization (id text PRIMARY KEY, name text NOT NULL);" +
                "CREATE TABLE IF NOT EXISTS auth.\"user\" (id text PRIMARY KEY, name text NOT NULL, email text NOT NULL, role text);" +
                "CREATE TABLE IF NOT EXISTS auth.member (id text PRIMARY KEY, organization_id text NOT NULL, user_id text NOT NULL);");
            GlobalSearchTests.Seed(db);

            async Task<GlobalSearchResponse> Search(string text, string? org = null, GlobalSearchOptions? options = null)
            {
                await using var scoped = Context(target);
                scoped.CurrentOrganizationId = org;
                return await new GlobalSearchService(scoped, options).SearchAsync(new GlobalSearchQuery { Text = text, Limit = 10, CanSearchUsers = true });
            }

            // --- no extension: case-insensitive substring matching, every kind, tenant scoped in SQL
            var literal = await Search("OP020");
            Assert.Equal(new[] { "L01-OP020", "L02-OP020" }, literal.Stations.Select(s => s.Name).OrderBy(n => n));
            Assert.Equal(new[] { "COMP-SW-Opcenter-MES-L01-OP020-1" }, literal.Inventory.Select(i => i.Name));
            Assert.Equal("SoftwareAsset", literal.Inventory[0].ItemType);
            Assert.Equal("TKT-F2AF7680", (await Search("tkt-f2af")).Tickets.Single().TicketNumber);
            Assert.Equal("L01-OP020", (await Search("servo fault")).Tickets.Single().StationName);
            Assert.Equal(new[] { "IPC-L01-OP060-PLC" }, (await Search("54:01:65")).Controllers.Select(c => c.Hostname));
            Assert.Equal(new[] { "SPARE-BECK-AX5000-0314" }, (await Search("beckhoff ax5000")).Inventory.Select(i => i.Name));
            Assert.Empty((await Search("100%")).Inventory);
            Assert.Equal(new[] { "L01-OP020", "L01-OP030" }, (await Search("l0", "org-a")).Stations.Select(s => s.Name).OrderBy(n => n));
            Assert.Equal(new[] { "usr-tensor" }, (await Search("or", "org-a")).Users.Select(u => u.Id));
            Assert.Empty((await Search("l01-op020", "org-b")).Tickets);

            // typo fallback by scanning (small tables), and "partial" when the window is too small and pg_trgm is missing
            var scanned = await Search("serwo");
            Assert.Equal(new[] { "Servo Drive Fault" }, scanned.Tickets.Select(t => t.Title));
            Assert.Equal("full", scanned.TypoTolerance);
            Assert.Equal("partial", (await Search("serwo", options: new GlobalSearchOptions { TypoScanLimit = 1 })).TypoTolerance);

            // --- the migration's statements, twice: idempotent
            for (var pass = 0; pass < 2; pass++)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;" +
                    "CREATE INDEX IF NOT EXISTS ix_inventory_items_name_trgm ON backend.inventory_items USING gin (name public.gin_trgm_ops);" +
                    "CREATE INDEX IF NOT EXISTS ix_inventory_items_display_name_trgm ON backend.inventory_items USING gin (display_name public.gin_trgm_ops);");
            }

            // --- pg_trgm narrows the typo candidates when a kind is larger than the scan window
            var trigram = await Search("serwo", options: new GlobalSearchOptions { TypoScanLimit = 1, TrigramsAvailable = true });
            Assert.Equal("trigram", trigram.TypoTolerance);
            Assert.Equal(new[] { "Servo Drive Fault" }, trigram.Tickets.Select(t => t.Title));
            Assert.Equal(new[] { "SPARE-BECK-AX5000-0314" }, trigram.Inventory.Select(i => i.Name));
            // A misspelled manufacturer still leads to its parts (the one-row window keeps the first by name).
            Assert.Equal(new[] { "COMP-SW-Opcenter-MES-L01-OP020-1" },
                (await Search("siemns", options: new GlobalSearchOptions { TypoScanLimit = 1, TrigramsAvailable = true })).Inventory.Select(i => i.Name));
        }
        finally
        {
            await adminDb.Database.ExecuteSqlRawAsync($"DROP DATABASE IF EXISTS {database} WITH (FORCE)");
        }
    }
}
