using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Natura.Server.Services.Output;

namespace Natura.Server.Services
{
	public class GbifClient
	{
		public class Vernacular
		{
			[JsonPropertyName("vernacularName")]
			public string Name { get; set; } = null!;

			public string Language { get; set; } = null!;
		}

		public class Species
		{
			public string? VernacularName { get; set; }
		}

		public class Occurrence
		{
			[JsonPropertyName("gbifID")]
			public string GbifId { get; set; } = null!;

			public List<GbifImage> Media { get; set; } = new List<GbifImage>();

			public string ScientificName { get; set; } = null!;

			public string AcceptedScientificName { get; set; } = null!;

			public string Kingdom { get; set; } = null!;

			public string Phylum { get; set; } = null!;

			public string Order { get; set; } = null!;

			public string Family { get; set; } = null!;

			public string Genus { get; set; } = null!;

			public string Species { get; set; } = null!;

			public int SpeciesKey { get; set; }

			public string GenericName { get; set; } = null!;

			public string SpecificEpithet { get; set; } = null!;
		}

		public class GbifQueryResult<T>
		{
			public List<T> Results { get; set; } = new List<T>();
		}

		private readonly HttpClient _httpClient;
		private readonly ILogger<GbifClient> _logger;
		private readonly IDistributedCache _cache;

		public GbifClient(HttpClient httpClient, ILogger<GbifClient> logger, IDistributedCache cache)
		{
			_httpClient = httpClient;
			_logger = logger;
			_cache = cache;
		}

		public async Task<List<GbifImage>> FindImagesForSpecies(string speciesKey)
		{
			List<GbifImage> result;

			var cachedResult = await _cache.GetStringAsync(speciesKey);
			if (!string.IsNullOrEmpty(cachedResult))
			{
				result = JsonSerializer.Deserialize<List<GbifImage>>(cachedResult);
				if (result.Any())
				{
					return result;
				}
			}

			var query = new QueryBuilder();
			query.Add("speciesKey", speciesKey);
			var httpResponse = await _httpClient.GetAsync($"occurrence/search{query}");
			try
			{
				httpResponse.EnsureSuccessStatusCode();
			}
			catch (HttpRequestException e)
			{
				_logger.LogError(await httpResponse.Content.ReadAsStringAsync());
				throw;
			}

			var contentStream = await httpResponse.Content.ReadAsStreamAsync();
			var response = await JsonSerializer.DeserializeAsync<GbifQueryResult<Occurrence>>(
				contentStream,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			result = response.Results
				.SelectMany(x => x.Media)
				.Where(x => x != null)
				.ToList();

			await _cache.SetStringAsync(
				speciesKey,
				JsonSerializer.Serialize(result),
				new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(6) });

			return result;
		}

		public async Task<List<GbifImage>> FindImagesForSpecies(IEnumerable<string> speciesKeys)
		{
			var result = new ConcurrentBag<GbifImage>();

			foreach (var speciesKey in speciesKeys.AsParallel())
			{
				var query = new QueryBuilder();
				query.Add("speciesKey", speciesKey);
				var httpResponse = await _httpClient.GetAsync($"occurrence/search{query}");
				httpResponse.EnsureSuccessStatusCode();

				var contentStream = await httpResponse.Content.ReadAsStreamAsync();
				var response = await JsonSerializer.DeserializeAsync<GbifQueryResult<Occurrence>>(
					contentStream,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

				var images = response.Results
					.SelectMany(x => x.Media)
					.ToList();

				foreach (var image in images)
				{
					result.Add(image);
				}
			}

			return result.ToList();
		}

		public async Task<List<Occurrence>> GetPlantSeedData()
		{
			var query = new QueryBuilder();
			var kingdomKeyPlantae = 6;
			query.Add("country", "AU");
			query.Add("mediaType", "StillImage");
			query.Add("kingdomKey", kingdomKeyPlantae.ToString());
			var httpResponse = await _httpClient.GetAsync($"occurrence/search{query}");
			httpResponse.EnsureSuccessStatusCode();

			var contentStream = await httpResponse.Content.ReadAsStreamAsync();

			var response = await JsonSerializer.DeserializeAsync<GbifQueryResult<Occurrence>>(
				contentStream,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			return response.Results
				.GroupBy(x => x.Species)
				.Select(x => x.First())
				.ToList();
		}

		public async Task<List<string>> FindSpeciesVernacularNames(int speciesKey)
		{
			var query = new QueryBuilder();
			query.Add("language", "en");
			query.Add("limit", "3");
			var httpResponse = await _httpClient.GetAsync($"species/{speciesKey}/vernacularNames{query}");
			httpResponse.EnsureSuccessStatusCode();

			var contentStream = await httpResponse.Content.ReadAsStreamAsync();
			var response = await JsonSerializer.DeserializeAsync<GbifQueryResult<Vernacular>>(
				contentStream,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			return response.Results
				.Where(x => x.Language == "eng")
				.Select(x => x.Name.Trim())
				.Distinct()
				.ToList();
		}
	}
}
