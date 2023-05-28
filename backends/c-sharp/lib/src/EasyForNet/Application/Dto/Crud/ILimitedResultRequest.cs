namespace EasyForNet.Application.Dto.Crud;

public interface ILimitedResultRequest
{
    int MaxResultCount { get; set; }
}