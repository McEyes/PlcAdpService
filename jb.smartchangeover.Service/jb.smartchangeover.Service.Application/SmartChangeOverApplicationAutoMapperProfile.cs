using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Application
{
    public class SmartChangeOverApplicationAutoMapperProfile : Profile
    {
        public SmartChangeOverApplicationAutoMapperProfile()
        {
            //CreateMap<Domain.Aggregates.Test, TestOutput>()
            //   .ForMember(d => d.StartTime,
            //        o => o.MapFrom(p =>
            //            p.StartTime.ToString("yyyy-MM-dd HH:mm:ss")));
        }
    }
}
