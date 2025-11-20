using System;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Uow;

namespace jb.smartchangeover.Service.Domain
{
    public abstract class SmartChangeOverDomainService : DomainService
    {
        protected Type? ObjectMapperContext { get; set; }

        protected IUnitOfWorkManager UnitOfWorkManager =>
            LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>();

        protected IDistributedEventBus DistributedEventBus =>
            LazyServiceProvider.LazyGetRequiredService<IDistributedEventBus>();

        protected IObjectMapper ObjectMapper => LazyServiceProvider.LazyGetService<IObjectMapper>(
            provider =>
                ObjectMapperContext == null
                    ? provider.GetRequiredService<IObjectMapper>()
                    : (IObjectMapper)provider.GetRequiredService(
                        typeof(IObjectMapper<>).MakeGenericType(ObjectMapperContext)));
    }
}