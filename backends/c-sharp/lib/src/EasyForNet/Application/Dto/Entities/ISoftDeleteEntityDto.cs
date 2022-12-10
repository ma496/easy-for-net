namespace EasyForNet.Application.Dto.Entities;

public interface ISoftDeleteEntityDto
{
    bool IsDeleted { get; set; }
}