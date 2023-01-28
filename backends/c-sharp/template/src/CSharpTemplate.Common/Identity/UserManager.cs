using AutoMapper.QueryableExtensions;
using CSharpTemplate.Common.Identity.Dto;
using CSharpTemplate.Common.Identity.Entities;
using EasyForNet.Domain.Services;
using EasyForNet.Exceptions.UserFriendly;
using EasyForNet.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.Common.Identity;

public class UserManager : DomainService, IUserManager
{
    private readonly IRepository<AppUser, long> _userRepository;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public UserManager(IRepository<AppUser, long> userRepository, IPasswordHasher<AppUser> passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }
    
    public async Task<AppUser> CreateAsync(UserDto user)
    {
        if (await _userRepository.GetAll().Where(u => u.Email.ToLower() == user.Email.ToLower()).AnyAsync())
            throw new DuplicateException(nameof(user.Email));
        if (await _userRepository.GetAll().Where(u => u.Username.ToLower() == user.Username.ToLower()).AnyAsync())
            throw new DuplicateException(nameof(user.Username));
        
        var appUser = Mapper.Map<AppUser>(user);
        await _userRepository.CreateAsync(appUser, true);
        return appUser;
    }

    public async Task UpdatePasswordAsync(AppUser user, string password)
    {
        user.HashedPassword = _passwordHasher.HashPassword(user, password);
        await _userRepository.SaveChangesAsync();
    }
    
    public async Task<UserDto?> GetByIdAsync(long id)
    {
        var user = await _userRepository.GetAll()
            .Where(e => e.Id == id)
            .ProjectTo<UserDto>(Mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
        return user;
    }

    public async Task<UserDto?> GetByEmailAsync(string email)
    {
        var user = await _userRepository.GetAll()
            .Where(e => e.Email.ToLower() == email.ToLower())
            .ProjectTo<UserDto>(Mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
        return user;
    }
}