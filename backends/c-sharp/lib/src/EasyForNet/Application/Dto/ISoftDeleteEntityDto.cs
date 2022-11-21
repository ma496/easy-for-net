namespace EasyForNet.Application.Dto;

public interface ISoftDeleteEntityDto
{
    bool IsDeleted { get; set; }
}