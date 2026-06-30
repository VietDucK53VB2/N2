# N2 Circulation API

Service N2 phụ trách nghiệp vụ lưu thông thư viện: mượn sách, trả sách, gia hạn, phạt, doanh thu, đánh giá và các báo cáo phục vụ màn hình độc giả/thủ thư.

## Tổng Quan

- Backend: ASP.NET Core 8 Web API.
- Database: SQL Server, cấu hình trong `backend/appsettings.json`.
- Reader UI: Vue 3 + Vuetify, build vào `backend/wwwroot/ui/reader`.
- Librarian UI: Vue 3 + Ant Design Vue, build vào `backend/wwwroot/ui/librarian`.
- Service tích hợp:
  - N1 Catalog: sách, danh mục, ảnh bìa, số lượng sách.
  - N3 Identity: đăng nhập, token, thông tin độc giả, số thẻ, dashboard identity.

## Cấu Trúc Thư Mục

```text
backend/
  Controllers/          API controllers
  Data/                 DbContext
  Models/               Entity models
  Contracts/            Request/response contracts
  wwwroot/ui/reader     Reader UI đã build
  wwwroot/ui/librarian  Librarian UI đã build
  Dockerfile            Docker image backend + static UI

frontend/reader/        Source giao diện độc giả
frontend/librarian/     Source giao diện thủ thư
docs/                   Tài liệu bổ sung
scripts/                Script hỗ trợ nếu có
```

## Chức Năng Chính

Reader:

- Xem danh sách sách, chi tiết sách, ảnh bìa.
- Thêm sách vào giỏ mượn.
- Gửi yêu cầu mượn sách.
- Theo dõi sách đang mượn, lịch sử mượn trả, ngày mượn, hạn trả, ngày trả.
- Gửi yêu cầu trả sách, gia hạn, hủy yêu cầu.
- Xem và thanh toán phí phạt.
- Đánh giá sách sau khi mượn.

Librarian:

- Dashboard tổng quan.
- Duyệt/từ chối phiếu mượn.
- Xác nhận trả sách, kiểm tra tình trạng sách.
- Quản lý gia hạn.
- Tra cứu độc giả theo tên hoặc mã thẻ.
- Quản lý giá mượn, phí phạt.
- Xem doanh thu đã ghi nhận.
- Quản lý phí phạt.
- Quản lý đánh giá sách.

## URL Trên VPS

```text
Reader UI:        http://163.223.210.87:5082/ui/reader/
Librarian UI:     http://163.223.210.87:5082/ui/librarian/
N2 API direct:    http://163.223.210.87:5082/api/circulation
API Gateway:      http://163.223.210.87:5000/api/circulation
```

Các trang embed cho nhóm khác:

```text
Đánh giá thủ thư: http://163.223.210.87:5082/ui/librarian/embed/reviews
Phiếu mượn:       http://163.223.210.87:5082/ui/librarian/embed/loans
Phí phạt:         http://163.223.210.87:5082/ui/librarian/embed/fines
Giá & phí:        http://163.223.210.87:5082/ui/librarian/embed/prices
Doanh thu:        http://163.223.210.87:5082/ui/librarian/embed/revenue
```

Ví dụ iframe:

```html
<iframe
  src="http://163.223.210.87:5082/ui/librarian/embed/reviews"
  width="100%"
  height="800"
  style="border:0;"
></iframe>
```

## Yêu Cầu Môi Trường

- .NET SDK 8.
- Node.js 18+.
- npm.
- SQL Server.
- Docker nếu build/deploy bằng container.

## Chạy Backend Local

```bash
cd backend
dotnet restore
dotnet run --project N2.Circulation.Api.csproj --urls "http://localhost:5089"
```

Swagger:

```text
http://localhost:5089/swagger
```

Ping:

```text
http://localhost:5089/ping
```

## Chạy Frontend Local

Reader UI:

```bash
cd frontend/reader
npm install
npm run dev
```

Librarian UI:

```bash
cd frontend/librarian
npm install
npm run dev
```

## Build Frontend Vào Backend

Reader:

```bash
cd frontend/reader
npm install
npm run build
```

Output:

```text
backend/wwwroot/ui/reader
```

Librarian:

```bash
cd frontend/librarian
npm install
npm run build
```

Output:

```text
backend/wwwroot/ui/librarian
```

Sau khi build, cần commit cả source frontend và static files trong `backend/wwwroot/ui/...` để VPS/GitHub/local đồng bộ.

## Build Docker Image

```bash
cd backend
docker build -t n2-circulation:local .
```

Chạy container mẫu:

```bash
docker run -d --name library_circulation_service ^
  -p 5082:80 ^
  -e ASPNETCORE_HTTP_PORTS=80 ^
  -e Jwt__Key=<jwt-key-dung-voi-identity> ^
  n2-circulation:local
```

Trên PowerShell có thể dùng dấu `` ` `` thay cho `^`.

## Endpoint Quan Trọng

Các endpoint cần gửi header:

```http
Authorization: Bearer <token>
```

Dashboard/tổng lượt mượn cho N3:

```http
GET /api/circulation/reports/dashboard
```

Field tổng lượt mượn:

```js
data.totalBorrows
```

Fallback khi dashboard lỗi:

```http
GET /api/circulation/transactions?pageSize=200
```

Rồi đếm số phần tử trả về.

Một số endpoint chính:

```text
GET    /api/circulation/transactions
GET    /api/circulation/transactions/embed
POST   /api/circulation/transactions/{id}/approve
POST   /api/circulation/transactions/{id}/reject
POST   /api/circulation/transactions/{id}/return/approve
POST   /api/circulation/transactions/{id}/return/reject
POST   /api/circulation/transactions/{id}/renew
POST   /api/circulation/transactions/{id}/renew/reject

GET    /api/circulation/fines
GET    /api/circulation/fines/embed
POST   /api/circulation/fines/{id}/pay

GET    /api/circulation/revenue
GET    /api/circulation/revenue/embed

GET    /api/circulation/books/reviews
POST   /api/circulation/books/{bookId}/reviews
DELETE /api/circulation/books/{bookId}/reviews/{reviewKey}

GET    /api/circulation/settings/prices
PUT    /api/circulation/settings/prices
GET    /api/circulation/settings/borrow-policy
```

## Tích Hợp Với N1 Và N3

N1 Catalog:

- Lấy danh sách sách.
- Lấy ảnh bìa.
- Lấy tổng số lượng sách.
- Cập nhật/truy vấn review sách nếu cần đồng bộ.

N3 Identity:

- Xác thực token.
- Lấy thông tin độc giả theo mã thẻ.
- Lấy tổng số độc giả và tổng số thẻ từ dashboard identity.

Các API gọi liên service được cấu hình trong:

```text
backend/appsettings.json
```

## Luồng Nghiệp Vụ Chính

Mượn sách:

1. Độc giả chọn sách và gửi yêu cầu mượn.
2. N2 tạo phiếu mượn trạng thái chờ duyệt.
3. Thủ thư duyệt phiếu.
4. N2 ghi nhận giao dịch đang mượn và tạo doanh thu mượn.
5. Sách xuất hiện trong lịch sử/sách của tôi của độc giả.

Trả sách:

1. Độc giả gửi yêu cầu trả.
2. Thủ thư kiểm tra tình trạng sách.
3. Nếu sách tốt, phiếu chuyển sang đã trả.
4. Nếu quá hạn/hư/mất, N2 tạo phí phạt tương ứng.

Doanh thu:

1. Doanh thu mượn được ghi khi phiếu mượn được duyệt.
2. Doanh thu phạt được ghi khi phí phạt được thanh toán/duyệt.
3. Trang doanh thu lấy dữ liệu từ backend N2.

Đánh giá:

1. Độc giả đã mượn sách có thể gửi đánh giá.
2. Thủ thư xem/xóa đánh giá ở trang đánh giá.
3. Trang đánh giá có route embed để N3 nhúng.

## Ghi Chú Deploy An Toàn

Quy trình nên dùng:

1. Sửa code trên máy.
2. Build frontend nếu có thay đổi UI.
3. Chạy kiểm tra cơ bản.
4. Commit source và build output.
5. Push GitHub.
6. Deploy lên VPS.
7. Kiểm tra HTTP/API trên VPS.

Không nên sửa trực tiếp trên VPS nếu không có backup. Nếu buộc phải hotfix static UI trên VPS, cần đồng bộ lại source/build output về GitHub ngay sau đó.

## Trạng Thái Đồng Bộ Hiện Tại

Branch chính đang dùng:

```text
codex/deploy-checklist
```

VPS hiện chạy backend image:

```text
n2-circulation:review-delete-58c594e
```

Static UI trên VPS đã đồng bộ với build output trong repo:

- Reader history dates.
- Librarian embed reviews.
- Librarian revenue không lặp mã thẻ.

Gateway đã nối được tới N2 qua Docker network; gọi qua gateway cần Bearer token.
