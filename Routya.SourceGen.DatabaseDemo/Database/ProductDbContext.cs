using Microsoft.Data.Sqlite;
using Routya.SourceGen.DatabaseDemo.Models;

namespace Routya.SourceGen.DatabaseDemo.Database;

public class ProductDbContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public ProductDbContext(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price DECIMAL(10, 2) NOT NULL,
                StockQuantity INTEGER NOT NULL
            )";
        command.ExecuteNonQuery();
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        var command = _connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Price, StockQuantity FROM Products WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Price = reader.GetDecimal(2),
                StockQuantity = reader.GetInt32(3)
            };
        }

        return null;
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        var products = new List<Product>();
        var command = _connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Price, StockQuantity FROM Products";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            products.Add(new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Price = reader.GetDecimal(2),
                StockQuantity = reader.GetInt32(3)
            });
        }

        return products;
    }

    public async Task<int> CreateProductAsync(Product product)
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Products (Name, Price, StockQuantity)
            VALUES ($name, $price, $stock);
            SELECT last_insert_rowid();";
        
        command.Parameters.AddWithValue("$name", product.Name);
        command.Parameters.AddWithValue("$price", product.Price);
        command.Parameters.AddWithValue("$stock", product.StockQuantity);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<bool> UpdateProductStockAsync(int productId, int newQuantity)
    {
        var command = _connection.CreateCommand();
        command.CommandText = "UPDATE Products SET StockQuantity = $quantity WHERE Id = $id";
        command.Parameters.AddWithValue("$quantity", newQuantity);
        command.Parameters.AddWithValue("$id", productId);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
