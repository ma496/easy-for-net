using EasyForNet.Application.Dependencies;
using EasyForNet.Common;

namespace EasyForNet.Domain.Services;

public abstract class DomainService : CommonThings, IDomainService, ITransientDependency
{
}