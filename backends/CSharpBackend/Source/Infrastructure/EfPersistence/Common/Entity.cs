using System.ComponentModel.DataAnnotations.Schema;

namespace Efn.Infrastructure.EfPersistence.Common;

public abstract class Entity
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
}
