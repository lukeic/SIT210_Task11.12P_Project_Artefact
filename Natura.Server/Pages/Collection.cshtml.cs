using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Natura.Server.Data;
using Natura.Server.Models;
using Natura.Server.Services;
using Natura.Server.Services.Output;

namespace Natura.Server.Pages
{
	public class Collection : PageModel
	{
		private readonly UserManager<User> _userManager;
		private readonly ApplicationDbContext _db;
		private readonly GbifClient _gbifClient;

		public Collection(UserManager<User> userManager, ApplicationDbContext db, GbifClient gbifClient)
		{
			_userManager = userManager;
			_db = db;
			_gbifClient = gbifClient;
		}

		[ViewData]
		public string Title { get; } = "Plant Collection";

		public IReadOnlyCollection<Models.Plant> UserPlantCollection { get; private set; } =
			new List<Models.Plant>();

		public IList<Models.Plant> AllPlants { get; private set; } =
			new List<Models.Plant>();

		public ConcurrentDictionary<Models.Plant, GbifImage?> PlantsWithImages { get; set; } =
			new ConcurrentDictionary<Models.Plant, GbifImage?>();

		public async Task<IActionResult> OnGet()
		{
			var user = await _userManager.Users
				.AsNoTracking()
				.Include(x => x.UserPlants)
				.ThenInclude(x => x.Plant)
				.ThenInclude(x => x.PlantNames)
				.SingleAsync(x => x.Id == "natura");

			UserPlantCollection = user.PlantCollection;
			AllPlants = await _db.Plants
				.AsNoTracking()
				.Include(x => x.PlantNames)
				.ToListAsync();

			var rnd = new Random();
			foreach (var plant in AllPlants.AsParallel())
			{
				if (!string.IsNullOrWhiteSpace(plant.GbifSpeciesKey))
				{
					var images = await _gbifClient.FindImagesForSpecies(plant.GbifSpeciesKey);
					GbifImage? image = null;
					if (images.Any())
					{
						var randomIndex = rnd.Next(0, images.Count - 1);
						image = images.ElementAt(randomIndex);
					}

					PlantsWithImages.TryAdd(plant, image);
				}
			}

			return Page();
		}

		public bool HasCollectedPlant(Models.Plant plant)
		{
			return UserPlantCollection.Contains(plant);
		}
	}
}
