# Hướng dẫn đọc mã nguồn và trình bày dự án

## Đọc theo thứ tự

1. `docs/SPECIFICATION.md`: phạm vi, vai trò, nghiệp vụ và ERD.
2. `Models/Domain.cs`: Product, SaleOrder, OrderItem, StockMovement và Identity.
3. `Data/AppDbContext.cs`: quan hệ, unique index, rowversion và ràng buộc.
4. `ViewModels/Forms.cs`: form nhập và validation; vì sao không bind trực tiếp Entity.
5. `Controllers/OrdersController.cs` → `Services/OrderService.cs`: luồng HTTP tới transaction bán/hủy đơn.
6. `Views/Orders/Create.cshtml` và `wwwroot/js/pos.js`: giỏ hàng trình duyệt, dữ liệu gửi server.
7. `Services/ReportService.cs`: truy vấn báo cáo và biên ngày UTC+7.
8. `tests/ShopManager.Tests/SalesTests.cs`: kiểm chứng hành vi và cạnh tranh tồn kho.

Các đường dẫn từ bước 2 đến 7 nằm dưới `src/ShopManager.Web`.

## Những câu hỏi cần tự trả lời

- Vì sao dùng decimal cho tiền? Vì sao không tin tổng tiền do JavaScript gửi lên?
- Transaction bảo vệ những thao tác nào trong tạo đơn? Nếu một thao tác lỗi thì sao?
- Hai nhân viên bán món hàng cuối cùng được xử lý như thế nào? RowVersion có vai trò gì?
- Vì sao OrderItem lưu UnitPrice và tên/mã sản phẩm riêng?
- Làm sao chống người dùng nhấn xác nhận hai lần?
- Vì sao Staff không xem được đơn của người khác dù đoán được URL?
- Vì sao cần ViewModel, anti-forgery token và kiểm tra quyền tại controller?
- Vì sao test SQL Server thật quan trọng hơn EF InMemory cho transaction/rowversion?
- Khi hủy đơn cũ, báo cáo kỳ cũ thay đổi ra sao? Vì sao đây chưa phải hệ thống kế toán?

## Gợi ý nội dung CV sau khi đã tìm hiểu và kiểm chứng

**ShopManager — Dự án cá nhân | C#, ASP.NET Core MVC, EF Core, SQL Server**

- Phát triển ứng dụng quản lý bán hàng gồm phân quyền quản lý/nhân viên, sản phẩm, khách hàng, tồn kho, đơn hàng và báo cáo.
- Xử lý bán/hủy đơn bằng transaction; kiểm soát cập nhật đồng thời với SQL Server rowversion; lưu lịch sử giá và biến động kho.
- Xây dựng giao diện Razor/Bootstrap và giỏ hàng JavaScript; kiểm thử nghiệp vụ bằng xUnit và kiểm tra HTTP các luồng chính.

Không ghi “đã triển khai cho cửa hàng thực tế” khi chưa có triển khai đó. Khi phỏng vấn, mô tả trung thực phần được công cụ hỗ trợ và phần bạn tự nghiên cứu, sửa đổi, kiểm thử. Nên tự thực hiện một cải tiến nhỏ, ghi commit và giải thích lý do thiết kế để thể hiện mức độ hiểu dự án.
