using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.AspNetCore.Identity;

namespace Natura.Server.Models
{
	public class User : IdentityUser
	{
		private readonly List<UserPlant> _userPlants = new List<UserPlant>();

		public IReadOnlyCollection<UserPlant> UserPlants => _userPlants;

		[NotMapped]
		public IReadOnlyCollection<Plant> PlantCollection => _userPlants
			.Select(x => x.Plant)
			.ToList();

		public void AddPlantToCollection(Plant plant)
		{
			if (!PlantCollection.Contains(plant))
			{
				var userPlant = new UserPlant(this, plant);
				_userPlants.Add(userPlant);
			}
		}
	}
}
