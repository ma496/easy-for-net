using System;

namespace EasyForNet.Domain.Entities.Audit
{
    public interface ICreateAuditEntity
    {
        DateTime CreatedAt { get; set; }

        string CreatedBy { get; set; }
    }
}
