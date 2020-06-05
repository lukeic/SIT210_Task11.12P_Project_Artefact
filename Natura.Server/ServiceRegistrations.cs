using System;
using Microsoft.Extensions.DependencyInjection;
using Natura.Server.Services;
using Natura.Server.Services.Interfaces;
using Polly;
using Polly.Extensions.Http;

namespace Natura.Server
{
	public static class ServiceRegistrations
	{
		public static void AddNaturaServices(this IServiceCollection services)
		{
			services.AddSingleton<IPlantIdentifier, PlantNetPlantIdentifier>();

			services
				.AddHttpClient<GbifClient>(
					client => { client.BaseAddress = new Uri("https://api.gbif.org/v1/"); })
				.AddPolicyHandler(
					_ => HttpPolicyExtensions
						.HandleTransientHttpError()
						.WaitAndRetryAsync(
							3,
							retryAttempt => TimeSpan.FromSeconds(Math.Pow(2.0, retryAttempt))));
		}
	}
}
