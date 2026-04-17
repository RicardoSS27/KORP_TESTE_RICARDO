using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

var builder = WebApplication.CreateBuilder(args);

// DB
builder.Services.AddDbContext<FaturamentoDbContext>(opt =>
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

// HTTP + Polly
builder.Services.AddHttpClient("estoque", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:EstoqueBaseUrl"] ?? "http://estoque:8080");
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));

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
            var db = scope.ServiceProvider.GetRequiredService<FaturamentoDbContext>();
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
    
    throw new InvalidOperationException("Nao foi possivel inicializar o banco de dados do faturamento.");
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

public class NotaFiscal
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public string Status { get; set; } = "Aberta";
    public List<ItemNota> Itens { get; set; } = new();
}

public class ItemNota
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
}

public class FaturamentoDbContext : DbContext
{
    public FaturamentoDbContext(DbContextOptions<FaturamentoDbContext> options)
        : base(options) { }

    public DbSet<NotaFiscal> Notas => Set<NotaFiscal>();
}


// ================= CONTROLLER =================

[ApiController]
[Route("api/notas")]
public class NotasController : ControllerBase
{
    private readonly FaturamentoDbContext _context;
    private readonly IHttpClientFactory _factory;

    public NotasController(FaturamentoDbContext context, IHttpClientFactory factory)
    {
        _context = context;
        _factory = factory;
    }

    [HttpPost]
    public async Task<IActionResult> Criar(NotaFiscal nota)
    {
        nota.Status = "Aberta";
        _context.Notas.Add(nota);
        await _context.SaveChangesAsync();
        return Ok(nota);
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var notas = await _context.Notas
            .Include(n => n.Itens)
            .ToListAsync();

        return Ok(notas);
    }

    [HttpPost("{id}/imprimir")]
    public async Task<IActionResult> Imprimir(int id)
    {
        var nota = await _context.Notas
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (nota == null)
            return NotFound();

        if (nota.Status != "Aberta")
            return BadRequest("Nota já fechada");

        var client = _factory.CreateClient("estoque");

        foreach (var item in nota.Itens)
        {
            var response = await client.PostAsJsonAsync("/api/produtos/baixar", new
            {
                produtoId = item.ProdutoId,
                quantidade = item.Quantidade
            });

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var detalhe = await response.Content.ReadAsStringAsync();
                return BadRequest(string.IsNullOrWhiteSpace(detalhe) ? "Nao foi possivel baixar o estoque." : detalhe);
            }

            if (!response.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status502BadGateway, "Erro ao atualizar estoque");
        }

        nota.Status = "Fechada";
        await _context.SaveChangesAsync();

        return Ok("Nota impressa");
    }
}
