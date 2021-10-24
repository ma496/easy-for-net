using System;
using System.Linq;
using System.Threading.Tasks;

namespace EasyForNet.Crud
{
    public interface ICrudActions<TKey, TListDto, TCreateDto, TUpdateDto, TGetDto>
        where TKey : IComparable
        where TListDto : class
        where TCreateDto : class
        where TUpdateDto : class
        where TGetDto : class
    {
        Task<IQueryable<TListDto>> ListAsync();
        Task<TCreateDto> CreateAsync(TCreateDto dto);
        Task<TUpdateDto> UpdateAsync(TKey id, TUpdateDto dto);
        Task DeleteAsync(TKey id);
        Task UndoDeleteAsync(TKey id);
        Task<TGetDto> GetAsync(TKey id);
    }
}