using System.Collections.Generic;

namespace Natura.Server.Services.Output
{
	public class PlantOutput
	{
		public PlantOutput(
			string scentificName,
			IReadOnlyCollection<string> commonNames,
			string? gbifId = null)
		{
			ScentificName = scentificName;
			CommonNames = commonNames;
			GbifId = gbifId;
		}

		public string ScentificName { get; }

		public IReadOnlyCollection<string> CommonNames { get; }

		public string? GbifId { get; }
	}
}
