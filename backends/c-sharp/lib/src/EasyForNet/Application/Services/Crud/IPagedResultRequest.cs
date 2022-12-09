namespace EasyForNet.Application.Services.Crud;

public interface IPagedResultRequest : ILimitedResultRequest
{
    int SkipCount { get; set; }
}
