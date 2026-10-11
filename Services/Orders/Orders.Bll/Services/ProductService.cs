using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Dal.Abstractions;
using Orders.Domain.Exceptions;

namespace Orders.Bll.Services;

/// <summary>Лише читання: власник товарів - сервіс Каталогу, тут локальна копія довідника.</summary>
public sealed class ProductService(IUnitOfWork uow, IMapper mapper) : IProductService
{
    public async Task<ProductDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var product = await uow.Products.GetByIdAsync(id, ct)
                      ?? throw new NotFoundException($"Product {id} not found");
        return mapper.Map<ProductDto>(product);
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default)
        => mapper.Map<List<ProductDto>>(await uow.Products.GetAllAsync(ct));
}