using System.Linq;
using System.Threading.Tasks;

namespace EasyForNet.Application.Services.Crud;

public interface ICrudAppService<in TKey, in TListInput, out TListDto, in TCreateInput, in TUpdateInput, TGetDto>
{
    IQueryable<TListDto> List(TListInput input);
    Task<TGetDto> CreateAsync(TCreateInput input);
    Task<TGetDto> UpdateAsync(TKey id, TUpdateInput input);
    Task<TGetDto> GetAsync(TKey id);
    Task DeleteAsync(TKey id);
    Task UndoDeleteAsync(TKey id);
}
