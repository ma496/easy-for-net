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
    private readonly IRepository<User, long> _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserManager(IRepository<User, long> userRepository, IPasswordHasher<User> passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }
    
    public async Task<User> CreateAsync(UserDto user)
    {
        if (await _userRepository.GetAll().Where(u => u.Email.ToLower() == user.Email.ToLower()).AnyAsync())
            throw new DuplicateException(nameof(user.Email));
        if (await _userRepository.GetAll().Where(u => u.Username.ToLower() == user.Username.ToLower()).AnyAsync())
            throw new DuplicateException(nameof(user.Username));
        
        var appUser = Mapper.Map<User>(user);
        await _userRepository.CreateAsync(appUser, true);
        return appUser;
    }

    public async Task UpdatePasswordAsync(User user, string password)
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

    public async Task<List<UserDto>> GetListAsync()
    {
        return await _userRepository.GetAll()
            .ProjectTo<UserDto>(Mapper.ConfigurationProvider)
            .ToListAsync();
    }
}