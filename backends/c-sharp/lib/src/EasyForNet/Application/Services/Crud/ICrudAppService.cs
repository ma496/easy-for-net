using EasyForNet.Application.Dto.Crud;
using System.Linq;
using System.Threading.Tasks;

namespace EasyForNet.Application.Services.Crud;

public interface ICrudAppService<in TKey, in TGetListInput, TGetListDto, in TCreateInput, in TUpdateInput, TGetDto>
{
    Task<PagedResultDto<TGetListDto>> GetListAsync(TGetListInput input);
    Task<TGetDto> CreateAsync(TCreateInput input);
    Task<TGetDto> UpdateAsync(TKey id, TUpdateInput input);
    Task<TGetDto> GetAsync(TKey id);
    Task DeleteAsync(TKey id);
    Task UndoDeleteAsync(TKey id);
}
