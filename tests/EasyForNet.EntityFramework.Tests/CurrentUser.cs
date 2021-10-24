using EasyForNet.Application.Dependencies;

namespace EasyForNet.EntityFramework.Tests
{
    public class CurrentUser : ICurrentUser, IScopedDependency
    {
        public long UserId => 453453;

        public string Username => "unreal-001";
    }
}