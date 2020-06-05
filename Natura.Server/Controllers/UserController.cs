using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Natura.Server.Models;
using Natura.Server.Services.Interfaces;

namespace Natura.Server.Controllers
{
	[ApiController]
	[Route("api/user")]
	public class UserController : ControllerBase
	{
		private readonly SignInManager<User> _signInManager;
		private readonly IPlantIdentifier _plantIdentifier;

		public UserController(SignInManager<User> signInManager, IPlantIdentifier plantIdentifier)
		{
			_signInManager = signInManager;
			_plantIdentifier = plantIdentifier;
		}

		[HttpGet("login")]
		public async Task<IActionResult> Login([FromQuery] string? returnUrl)
		{
			if (User.Identity.IsAuthenticated)
			{
				return LocalRedirect("/Collection");
			}

			return Challenge(new AuthenticationProperties { RedirectUri = Url.Page("/Collection") });
		}
	}
}
