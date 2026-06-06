# API Contract - N2 Circulation Service

## Service Info
- **Service**: Circulation Service (Nhóm 2)
- **Base URL**: `http://192.168.29.27:5089`
- **Database**: CirculationDB (SQL Server)

---

## 1. N1 - Catalog Service

### Base URL
```
http://192.168.29.16:5185
```

### API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/books` | Lấy danh sách đầu sách |
| GET | `/api/books/{id}` | Lấy chi tiết sách theo ID |
| POST | `/api/books` | Thêm đầu sách mới |
| PUT | `/api/books/{id}` | Cập nhật thông tin sách |
| DELETE | `/api/books/{id}` | Xóa đầu sách |
| GET | `/api/books/search?q={keyword}` | Tìm kiếm sách |
| GET | `/api/books/available` | Lấy sách có thể mượn |

### Book Schema
```json
{
  "id": 1,
  "tenSach": "Dế Mèn Phiêu Lưu Ký",
  "tacGia": "Tô Hoài",
  "nhaSanXuat": "NXB Kim Đồng",
  "soLuong": 25,
  "soBanDaMuon": 3,
  "soBanConLai": 22,
  "trangThai": "Có thể mượn"
}
```

---

## 2. N2 - Circulation Service

### Base URL
```
http://192.168.29.27:5089
```

### 2.1. Đăng nhập (từ N3)

```
POST http://192.168.29.6:5208/api/Auth/login
Content-Type: application/json

{
  "username": "string",
  "password": "string"
}
```

**Response**:
```json
{
  "token": "JWT_ACCESS_TOKEN",
  "refreshToken": "JWT_REFRESH_TOKEN",
  "user": {
    "id": "string",
    "username": "string",
    "fullName": "string",
    "email": "string",
    "role": "Reader|Librarian|Admin",
    "isActive": true
  }
}
```

### 2.2. Lấy thông tin user hiện tại

```
GET http://192.168.29.6:5208/api/User/profile
Authorization: Bearer {token}
```

### 2.3. API Endpoints N2

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/circulation/transactions` | Lấy danh sách phiếu mượn |
| GET | `/api/circulation/transactions?cardNumber={cardNo}` | Lọc theo mã thẻ |
| GET | `/api/circulation/overdue` | Lấy danh sách quá hạn |
| GET | `/api/circulation/fines` | Lấy danh sách phí phạt |
| POST | `/api/circulation/borrow` | Mượn sách |
| POST | `/api/circulation/return` | Trả sách |
| GET | `/api/circulation/stats/monthly` | Thống kê tháng |
| GET | `/api/books` | Lấy sách từ N1 |

---

## 3. Database Schema - CirculationDB (N2)

### BorrowTransactions
| Column | Type | Description |
|--------|------|-------------|
| Id | uniqueidentifier | Primary key |
| BookId | nvarchar(20) | ID sách (từ N1) |
| Isbn | nvarchar(20) | ISBN sách |
| UserId | nvarchar(128) | UserId từ N3 |
| CardNumber | nvarchar(32) | Mã thẻ thư viện (từ N3 LibraryCard) |
| BorrowedAt | datetime2 | Ngày mượn |
| DueAt | datetime2 | Hạn trả |
| ReturnedAt | datetime2 | Ngày trả |
| FineAmount | decimal(18,2) | Tiền phạt |
| Status | nvarchar(20) | Pending/Borrowed/Returned/Overdue |

### FineCharges
| Column | Type | Description |
|--------|------|-------------|
| Id | uniqueidentifier | Primary key |
| BorrowTransactionId | uniqueidentifier | FK to BorrowTransaction |
| UserId | nvarchar(128) | UserId từ N3 |
| CardNumber | nvarchar(32) | Mã thẻ thư viện |
| Amount | decimal(18,2) | Số tiền |
| Reason | nvarchar(256) | Lý do |
| CreatedAt | datetime2 | Ngày tạo |
| PaidAt | datetime2 | Ngày thanh toán |

### Users (local cache)
| Column | Type | Description |
|--------|------|-------------|
| Id | uniqueidentifier | Primary key |
| Username | nvarchar(64) | Tên đăng nhập |
| PasswordHash | nvarchar(256) | Mật khẩu đã mã hóa |
| FullName | nvarchar(128) | Họ và tên |
| Role | nvarchar(32) | Quyền hạn |
| Email | nvarchar(128) | Email |
| CardNumber | nvarchar(32) | Mã thẻ |
| CreatedAt | datetime2 | Ngày tạo |
| IsActive | bit | Trạng thái |

### PublishedEventLogs
| Column | Type | Description |
|--------|------|-------------|
| Id | uniqueidentifier | Primary key |
| SourceService | nvarchar(64) | Nguồn phát event |
| EventType | nvarchar(64) | Loại event |
| PayloadJson | nvarchar(max) | Dữ liệu JSON |
| PublishedAt | datetime2 | Thời gian phát |
| IsActive | bit | Trạng thái |

---

## 4. Frontend Pages

### Reader Page
- URL: `/ui/reader/`
- Auth: Lấy token từ N3 login URL param `?token=xxx&cardNumber=xxx`
- Role: Reader

### Librarian Page
- URL: `/ui/librarian/`
- Auth: Check localStorage token
- Role: Librarian
- Tabs: Overview Dashboard, Xử lý Phiếu mượn/trả, Tra cứu Tình trạng, Xác nhận Trả sách

### Admin Page
- URL: `/ui/admin/`
- Auth: Check localStorage token
- Role: Admin

---

## 5. Configuration

```json
{
  "CatalogServiceUrl": "http://192.168.29.16:5185",
  "IdentityServiceUrl": "http://192.168.29.6:5208",
  "ConnectionStrings": {
    "CirculationDb": "Server=localhost\\SQLEXPRESS;Database=N2.Circulation.Api;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```
