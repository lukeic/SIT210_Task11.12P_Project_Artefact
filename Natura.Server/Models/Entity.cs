using System.ComponentModel.DataAnnotations.Schema;

namespace Natura.Server.Models
{
	public abstract class Entity
	{
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public string Id { get; private set; } = null!;
	}
}
