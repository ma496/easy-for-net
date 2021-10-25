namespace EasyForNet.EntityFramework.Tests
{
    public class CurrentUser : ICurrentUser
    {
        public long UserId => 453453;

        public string Username => "unreal-001";
    }
}