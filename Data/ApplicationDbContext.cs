using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<GlobaLogCredential> GlobaLogCredentials => Set<GlobaLogCredential>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<FlightCrew> FlightCrew => Set<FlightCrew>();
    public DbSet<CrewMember> CrewMembers => Set<CrewMember>();
    public DbSet<CrewRole> CrewRoles => Set<CrewRole>();
    public DbSet<Aircraft> Aircraft => Set<Aircraft>();
    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<PreviousExperience> PreviousExperience => Set<PreviousExperience>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<IcaoAircraftType> IcaoAircraftTypes => Set<IcaoAircraftType>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<GlobaLogCredential>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MyCrewMember).WithMany().HasForeignKey(x => x.MyCrewMemberId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PreviousExperience>(e =>
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade));

        // An ICAO aircraft type can't be deleted while aircraft are linked to it.
        builder.Entity<Aircraft>(e =>
            e.HasOne(x => x.IcaoAircraftType).WithMany().HasForeignKey(x => x.IcaoAircraftTypeId).OnDelete(DeleteBehavior.Restrict));

        // A manufacturer can't be deleted while aircraft types are linked to it.
        builder.Entity<IcaoAircraftType>(e =>
            e.HasOne(x => x.Manufacturer).WithMany(m => m.AircraftTypes).HasForeignKey(x => x.ManufacturerId).OnDelete(DeleteBehavior.Restrict));

        builder.Entity<SyncState>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Flight>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Crew).WithOne(c => c.Flight!).HasForeignKey(c => c.FlightId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CrewRole>(e => e.HasData(CrewRole.Seed));
        builder.Entity<FlightCrew>(e =>
            e.HasOne(x => x.CrewRole).WithMany().HasForeignKey(x => x.CrewRoleId).OnDelete(DeleteBehavior.Restrict));
        builder.Entity<Region>(e =>
            e.HasOne(x => x.Country).WithMany(c => c.Regions).HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict));
        builder.Entity<Airport>(e =>
        {
            e.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Flight>(e =>
        {
            e.HasOne(x => x.DepartureAirport).WithMany().HasForeignKey(x => x.DepartureAirportId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ArrivalAirport).WithMany().HasForeignKey(x => x.ArrivalAirportId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Aircraft>(e =>
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade));
        builder.Entity<Flight>(e =>
            e.HasOne(x => x.Aircraft).WithMany().HasForeignKey(x => x.AircraftId).OnDelete(DeleteBehavior.Restrict));
        builder.Entity<Flight>(e =>
            e.HasOne(x => x.MyCrewRole).WithMany().HasForeignKey(x => x.MyCrewRoleId).OnDelete(DeleteBehavior.Restrict));

        builder.Entity<CrewMember>(e =>
        {
            e.HasOne(x => x.LatestCrewRole).WithMany().HasForeignKey(x => x.LatestCrewRoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Flights).WithOne(c => c.CrewMember!).HasForeignKey(c => c.CrewMemberId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
