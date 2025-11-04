# Migration từ EF Core sang Cosmos DB SDK

## Tóm tắt những thay đổi

### 1. **Thay thế AppDbContext bằng CosmosDbContext**
   - **File mới**: `CSVProssessor.Domain/CosmosDbContext.cs`
   - **Chức năng**:
     - Khởi tạo CosmosClient
     - Tạo Database và Containers nếu chưa có
     - Cung cấp Container references cho repositories

### 2. **Thay thế GenericRepository bằng CosmosRepository**
   - **File mới**: `CSVProssessor.Infrastructure/Repositories/CosmosRepository.cs`
   - **Chức năng**:
     - Implement CRUD operations sử dụng Cosmos DB SDK
     - Hỗ trợ soft delete, tracking timestamp, audit fields
     - Sử dụng SQL query cho Cosmos DB thay vì LINQ

### 3. **Update UnitOfWork**
   - **File cập nhật**: `CSVProssessor.Infrastructure/UnitOfWork.cs`
   - **Thay đổi**:
     - Thay AppDbContext bằng CosmosDbContext
     - Repositories được lazy-loaded từ Cosmos containers
     - SaveChangesAsync() không cần ghi vào DB (Cosmos SDK tự lưu)
     - Loại bỏ transaction support (Cosmos DB không hỗ trợ transaction giống SQL)

### 4. **Dependency Injection Setup**
   - **File mới**: `CSVProssessor.FunctionApp/Startup.cs`
   - **Cấu hình**:
     - Register CosmosDbContext với connection string từ environment variable
     - Gọi InitializeAsync() khi startup để tạo Database/Containers
     - Register services và UnitOfWork

### 5. **Example Function**
   - **File mới**: `CSVProssessor.FunctionApp/Functions/CsvJobFunction.cs`
   - **Ví dụ**: CRUD operations cho CsvJob entity

---

## Cách sử dụng

### 1. Cấu hình Connection String
```json
{
  "CosmosDbConnectionString": "AccountEndpoint=https://your-account.documents.azure.com:443/;AccountKey=YOUR_KEY"
}
```

### 2. Inject IUnitOfWork vào Function/Service
```csharp
public class MyFunction
{
    private readonly IUnitOfWork _unitOfWork;

    public MyFunction(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task MyOperation()
    {
        // Create
        var csvJob = new CsvJob { FileName = "test.csv", Status = CsvJobStatus.Pending };
        await _unitOfWork.CsvJobs.AddAsync(csvJob);

        // Read
        var job = await _unitOfWork.CsvJobs.GetByIdAsync(csvJob.Id);

        // Update
        job.Status = CsvJobStatus.Processing;
        await _unitOfWork.CsvJobs.Update(job);

        // Delete (soft)
        await _unitOfWork.CsvJobs.SoftRemove(job);

        // Save changes
        await _unitOfWork.SaveChangesAsync();
    }
}
```

---

## Nhận xét quan trọng

### 1. **PartitionKey là bắt buộc**
- CsvJob: partition key = `/Status`
- CsvRecord: partition key = `/JobId`
- Khi lấy document, luôn truyền partition key value

### 2. **Schema-less**
- Cosmos DB không validate schema
- Khi thêm field mới trong model, không cần migration
- Documents cũ sẽ không có field mới (null)

### 3. **LINQ không được hỗ trợ đầy đủ**
- CosmosRepository sử dụng SQL query thay vì LINQ
- Hạn chế: Không hỗ trợ `Include()` navigation properties
- Nếu cần query phức tạp, viết SQL query trực tiếp

### 4. **SaveChangesAsync() không làm gì**
- Cosmos SDK tự lưu khi gọi CreateItemAsync, UpsertItemAsync, DeleteItemAsync
- SaveChangesAsync() được giữ lại để compatibility với interface cũ

### 5. **Không có Transaction**
- Cosmos DB không hỗ trợ transaction giống SQL Server
- Loại bỏ BeginTransactionAsync(), CommitAsync(), RollbackAsync()

---

## Packages cài đặt

```bash
# Infrastructure
dotnet add package Microsoft.Azure.Cosmos

# FunctionApp
dotnet add package Microsoft.Azure.Functions.Extensions
dotnet add package Newtonsoft.Json
```

---

## Cấu trúc Database

**Database Name**: `CSVProcessor`

**Containers**:
1. **CsvJobs**
   - Partition Key: `/Status`
   - Throughput: 400 RU/s

2. **CsvRecords**
   - Partition Key: `/JobId`
   - Throughput: 400 RU/s

---

## Troubleshooting

### 1. CosmosException: Document with ID already exists
```
→ Đảm bảo ID là unique trong mỗi partition
→ Hoặc sử dụng UpsertItemAsync() thay vì CreateItemAsync()
```

### 2. CosmosException: Resource not found
```
→ Kiểm tra partition key value có khớp không
→ Document có thể đã bị delete
```

### 3. Document có field bị null sau thêm field mới
```
→ Bình thường! Chỉ documents mới có field mới
→ Viết script để update documents cũ nếu cần
```

---

## Cải tiến trong tương lai

- [ ] Implement Index management cho performance
- [ ] Batch operations cho throughput cao
- [ ] Query builder abstraction
- [ ] Migration tracking system
