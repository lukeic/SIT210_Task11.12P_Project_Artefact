using System;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Natura.Server.Data;
using Natura.Server.Models;
using Plant.NET.Infrastructure;

namespace Natura.Server
{
	public class Startup
	{
		public Startup(IConfiguration configuration, IWebHostEnvironment environment)
		{
			Configuration = configuration;
			Environment = environment;
		}

		public IConfiguration Configuration { get; }

		public IWebHostEnvironment Environment { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			if (Environment.IsDevelopment())
			{
				services.AddDbContext<ApplicationDbContext>(
					options => { options.UseSqlite(Configuration.GetConnectionString("Sqlite")); });
			}
			else
			{
				services.AddDbContext<ApplicationDbContext>(
					options => { options.UseSqlServer(Configuration.GetConnectionString("SqlServer")); });
			}

			services
				.AddIdentityCore<User>(
					options => { options.ClaimsIdentity.UserNameClaimType = ClaimTypes.Email; })
				.AddSignInManager()
				.AddEntityFrameworkStores<ApplicationDbContext>();

			services
				.AddAuthentication(
					options =>
					{
						options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
						options.DefaultChallengeScheme = MicrosoftAccountDefaults.AuthenticationScheme;
					})
				.AddCookie(
					options =>
					{
						options.Cookie.Name = "natura.user";
						options.Cookie.IsEssential = true;
						options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
					})
				.AddMicrosoftAccount(
					options =>
					{
						options.ClientId = Configuration["Authentication:Microsoft:ClientId"];
						options.ClientSecret = Configuration["Authentication:Microsoft:ClientSecret"];
					});

			services.AddControllers();

			services.AddRazorPages(
				options => { options.Conventions.AuthorizePage("/Collection"); });

			services.AddHsts(options => options.MaxAge = TimeSpan.FromMinutes(5));

			services.AddPlantNetClient(
				options => { options.ApiKey = Configuration["PlantNet:ApiKey"]; });

			services.AddDistributedMemoryCache();

			services.AddNaturaServices();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseExceptionHandler("/Error");

				app.UseHsts();
			}

			app.UseStaticFiles();

			app.UseRouting();

			app.UseAuthentication();

			app.UseAuthorization();

			app.UseEndpoints(
				endpoints =>
				{
					endpoints.MapRazorPages();
					endpoints.MapControllers();
				});
		}
	}
}
