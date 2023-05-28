namespace EasyForNet.Application.Dto.Crud;

public interface IPagedResultDto<T> : IListResultDto<T>, IHasTotalCount
{
}
