using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Dal.Abstractions;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Bll.Services;

public sealed class CustomerService(IUnitOfWork uow, IMapper mapper) : ICustomerService
{
    public async Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var customer = await uow.Customers.GetByIdAsync(id, ct)
                       ?? throw new NotFoundException($"Customer {id} not found");
        return mapper.Map<CustomerDto>(customer);
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct = default)
        => mapper.Map<List<CustomerDto>>(await uow.Customers.GetAllAsync(ct));

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto input, CancellationToken ct = default)
    {
        var email = input.Email.Trim();

        // бізнес-валідація зі станом БД: унікальність Email
        if (await uow.Customers.GetByEmailAsync(email, ct) is not null)
            throw new BusinessConflictException($"Customer with email '{email}' already exists");

        var id = await uow.Customers.AddAsync(new Customer { Email = email, FullName = input.FullName.Trim() }, ct);

        var created = await uow.Customers.GetByIdAsync(id, ct)
                      ?? throw new NotFoundException($"Customer {id} not found");
        return mapper.Map<CustomerDto>(created);
    }

    // існування клієнта й відсутність незавершених замовлень перевіряє процедура usp_SoftDeleteCustomer
    public Task DeleteAsync(long id, CancellationToken ct = default)
        => uow.Customers.SoftDeleteAsync(id, ct);
}