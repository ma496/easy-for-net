using System.Collections.Generic;
using System.Linq;

namespace EasyForNet.EntityFramework.Tests.Crud.CustomerEntityCrudTests
{
    public static class CustomerEntityCrudTestsHelper
    {
        public static CustomerDto NewCustomer()
        {
            return new()
            {
                Code = IncrementalId.Id,
                IdCard = $"34344-98433-0903{IncrementalId.Id}",
                CellNo = "0345098567",
                Name = "Muhammad Ali"
            };
        }

        public static List<CustomerDto> NewCustomers(int count)
        {
            return Enumerable.Range(0, count)
                .Select(_ => NewCustomer())
                .ToList();
        }
    }
}