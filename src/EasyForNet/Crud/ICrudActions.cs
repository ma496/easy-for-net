using System;
using System.Linq;
using System.Threading.Tasks;
using EasyForNet.Application.Dto;

namespace EasyForNet.Crud
{
    public interface ICrudActions<in TKey, TListDto, in TCreateDto, TCreateResponseDto, TUpdateDto, TUpdateResponseDto, TGetDto>
        where TKey : IComparable
        where TListDto : class, IDto<TKey>
        where TCreateDto : class
        where TCreateResponseDto : class, IDto<TKey>, TCreateDto 
        where TUpdateDto : class
        where TUpdateResponseDto : class, IDto<TKey>, TUpdateDto
        where TGetDto : class, IDto<TKey>
    {
        Task<IQueryable<TListDto>> ListAsync();
        Task<TCreateResponseDto> CreateAsync(TCreateDto dto);
        Task<TUpdateResponseDto> UpdateAsync(TKey id, TUpdateDto dto);
        Task<TUpdateDto> ForUpdateAsync(TKey id);
        Task DeleteAsync(TKey id);
        Task UndoDeleteAsync(TKey id);
        Task<TGetDto> GetAsync(TKey id);
    }
}