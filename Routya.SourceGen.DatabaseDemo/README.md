# Routya Source Generator - Database Integration Demo

This demo shows **Routya v3.1 source generator** working with real database operations using SQLite.

## What This Demonstrates

✅ **Database Reads** - Query handlers fetching data from SQLite  
✅ **Database Writes** - Command handlers creating and updating records  
✅ **Source-Generated Dispatcher** - Zero-overhead type-specific dispatch  
✅ **DI Integration** - Handlers with injected database context  
✅ **Notifications** - Multiple handlers processing the same notification  
✅ **Generic Type Support** - Handlers returning `List<Product>` work correctly

## How It Works

### Source Generator Auto-Discovers:
- **4 Request Handlers** (Create, Read, ReadAll, Update)
- **2 Notification Handlers** (Audit logging, Email notifications)

### Generated Code Provides:
- `AddGeneratedRoutya()` - Registers all handlers automatically
- `GeneratedRoutya` - Type-specific dispatch methods with zero dictionary lookups
- Full DI integration - Database context injected into handlers

## Running the Demo

```powershell
dotnet run -c Release
```

## Sample Output

```
🗄️  Routya Source Generator - Database Demo
==========================================

📝 Step 1: Create Products (Write to Database)
----------------------------------------------
  → Creating product: Gaming Laptop...
  ✓ Product created with ID: 1
  → [AUDIT LOG] Product created: ID=1, Name=Gaming Laptop
  → [EMAIL] Sending notification about new product: Gaming Laptop

📖 Step 2: Read Single Product from Database
--------------------------------------------
  → Fetching product with ID 1 from database...
  ✓ Found: Gaming Laptop - $1299.99 (Stock: 15)
✅ Retrieved: Gaming Laptop - $1299.99

📚 Step 3: Read All Products from Database
------------------------------------------
  → Fetching all products from database...
  ✓ Found 3 products

✅ Total products in database: 3
   • Gaming Laptop: $1299.99 (Stock: 15)
   • Wireless Mouse: $29.99 (Stock: 50)
   • Mechanical Keyboard: $89.99 (Stock: 30)

✏️  Step 4: Update Product Stock (Write to Database)
---------------------------------------------------
  → Updating stock for product ID 2 to 45...
  ✓ Stock updated successfully

🔍 Step 5: Verify Update (Read from Database)
---------------------------------------------
  → Fetching product with ID 2 from database...
  ✓ Found: Wireless Mouse - $29.99 (Stock: 45)
✅ Verified: Wireless Mouse now has 45 units in stock
```

## Project Structure

```
Routya.SourceGen.DatabaseDemo/
├── Database/
│   └── ProductDbContext.cs          # SQLite database operations
├── Models/
│   └── Product.cs                   # Product entity
├── Requests/
│   ├── CreateProductCommand.cs      # Write operation
│   ├── GetProductQuery.cs           # Single read operation
│   ├── GetAllProductsQuery.cs       # List read operation
│   └── UpdateStockCommand.cs        # Update operation
├── Handlers/
│   ├── CreateProductCommandHandler.cs
│   ├── GetProductQueryHandler.cs
│   ├── GetAllProductsQueryHandler.cs
│   ├── UpdateStockCommandHandler.cs
│   ├── LogProductCreationHandler.cs      # Notification
│   └── SendProductNotificationHandler.cs # Notification
├── Notifications/
│   └── ProductCreatedNotification.cs
└── Program.cs                       # Demo application

Generated Code (in obj/GeneratedFiles/):
├── RoutyaGenerated.Registration.g.cs  # DI registration
└── RoutyaGenerated.Dispatcher.g.cs    # Type-specific dispatcher
```

## Key Learnings

### ✅ Generic Types Work
The source generator correctly handles complex return types like `List<Product>`:
```csharp
public class GetAllProductsQuery : IRequest<List<Product>> { }
```

Generates:
```csharp
public async Task<System.Collections.Generic.List<Routya.SourceGen.DatabaseDemo.Models.Product>> SendAsync(
    Routya.SourceGen.DatabaseDemo.Requests.GetAllProductsQuery request,
    CancellationToken cancellationToken = default)
```

### ✅ Real Database Integration
Handlers receive injected dependencies via DI:
```csharp
public class GetProductQueryHandler : IAsyncRequestHandler<GetProductQuery, Product>
{
    private readonly ProductDbContext _dbContext;
    
    public GetProductQueryHandler(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<Product> HandleAsync(GetProductQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.GetProductByIdAsync(request.ProductId);
    }
}
```

### ✅ Zero Configuration
Just one line registers everything:
```csharp
services.AddGeneratedRoutya();
```

## Comparison to Manual Registration

### Without Source Generator:
```csharp
services.AddTransient<CreateProductCommandHandler>();
services.AddTransient<IAsyncRequestHandler<CreateProductCommand, int>>(sp => 
    sp.GetRequiredService<CreateProductCommandHandler>());
    
services.AddTransient<GetProductQueryHandler>();
services.AddTransient<IAsyncRequestHandler<GetProductQuery, Product>>(sp => 
    sp.GetRequiredService<GetProductQueryHandler>());
    
// ... repeat for all 6 handlers ...

services.AddTransient<IRoutya, DefaultRoutya>(); // Generic runtime dispatcher
```

### With Source Generator:
```csharp
services.AddGeneratedRoutya(); // ✨ Done!
```

## Performance Benefits

- **Zero dictionary lookups** - Direct handler resolution
- **Compile-time optimization** - Type-specific dispatch methods
- **No reflection** - All types known at compile time
- **Minimal allocations** - Optimized code generation

---

**Perfect for:** CQRS patterns, database operations, event-driven architectures with async database calls
