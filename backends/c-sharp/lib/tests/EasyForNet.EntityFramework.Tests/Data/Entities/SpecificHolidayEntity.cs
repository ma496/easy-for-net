using System;
using EasyForNet.Domain.Entities;

namespace EasyForNet.EntityFramework.Tests.Data.Entities;

public class SpecificHolidayEntity : Entity<long>
{
    public DateTime Date { get; set; }
}