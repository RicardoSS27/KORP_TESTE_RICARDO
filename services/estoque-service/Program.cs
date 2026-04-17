using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;


var builder = WebApplication.CreateBuilder(args);

// DB
builder.Services.AddDbContext<EstoqueDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

EnsureDatabaseCreatedWithRetry(app.Services, app.Logger);

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");

app.MapControllers();

app.Run();

static void EnsureDatabaseCreatedWithRetry(IServiceProvider services, ILogger logger)
{
    const int maxAttempts = 10;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EstoqueDbContext>();
            EnsureDatabaseExists(db.Database.GetConnectionString()!);
            var databaseCreator = db.GetService<IRelationalDatabaseCreator>();
            if (!databaseCreator.HasTables())
            {
                databaseCreator.CreateTables();
            }
            return;
        }
        catch (SqlException ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Banco indisponivel na tentativa {Attempt}. Aguardando para tentar novamente.", attempt);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
    
    throw new InvalidOperationException("Nao foi possivel inicializar o banco de dados do estoque.");
}

static void EnsureDatabaseExists(string connectionString)
{
    var builder = new SqlConnectionStringBuilder(connectionString);
    var databaseName = builder.InitialCatalog;

    if (string.IsNullOrWhiteSpace(databaseName))
        throw new InvalidOperationException("Connection string sem nome de banco.");

    builder.InitialCatalog = "master";

    using var connection = new SqlConnection(builder.ConnectionString);
    connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = $"IF DB_ID(N'{databaseName.Replace("'", "''")}') IS NULL CREATE DATABASE [{databaseName.Replace("]", "]]")}]";
    command.ExecuteNonQuery();
}


// ================= MODELOS =================

public class Produto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public int Saldo { get; set; }
}

public class ItemRequest
{
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
}

public class EstoqueDbContext : DbContext
{
    public EstoqueDbContext(DbContextOptions<EstoqueDbContext> options)
        : base(options) { }

    public DbSet<Produto> Produtos => Set<Produto>();
}


// ================= CONTROLLER =================

[ApiController]
[Route("api/produtos")]
public class ProdutosController : ControllerBase
{
    private readonly EstoqueDbContext _context;

    public ProdutosController(EstoqueDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Criar(Produto produto)
    {
        _context.Produtos.Add(produto);
        await _context.SaveChangesAsync();
        return Ok(produto);
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var lista = await _context.Produtos.ToListAsync();
        return Ok(lista);
    }

    [HttpPost("baixar")]
    public async Task<IActionResult> Baixar([FromBody] ItemRequest item)
    {
        var produto = await _context.Produtos.FindAsync(item.ProdutoId);

        if (produto == null || produto.Saldo < item.Quantidade)
            return BadRequest("Estoque insuficiente");

        produto.Saldo -= item.Quantidade;
        await _context.SaveChangesAsync();

        return Ok();
    }
}
