using System;

namespace EasyForNet.Domain.Entities.Audit
{
    public interface IUpdateAuditEntity
    {
        DateTime UpdatedAt { get; set; }

        string UpdatedBy { get; set; }
    }
}
