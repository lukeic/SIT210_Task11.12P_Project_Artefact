using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Natura.Server;
using Natura.Server.Data;
using Natura.Server.Services;

namespace Natura.PlantDataLoader
{
	class Program
	{
		private static ServiceProvider _provider;
		private static ApplicationDbContext _db;

		static async Task Main(string[] args)
		{
			var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
			var services = new ServiceCollection();
			services.AddNaturaServices();
			services.AddDistributedMemoryCache();

			if (isDevelopment)
			{
				services.AddDbContext<ApplicationDbContext>(
					options => options.UseSqlite("Data Source=Natura.db"));
			}
			else
			{
				var connstr = Environment.GetEnvironmentVariable("CUSTOMCONNSTR_SQLSERVER");
				if (string.IsNullOrEmpty(connstr))
				{
					throw new Exception("Production database connection-string is missing");
				}

				services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connstr));
			}

			_provider = services.BuildServiceProvider();
			_db = _provider.GetRequiredService<ApplicationDbContext>();
			if (isDevelopment)
			{
				await _db.Database.MigrateAsync();
			}

			await SeedDatabase();
		}

		private static async Task SeedDatabase()
		{
			var gbifClient = _provider.GetRequiredService<GbifClient>();
			var data = await gbifClient.GetPlantSeedData();
			var plants = new List<Server.Models.Plant>();
			foreach (var occurence in data)
			{
				var commonNames = await gbifClient.FindSpeciesVernacularNames(occurence.SpeciesKey);
				var plant = new Server.Models.Plant(
					occurence.Species,
					commonNames,
					occurence.SpeciesKey.ToString());

				plants.Add(plant);
			}

			await _db.Plants.AddRangeAsync(plants);
			await _db.SaveChangesAsync();
		}
	}
}
