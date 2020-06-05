using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Natura.Server.Services.Interfaces;
using Natura.Server.Services.Output;
using Plant.NET;
using Plant.NET.Models;

namespace Natura.Server.Services
{
	public class PlantNetPlantIdentifier : IPlantIdentifier
	{
		private readonly PlantNetClient _plantNetClient;

		public PlantNetPlantIdentifier(PlantNetClient plantNetClient)
		{
			_plantNetClient = plantNetClient;
		}

		public async Task<PlantOutput?> Identify(MemoryStream image)
		{
			var identificationResult = await _plantNetClient.Identify(new PlantImage(image));

			return ProcessResult(identificationResult);
		}

		public async Task<PlantOutput?> Identify(string image)
		{
			var identificationResult = await _plantNetClient.Identify(new RemotePlantImage(image));

			return ProcessResult(identificationResult);
		}

		private static PlantOutput? ProcessResult(IdentificationResult identificationResult)
		{
			var possibleSpecies = identificationResult.Results;
			if (!possibleSpecies.Any())
			{
				return null;
			}

			var topResult = possibleSpecies
				.OrderByDescending(_ => _.Score)
				.First();

			if (topResult?.Species == null)
			{
				return null;
			}

			var identifiedSpecies = topResult.Species;

			return new Output.PlantOutput(
				identifiedSpecies.ScientificNameWithoutAuthor,
				identifiedSpecies.CommonNames,
				topResult.Gbif?.Id);
		}
	}
}
