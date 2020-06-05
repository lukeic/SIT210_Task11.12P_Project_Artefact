using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Natura.Server.Data;
using Natura.Server.Models;
using Natura.Server.Services.Interfaces;
using Natura.Server.Services.Output;

namespace Natura.Server.Controllers
{
	[ApiController]
	[Route("api/identify")]
	public class PlantIdentificationController : ControllerBase
	{
		private readonly IPlantIdentifier _plantIdentifier;
		private readonly SignInManager<User> _signInManager;
		private readonly UserManager<User> _userManager;
		private readonly ILogger<PlantIdentificationController> _logger;
		private readonly ApplicationDbContext _db;

		public PlantIdentificationController(
			IPlantIdentifier plantIdentifier,
			SignInManager<User> signInManager,
			UserManager<User> userManager,
			ILogger<PlantIdentificationController> logger,
			ApplicationDbContext db)
		{
			_plantIdentifier = plantIdentifier;
			_signInManager = signInManager;
			_userManager = userManager;
			_logger = logger;
			_db = db;
		}

		[HttpGet]
		public async Task<ActionResult<IEnumerable<string>>> IdentifyPlant([FromQuery] string image)
		{
			PlantOutput? plant;
			try
			{
				plant = await _plantIdentifier.Identify(image);
			}
			catch (Exception e)
			{
				_logger.LogError($"Failed to indentify plant: {e.Message}");
				return Problem();
			}

			return await ProcessPlant(plant);
		}

		[HttpPost]
		public async Task<ActionResult<IEnumerable<string>>> IdentifyPlant()
		{
			using var inMemoryImage = new MemoryStream();
			await Request.Body.CopyToAsync(inMemoryImage);
			PlantOutput? plant;
			try
			{
				plant = await _plantIdentifier.Identify(inMemoryImage);
			}
			catch (Exception e)
			{
				_logger.LogError($"Failed to identify plant: {e.Message}");
				return Problem();
			}

			return await ProcessPlant(plant);
		}

		private async Task<ActionResult<IEnumerable<string>>> ProcessPlant(PlantOutput? plant)
		{
			if (plant == null)
			{
				return NotFound();
			}

			var user = await _userManager.Users
				.Include(x => x.UserPlants)
				.ThenInclude(x => x.Plant)
				.ThenInclude(x => x.PlantNames)
				.SingleAsync(x => x.Id == "natura");

			var existingPlant =
				await _db.Plants.SingleOrDefaultAsync(x => x.ScientificName == plant.ScentificName);

			existingPlant?.AddMissingCommonNames(plant.CommonNames.ToList());

			var plantToAdd = existingPlant ?? new Models.Plant(
				plant.ScentificName,
				plant.CommonNames,
				plant.GbifId);

			user.AddPlantToCollection(plantToAdd);

			var update = await _userManager.UpdateAsync(user);
			if (!update.Succeeded)
			{
				_logger.LogError($"Failed to add plant to user's collection: {update.Errors}");
				return Problem();
			}

			return Ok(plant.CommonNames.Any() ? plant.CommonNames : new[] { plant.ScentificName });
		}
	}
}
