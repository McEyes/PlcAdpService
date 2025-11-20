using jb.smartchangeover.Service.Domain.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace jb.smartchangeover.Service.HttpApi.Controllers.Common
{
    /* Inherit your controllers from this class.
     */
    public abstract class PageBaseController : AbpController
    {
        protected PageBaseController()
        {
            
        }
    }
}