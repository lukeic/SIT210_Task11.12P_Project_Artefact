namespace Natura.Server.Models
{
	public class PlantName : Entity
	{
		private PlantName()
		{
		}

		public PlantName(Plant plant, string name)
		{
			Plant = plant;
			Name = name;
		}

		public string PlantId { get; private set; } = null!;

		public Plant Plant { get; private set; } = null!;

		public string Name { get; private set; } = null!;
	}
}
