# Test script for Routya Web API Demo
# Demonstrates all three handler lifetimes: Singleton, Scoped, and Transient

$baseUrl = "http://localhost:5079/api/products"

Write-Host "`n=== Testing Routya Web API Demo ===" -ForegroundColor Cyan
Write-Host "This demo showcases three different handler lifetimes:`n" -ForegroundColor Yellow
Write-Host "  - SINGLETON: CreateProductHandler & UpdateProductStockHandler (fastest)" -ForegroundColor Green
Write-Host "  - SCOPED: GetProductHandler & DeleteProductHandler (one per request)" -ForegroundColor Blue
Write-Host "  - TRANSIENT: GetAllProductsHandler (new instance every time)`n" -ForegroundColor Magenta

# Test 1: Create products (SINGLETON handler)
Write-Host "`n[1] Creating products with SINGLETON handler..." -ForegroundColor Green
$product1 = @{
    name = "Laptop"
    price = 999.99
    stock = 10
} | ConvertTo-Json

$product2 = @{
    name = "Mouse"
    price = 29.99
    stock = 50
} | ConvertTo-Json

$product3 = @{
    name = "Keyboard"
    price = 79.99
    stock = 30
} | ConvertTo-Json

$response1 = Invoke-RestMethod -Uri $baseUrl -Method Post -Body $product1 -ContentType "application/json"
Write-Host "Created: $($response1.name) - `$$($response1.price) (ID: $($response1.id))" -ForegroundColor White

$response2 = Invoke-RestMethod -Uri $baseUrl -Method Post -Body $product2 -ContentType "application/json"
Write-Host "Created: $($response2.name) - `$$($response2.price) (ID: $($response2.id))" -ForegroundColor White

$response3 = Invoke-RestMethod -Uri $baseUrl -Method Post -Body $product3 -ContentType "application/json"
Write-Host "Created: $($response3.name) - `$$($response3.price) (ID: $($response3.id))" -ForegroundColor White

# Test 2: Get all products (TRANSIENT handler)
Write-Host "`n[2] Getting all products with TRANSIENT handler..." -ForegroundColor Magenta
$allProducts = Invoke-RestMethod -Uri $baseUrl -Method Get
Write-Host "Found $($allProducts.Count) products:" -ForegroundColor White
foreach ($p in $allProducts) {
    Write-Host "  - $($p.name): `$$($p.price) (Stock: $($p.stock))" -ForegroundColor White
}

# Test 3: Get single product (SCOPED handler)
Write-Host "`n[3] Getting single product with SCOPED handler..." -ForegroundColor Blue
$singleProduct = Invoke-RestMethod -Uri "$baseUrl/$($response1.id)" -Method Get
Write-Host "Retrieved: $($singleProduct.name) - `$$($singleProduct.price)" -ForegroundColor White

# Test 4: Update stock (SINGLETON handler)
Write-Host "`n[4] Updating product stock with SINGLETON handler..." -ForegroundColor Green
$updatedProduct = Invoke-RestMethod -Uri "$baseUrl/$($response1.id)/stock" -Method Put -Body "25" -ContentType "application/json"
Write-Host "Updated stock for $($updatedProduct.name): $($updatedProduct.stock)" -ForegroundColor White

# Test 5: Get all products again to verify update (TRANSIENT handler)
Write-Host "`n[5] Getting all products again with TRANSIENT handler..." -ForegroundColor Magenta
$allProductsUpdated = Invoke-RestMethod -Uri $baseUrl -Method Get
Write-Host "Products after update:" -ForegroundColor White
foreach ($p in $allProductsUpdated) {
    Write-Host "  - $($p.name): `$$($p.price) (Stock: $($p.stock))" -ForegroundColor White
}

# Test 6: Delete a product (SCOPED handler)
Write-Host "`n[6] Deleting product with SCOPED handler..." -ForegroundColor Blue
Invoke-RestMethod -Uri "$baseUrl/$($response2.id)" -Method Delete
Write-Host "Deleted: $($response2.name)" -ForegroundColor White

# Test 7: Get all products final (TRANSIENT handler)
Write-Host "`n[7] Final product list with TRANSIENT handler..." -ForegroundColor Magenta
$finalProducts = Invoke-RestMethod -Uri $baseUrl -Method Get
Write-Host "Remaining $($finalProducts.Count) products:" -ForegroundColor White
foreach ($p in $finalProducts) {
    Write-Host "  - $($p.name): `$$($p.price) (Stock: $($p.stock))" -ForegroundColor White
}

Write-Host "`n=== Test Complete! ===" -ForegroundColor Cyan
Write-Host "`nSummary of Handler Lifetimes Tested:" -ForegroundColor Yellow
Write-Host "  ✓ SINGLETON handlers executed (CreateProduct, UpdateStock)" -ForegroundColor Green
Write-Host "  ✓ SCOPED handlers executed (GetProduct, DeleteProduct)" -ForegroundColor Blue
Write-Host "  ✓ TRANSIENT handlers executed (GetAllProducts - called 3 times)" -ForegroundColor Magenta
