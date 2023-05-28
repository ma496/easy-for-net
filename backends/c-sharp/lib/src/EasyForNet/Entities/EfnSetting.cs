using EasyForNet.Domain.Entities;

namespace EasyForNet.Entities;

public class EfnSetting : Entity<long>
{
    public string Key { get; set; }
    public string Value { get; set; }
}