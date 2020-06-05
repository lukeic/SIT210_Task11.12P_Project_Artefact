using System.Text.Json.Serialization;

namespace Natura.Server.Services.Output
{
	public class GbifImage
	{
		public string Type { get; set; } = null!;

		[JsonPropertyName("Identifier")]
		public string Url { get; set; } = null!;

		public string Creator { get; set; } = null!;

		public string Publisher { get; set; } = null!;
	}
}
