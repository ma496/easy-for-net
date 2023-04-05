using AutoMapper;
using CSharpTemplate.Common.Identity.Entities;
using EasyForNet.Application.Dto.Entities;

namespace CSharpTemplate.Common.Identity.Dto;

[AutoMap(typeof(User), ReverseMap = true)]
public class UserDto : EntityDto<long>
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
}