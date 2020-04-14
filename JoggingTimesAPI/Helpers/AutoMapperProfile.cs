using AutoMapper;
using JoggingTimesAPI.Entities;
using JoggingTimesAPI.Models;

namespace JoggingTimesAPI.Helpers
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<UserRegisterModel, User>();
            CreateMap<UserUpdateModel, User>();
        }
    }
}
