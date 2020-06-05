using System.Linq;
using System.Security.Claims;

namespace Natura.Server.Infrastructure
{
	public static class ClaimsPrincipalExtensions
	{
		public static string? GetEmail(this ClaimsPrincipal user)
		{
			if (!user.Identity.IsAuthenticated)
			{
				return null;
			}

			return user.Claims
				.SingleOrDefault(x => x.Type == ClaimTypes.Email)
				.Value;
		}
	}
}
