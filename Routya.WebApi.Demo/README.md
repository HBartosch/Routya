# Routya Web API Demo

This project demonstrates the **Routya CQRS library** in a production-like ASP.NET Core Web API with SQL Server database integration.

## 🎯 What This Demo Showcases

- **All Three Handler Lifetimes**: Singleton, Scoped, and Transient handlers working with a real database
- **Entity Framework Core**: Full CRUD operations on SQL Server
- **RESTful API**: Complete Product management endpoints
- **Performance**: Real-world usage of the optimized Routya dispatcher

## 📊 Handler Lifetime Breakdown

| Handler | Lifetime | Use Case | Endpoint |
|---------|----------|----------|----------|
| `CreateProductHandler` | **Singleton** | Fastest, shared instance. Uses IServiceProvider to scope DbContext | `POST /api/products` |
| `UpdateProductStockHandler` | **Singleton** | Same pattern as Create | `PUT /api/products/{id}/stock` |
| `GetProductHandler` | **Scoped** | One instance per HTTP request (most common pattern) | `GET /api/products/{id}` |
| `DeleteProductHandler` | **Scoped** | Shares scope with other operations in request | `DELETE /api/products/{id}` |
| `GetAllProductsHandler` | **Transient** | New instance every time (most isolated) | `GET /api/products` |

## 🚀 Running the Demo

```powershell
# From the Routya.WebApi.Demo directory
dotnet run
```

The API will start on: `http://localhost:5079`

## 📡 API Endpoints

### Create a Product (Singleton Handler)
```bash
POST http://localhost:5079/api/products
Content-Type: application/json

{
  "name": "Laptop",
  "price": 999.99,
  "stock": 50
}
```

### Get All Products (Transient Handler)
```bash
GET http://localhost:5079/api/products
```

### Get Product by ID (Scoped Handler)
```bash
GET http://localhost:5079/api/products/1
```

### Update Product Stock (Singleton Handler)
```bash
PUT http://localhost:5079/api/products/1/stock
Content-Type: application/json

25
```

### Delete Product (Scoped Handler)
```bash
DELETE http://localhost:5079/api/products/1
```

## 🧪 Testing with PowerShell

```powershell
# Create a product
$body = @{
    name = "Laptop"
    price = 999.99
    stock = 50
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5079/api/products" -Method Post -Body $body -ContentType "application/json"

# Get all products
Invoke-RestMethod -Uri "http://localhost:5079/api/products" -Method Get

# Get product by ID
Invoke-RestMethod -Uri "http://localhost:5079/api/products/1" -Method Get

# Update stock
Invoke-RestMethod -Uri "http://localhost:5079/api/products/1/stock" -Method Put -Body "25" -ContentType "application/json"

# Delete product
Invoke-RestMethod -Uri "http://localhost:5079/api/products/1" -Method Delete
```

## 🗄️ Database

- **Provider**: SQL Server (LocalDB)
- **Database**: RoutyaDemo
- **Connection String**: Configured in `appsettings.json`
- **Schema**: Automatically created on first run via `EnsureCreatedAsync()`

## ⚡ Performance Notes

### Singleton Handlers
- **Fastest**: Single shared instance across all requests
- **Pattern**: Use `IServiceProvider` to manually create scopes for DbContext
- **Use When**: Handler is stateless and doesn't need request-scoped services

### Scoped Handlers
- **Most Common**: One instance per HTTP request
- **Pattern**: Inject DbContext directly into constructor
- **Use When**: Handler needs request-scoped services (like DbContext)

### Transient Handlers
- **Most Isolated**: New instance created every time
- **Pattern**: Same as Scoped, but new instance per resolution
- **Use When**: Handler maintains state that shouldn't be shared

## 🏗️ Project Structure

```
Routya.WebApi.Demo/
├── Controllers/
│   └── ProductsController.cs      # RESTful API endpoints
├── Data/
│   └── AppDbContext.cs            # Entity Framework DbContext
├── Handlers/
│   └── ProductHandlers.cs         # CQRS handlers (all lifetimes)
├── Models/
│   └── Product.cs                 # Domain entity
├── Requests/
│   └── ProductRequests.cs         # CQRS request definitions
├── Program.cs                     # Service configuration
└── appsettings.json               # Configuration & connection string
```

## 📦 Dependencies

- **Routya.Core** (local project reference)
- **Microsoft.EntityFrameworkCore.SqlServer** 10.0.0
- **Microsoft.EntityFrameworkCore.Design** 10.0.0

## 🎓 Key Learnings

1. **Manual Handler Registration**: When you need mixed lifetimes, register handlers manually and call `AddRoutya()` without assembly scanning
2. **Singleton + DbContext**: Use `IServiceProvider` to create scopes manually for database access
3. **Performance**: Singleton handlers show the best performance (~174ns), but require careful service resolution
4. **Production Ready**: This pattern works in real-world scenarios with databases and complex operations
