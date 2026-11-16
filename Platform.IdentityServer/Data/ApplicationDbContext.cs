using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Platform.IdentityServer.Models;

namespace Platform.IdentityServer.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
}