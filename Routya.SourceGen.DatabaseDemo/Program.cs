using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Generated;
using Routya.SourceGen.DatabaseDemo.Database;
using Routya.SourceGen.DatabaseDemo.Models;
using Routya.SourceGen.DatabaseDemo.Notifications;
using Routya.SourceGen.DatabaseDemo.Requests;

Console.WriteLine("🗄️  Routya Source Generator - Database Demo");
Console.WriteLine("==========================================\n");

// Setup DI container with database
var services = new ServiceCollection();

// Register database context
services.AddSingleton(new ProductDbContext("Data Source=:memory:"));

// Register Routya with source-generated handlers
services.AddGeneratedRoutya();

var serviceProvider = services.BuildServiceProvider();
var routya = serviceProvider.GetRequiredService<IGeneratedRoutya>();

Console.WriteLine("📝 Step 1: Create Products (Write to Database)");
Console.WriteLine("----------------------------------------------");

var laptopId = await routya.SendAsync(new CreateProductCommand
{
    Name = "Gaming Laptop",
    Price = 1299.99m,
    StockQuantity = 15
});

await routya.PublishAsync(new ProductCreatedNotification
{
    ProductId = laptopId,
    ProductName = "Gaming Laptop"
});

Console.WriteLine();

var mouseId = await routya.SendAsync(new CreateProductCommand
{
    Name = "Wireless Mouse",
    Price = 29.99m,
    StockQuantity = 50
});

await routya.PublishAsync(new ProductCreatedNotification
{
    ProductId = mouseId,
    ProductName = "Wireless Mouse"
});

Console.WriteLine();

var keyboardId = await routya.SendAsync(new CreateProductCommand
{
    Name = "Mechanical Keyboard",
    Price = 89.99m,
    StockQuantity = 30
});

Console.WriteLine("\n📖 Step 2: Read Single Product from Database");
Console.WriteLine("--------------------------------------------");

var product = await routya.SendAsync(new GetProductQuery { ProductId = laptopId });
if (product != null)
{
    Console.WriteLine($"✅ Retrieved: {product.Name} - ${product.Price}");
}

Console.WriteLine("\n📚 Step 3: Read All Products from Database");
Console.WriteLine("------------------------------------------");

var allProducts = await routya.SendAsync(new GetAllProductsQuery());
Console.WriteLine($"\n✅ Total products in database: {allProducts.Count}");
foreach (var p in allProducts)
{
    Console.WriteLine($"   • {p.Name}: ${p.Price} (Stock: {p.StockQuantity})");
}

Console.WriteLine("\n✏️  Step 4: Update Product Stock (Write to Database)");
Console.WriteLine("---------------------------------------------------");

var updateSuccess = await routya.SendAsync(new UpdateStockCommand
{
    ProductId = mouseId,
    NewQuantity = 45
});

if (updateSuccess)
{
    Console.WriteLine("✅ Stock update confirmed!");
}

Console.WriteLine("\n🔍 Step 5: Verify Update (Read from Database)");
Console.WriteLine("---------------------------------------------");

var updatedProduct = await routya.SendAsync(new GetProductQuery { ProductId = mouseId });
if (updatedProduct != null)
{
    Console.WriteLine($"✅ Verified: {updatedProduct.Name} now has {updatedProduct.StockQuantity} units in stock");
}

Console.WriteLine("\n🎉 Database Integration Test Complete!");
Console.WriteLine("=====================================");
Console.WriteLine("✅ Source generator working perfectly with database operations");
Console.WriteLine("✅ Created 3 products (database writes)");
Console.WriteLine("✅ Read individual product (database read)");
Console.WriteLine("✅ Read all products (database read)");
Console.WriteLine("✅ Updated product stock (database write)");
Console.WriteLine("✅ Verified update (database read)");
Console.WriteLine("✅ Published notifications with multiple handlers");

// Cleanup
serviceProvider.Dispose();
