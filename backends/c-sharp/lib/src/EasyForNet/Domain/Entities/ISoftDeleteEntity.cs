namespace EasyForNet.Domain.Entities;

public interface ISoftDeleteEntity
{
    bool IsDeleted { get; set; }
}