namespace EasyForNet.Application.Dto.Crud;

public interface IPagedResultRequest : ILimitedResultRequest
{
    int SkipCount { get; set; }
}
