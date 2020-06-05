using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Natura.Server.Models;

namespace Natura.Server.Data
{
	public class ApplicationDbContext : IdentityDbContext<User>
	{
		public ApplicationDbContext(DbContextOptions options) : base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			builder.Entity<User>(
				entity =>
				{
					entity.HasData(
						new User
						{
							Id = "natura",
							Email = "natura@natura.com",
							EmailConfirmed = true,
							UserName = "natura"
						});
				});

			builder.Entity<Models.Plant>(
				entity =>
				{
					entity.ToTable("Plants");

					entity
						.HasIndex(x => x.ScientificName)
						.IsUnique();

					entity
						.HasMany(x => x.PlantNames)
						.WithOne(x => x.Plant);
				});

			builder.Entity<UserPlant>(
				entity =>
				{
					entity.ToTable("UserPlants");

					entity.HasKey(x => new { x.UserId, x.PlantId });

					entity
						.HasOne(x => x.User)
						.WithMany(x => x.UserPlants)
						.HasForeignKey(x => x.UserId);

					entity
						.HasOne(x => x.Plant)
						.WithMany(x => x.UserPlants)
						.HasForeignKey(x => x.PlantId);
				});
		}

		public DbSet<Models.Plant> Plants { get; set; } = null!;
	}
}
