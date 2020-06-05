using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Natura.Server.Models
{
	public class Plant : Entity
	{
		private readonly List<PlantName> _plantNames = new List<PlantName>();
		private readonly List<UserPlant> _userPlants = new List<UserPlant>();

		private Plant(string? gbifSpeciesKey)
		{
			GbifSpeciesKey = gbifSpeciesKey;
		}

		public Plant(string scientificName, IEnumerable<string> commonNames, string? gbifSpeciesKey = null)
		{
			ScientificName = scientificName;
			GbifSpeciesKey = gbifSpeciesKey;
			_plantNames = commonNames
				.Select(x => new PlantName(this, x))
				.ToList();
		}

		public string ScientificName { get; private set; } = null!;

		public string? GbifSpeciesKey { get; private set; }

		public IReadOnlyCollection<UserPlant> UserPlants => _userPlants;

		public IReadOnlyCollection<PlantName> PlantNames => _plantNames;

		[NotMapped]
		public IReadOnlyCollection<string> CommonNames => _plantNames
			.Select(x => x.Name)
			.ToList();

		public void AddMissingCommonNames(IList<string> potentiallyMissingCommonNames)
		{
			if (potentiallyMissingCommonNames.Any())
			{
				var missingNames = potentiallyMissingCommonNames
					.Distinct()
					.Except(CommonNames)
					.Select(x => new PlantName(this, x))
					.ToList();

				_plantNames.AddRange(missingNames);
			}
		}

		protected bool Equals(Plant other)
		{
			return ScientificName == other.ScientificName;
		}

		public override bool Equals(object? obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;

			return Equals((Plant) obj);
		}

		public override int GetHashCode()
		{
			return ScientificName.GetHashCode();
		}
	}
}
