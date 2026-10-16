using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Mapping;

public class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Product, ProductDto>();
        CreateMap<ProductImage, ProductImageDto>();

        CreateMap<Product, ProductCardDto>()
            .ForMember(d => d.Description, o => o.MapFrom(s => s.Detail != null ? s.Detail.Description : null))
            .ForMember(d => d.Manufacturer, o => o.MapFrom(s => s.Detail != null ? s.Detail.Manufacturer : null))
            .ForMember(d => d.Categories, o => o.MapFrom(s => s.ProductCategories.Select(pc => pc.Category.Name)));
    }
}

public class CategoryProfile : Profile
{
    public CategoryProfile()
    {
        CreateMap<Category, CategoryDto>();

        CreateMap<Category, CategoryWithProductsDto>()
            .ForMember(d => d.Products, o => o.MapFrom(s => s.ProductCategories.Select(pc => pc.Product)));
    }
}