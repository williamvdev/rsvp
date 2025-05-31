// Program.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<RsvpContext>(options =>
    options.UseSqlite("Data Source=rsvp.db"));
builder.Services.AddCors();
builder.Services.AddControllers();

var app = builder.Build();

// Configure pipeline
app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.MapControllers();

// Create database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<RsvpContext>();
    context.Database.EnsureCreated();

    // Seed some test data if empty
    if (!context.Parties.Any())
    {
        var party1 = new Party
        {
            Code = "ABC123",
            Guests = new List<Guest>
            {
                new Guest { FullName = "John Doe" }
            }
        };
        var party2 = new Party
        {
            Code = "XYZ789",
            Guests = new List<Guest>
            {
                new Guest { FullName = "Jane Smith" },
                new Guest { FullName = "Bob Smith" }
            }
        };
        context.Parties.AddRange(party1, party2);
        context.SaveChanges();
    }
}

app.Run();

// Models
public class Party
{
    public int Id { get; set; }
    [Required]
    public string Code { get; set; } = "";
    public List<Guest> Guests { get; set; } = new();
}

public class Guest
{
    public int Id { get; set; }
    [Required]
    public string FullName { get; set; } = "";
    public bool? AttendingFriday { get; set; }
    public bool? AttendingSaturday { get; set; }
    public string? MealPreference { get; set; }
    public string? MusicSuggestions { get; set; }
    public int PartyId { get; set; }
    public Party? Party { get; set; }
}

// DbContext
public class RsvpContext : DbContext
{
    public RsvpContext(DbContextOptions<RsvpContext> options) : base(options) { }
    public DbSet<Party> Parties { get; set; }
    public DbSet<Guest> Guests { get; set; }
}

// DTOs
public record GuestDto(
    int Id,
    string FullName,
    bool? AttendingFriday,
    bool? AttendingSaturday,
    string? MealPreference,
    string? MusicSuggestions
);

public record VerifyCodeRequest(string Code);
public record VerifyCodeResponse(List<GuestDto> Guests);

public record SubmitRsvpRequest(
    string Code,
    List<GuestDto> Guests
);

// Controller
[ApiController]
[Route("/api/rsvp")]
public class RsvpController : ControllerBase
{
    private readonly RsvpContext _context;

    public RsvpController(RsvpContext context)
    {
        _context = context;
    }

    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode(VerifyCodeRequest request)
    {
        var party = await _context.Parties
            .Include(p => p.Guests)
            .FirstOrDefaultAsync(p => p.Code == request.Code);

        if (party == null)
            return NotFound();

        var guests = party.Guests.Select(g => new GuestDto(
            g.Id,
            g.FullName,
            g.AttendingFriday,
            g.AttendingSaturday,
            g.MealPreference,
            g.MusicSuggestions
        )).ToList();

        return Ok(new VerifyCodeResponse(guests));
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitRsvp(SubmitRsvpRequest request)
    {
        var party = await _context.Parties
            .Include(p => p.Guests)
            .FirstOrDefaultAsync(p => p.Code == request.Code);

        if (party == null)
            return NotFound();

        foreach (var guestDto in request.Guests)
        {
            var guest = party.Guests.FirstOrDefault(g => g.Id == guestDto.Id);
            if (guest != null)
            {
                guest.AttendingFriday = guestDto.AttendingFriday;
                guest.AttendingSaturday = guestDto.AttendingSaturday;
                guest.MealPreference = guestDto.MealPreference;
                guest.MusicSuggestions = guestDto.MusicSuggestions;
            }
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
}