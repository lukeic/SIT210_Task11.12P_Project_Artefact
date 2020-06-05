using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Natura.Server.Infrastructure;

namespace Natura.Server.Pages
{
	public class Index : PageModel
	{
		[ViewData]
		public string Title { get; } = "Plant Collection Portal";

		public string? Email { get; private set; }

		public PageResult OnGet()
		{
			Email = HttpContext.User.GetEmail();
			return Page();
		}
	}
}
