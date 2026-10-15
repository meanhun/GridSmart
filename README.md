# Đồng Nai GridSmart

Hệ thống tiếp nhận và điều phối phản ánh sự cố đô thị theo ô lưới cho Thành phố Đồng Nai.

Người dân chụp ảnh sự cố (rác thải, lấn chiếm vỉa hè, hư hỏng hạ tầng, xả thải) và gửi lên. Hệ thống tự xác định ô lưới chứa vị trí, phân loại sự cố, giao cho cán bộ phụ trách ô lưới đó và đếm ngược thời hạn xử lý (SLA). Quá hạn mà chưa ai nhận thì báo lên lãnh đạo. Cán bộ xử lý xong phải chụp ảnh "sau" tại đúng hiện trường. Người dân có thể tra cứu tiến độ.

## Ảnh chụp màn hình

### Đăng nhập

![Màn hình đăng nhập](Program%20images/Screenshot%201.png)

### Người dân tra cứu tiến độ phản ánh

![Tra cứu tiến độ phản ánh](Program%20images/Screenshot%202.png)

### Bản đồ điều hành

![Bản đồ điều hành](Program%20images/Screenshot.png)

### Danh sách sự cố

![Danh sách sự cố](Program%20images/Screenshot%203.png)

### Hàng chờ duyệt

![Hàng chờ duyệt](Program%20images/Screenshot%204.png)

### Quản lý đơn vị hành chính

![Quản lý đơn vị hành chính](Program%20images/Screenshot%205.png)

### Vai trò và quyền

![Vai trò và quyền](Program%20images/Screenshot%206.png)

## Công nghệ

- **Backend:** ASP.NET Core (.NET 10), SQL Server, SignalR (cập nhật realtime)
- **Frontend:** React + Vite, bản đồ Leaflet / OpenStreetMap

## Chạy thử

Yêu cầu: SQL Server 2017+, .NET SDK 10, Node.js 20.19+ hoặc 22.12+.

```powershell
# 1. Tạo database và nạp dữ liệu test (chỉ chạy 1 lần)
sqlcmd -S "lpc:." -E -b -I -f 65001 -i dev_migration_full.sql

# 2. Backend (terminal 1)
dotnet run --project backend/GridSmart.Api --launch-profile http
#    API: http://localhost:5282, tài liệu API: http://localhost:5282/scalar

# 3. Frontend (terminal 2)
cd frontend
npm install
npm run dev
#    Mở http://localhost:5173
```

Hướng dẫn kiểm thử chi tiết từng luồng nghiệp vụ: [HUONG_DAN_KIEM_THU.md](HUONG_DAN_KIEM_THU.md).
