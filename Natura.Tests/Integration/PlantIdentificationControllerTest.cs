using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Natura.Tests.Integration
{
	public class PlantIdentificationControllerTest
	{
		[Fact]
		public async Task IdentifyPlant_WhenPlantCanBeIdentified_ThenReturnsListOfPlantsCommonNames()
		{
			using var httpClient = new HttpClient();

			var imagePath = Path.Combine(Environment.CurrentDirectory, "plant.jpg");
			await using var imageFile = File.OpenRead(imagePath);
			using var result = new MemoryStream();
			await imageFile.CopyToAsync(result);
			result.Position = 0;

			await httpClient.PostAsync(
				"https://localhost:5001/api/identify",
				new StreamContent(result));
		}
	}
}
