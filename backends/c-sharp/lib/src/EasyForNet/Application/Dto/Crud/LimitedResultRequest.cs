namespace EasyForNet.Application.Dto.Crud;

public class LimitedResultRequest : ILimitedResultRequest
{
    public int MaxResultCount { get; set; }
}