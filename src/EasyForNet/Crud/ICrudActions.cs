using System;
using System.Linq;
using System.Threading.Tasks;
using EasyForNet.Application.Dto;

namespace EasyForNet.Crud
{
    public interface ICrudActions<in TKey, TListDto, in TCreateDto, TCreateResponseDto, TUpdateDto, TUpdateResponseDto, TGetDto>
        where TKey : IComparable
        where TListDto : class, IEntityDto<TKey>
        where TCreateDto : class
        where TCreateResponseDto : class, IEntityDto<TKey>, TCreateDto 
        where TUpdateDto : class
        where TUpdateResponseDto : class, IEntityDto<TKey>, TUpdateDto
        where TGetDto : class, IEntityDto<TKey>
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