using AutoMapper;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;

namespace Reviews.Application.Common.Mappings;

public class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<Review, ReviewDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))                 // ObjectId -> string
            .ForMember(d => d.AuthorName, o => o.MapFrom(s => s.Author.DisplayName))    // Value Object
            .ForMember(d => d.Rating, o => o.MapFrom(s => s.Rating.Value))              // Value Object
            .ForMember(d => d.CommentsCount, o => o.MapFrom(s => s.Comments.Count));    // вкладений масив
    }
}