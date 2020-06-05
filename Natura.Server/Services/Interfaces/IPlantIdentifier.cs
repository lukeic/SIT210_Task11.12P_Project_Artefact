using System.IO;
using System.Threading.Tasks;

namespace Natura.Server.Services.Interfaces
{
	public interface IPlantIdentifier
	{
		public Task<Output.PlantOutput?> Identify(MemoryStream image);

		public Task<Output.PlantOutput?> Identify(string image);
	}
}
