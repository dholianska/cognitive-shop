using System.ComponentModel.DataAnnotations;

namespace Orders.Bll.Dtos;

public record CustomerDto(long Id, string Email, string FullName);

public class CreateCustomerDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(200, MinimumLength = 1)]
    public string FullName { get; set; } = "";
}