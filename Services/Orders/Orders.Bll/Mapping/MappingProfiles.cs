using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Domain.Models;

namespace Orders.Bll.Mapping;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<Order, OrderDto>();
    }
}

public class CustomerProfile : Profile
{
    public CustomerProfile() => CreateMap<Customer, CustomerDto>();
}

public class ProductProfile : Profile
{
    public ProductProfile() => CreateMap<Product, ProductDto>();
}