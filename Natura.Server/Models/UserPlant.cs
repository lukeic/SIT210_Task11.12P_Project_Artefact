namespace Natura.Server.Models
{
	public class UserPlant
	{
		private UserPlant()
		{
		}

		public UserPlant(string userId, string plantId)
		{
			UserId = userId;
			PlantId = plantId;
		}

		public UserPlant(User user, Plant plant)
		{
			User = user;
			Plant = plant;
		}

		public string UserId { get; private set; } = null!;

		public User User { get; private set; } = null!;

		public string PlantId { get; private set; } = null!;

		public Plant Plant { get; private set; } = null!;
	}
}
