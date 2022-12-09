namespace EasyForNet.Application.Services.Crud;

public interface ILimitedResultRequest
{
    int MaxResultCount { get; set; }
}