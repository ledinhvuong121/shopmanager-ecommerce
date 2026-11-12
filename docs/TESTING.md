# Kết quả kiểm thử ShopManager

Ngày thực hiện: 06/11/2026. Môi trường: Windows, .NET SDK 10.0.403, ASP.NET Core/EF Core 10.0.14, SQL Server cục bộ. Đây là kiểm tra bản chạy local, không phải chứng nhận sẵn sàng vận hành production.

## 1. Biên dịch

`dotnet build ShopManager.slnx --no-restore -m:1 /p:UseSharedCompilation=false`

Kết quả: thành công, **0 lỗi, 0 cảnh báo**. Sau khi NuGet cảnh báo về phụ thuộc System.Security.Cryptography.Pkcs cũ, dự án đã ghim trực tiếp bản 10.0.14; lần restore/build sau đó không còn cảnh báo đó.

## 2. Nghiệp vụ trên SQL Server thật

`Test.ps1` hoặc `dotnet test tests/ShopManager.Tests`

**13/13 ca đạt, không bỏ qua.** Database `ShopManager_Tests_<guid>` được tạo và dọn riêng cho bộ test.

- Tạo đơn cập nhật tồn, tổng tiền, tiền thừa, nhật ký; đơn giá cũ không đổi sau khi sửa giá sản phẩm.
- Số lượng 0, âm và vượt tồn bị chặn (3 ca tham số).
- Dòng thứ hai sai không tạo đơn dở hoặc làm thay đổi tồn trong database.
- Tiền thiếu và giá đã thay đổi bị từ chối.
- Gửi lại cùng mã yêu cầu trả về đơn cũ, không trừ tồn lần hai.
- Hủy đơn hoàn tồn một lần và giữ lịch sử.
- Gửi lại phiếu nhập không cộng tồn lần hai.
- Giỏ trống, trùng sản phẩm và sản phẩm ngừng bán bị chặn.
- Hai người cùng bán hàng cuối: chỉ một giao dịch thành công, tồn không âm.
- Hai lần hủy đồng thời: chỉ một giao dịch thành công, không hoàn hai lần.
- Báo cáo dùng biên ngày Việt Nam, loại đơn hủy.

Kiểm thử báo cáo từng phát hiện truy vấn LINQ không dịch được sang SQL. Đã sửa thứ tự sắp xếp/projection và chạy lại toàn bộ bộ test thành công.

Chi tiết máy đọc được: `work/TestResults/business-tests.trx`.

## 3. Kiểm tra HTTP đầu cuối

Sau khi build, chạy:

```powershell
python tests/http_smoke.py
# Nếu SQL Server dùng instance khác:
$env:SHOPMANAGER_HTTP_SQL_SERVER = '.\SQLEXPRESS'
python tests/http_smoke.py
```

Cần Python 3.10+, `sqlcmd`, Windows Authentication và quyền tạo/xóa database kiểm thử. Không cần gói Python bên ngoài.

**65 bước/assertion đạt.** Số này gồm cả việc tải trang chuẩn bị và kiểm tra kết quả nghiệp vụ, không phải 65 test độc lập. Script tạo database `ShopManager_HttpTests_<guid>`, tài khoản quản lý/nhân viên ngẫu nhiên trong bộ nhớ và server riêng trên cổng trống; dừng server và xóa đúng database tạm sau khi chạy. Không thay đổi dữ liệu ShopManager dùng để demo.

Đã kiểm tra:

- Chưa đăng nhập được chuyển về trang đăng nhập.
- Các màn hình tổng quan, danh sách/form sản phẩm, danh mục, khách hàng, kho, đơn, nhân viên và đổi mật khẩu trả về trang hợp lệ.
- Tìm kiếm, trạng thái đơn và trang vượt phạm vi không gây lỗi server.
- POST thiếu anti-forgery token nhận 400.
- Tạo danh mục, sản phẩm, khách hàng; SKU trùng bị chặn; nhập kho cộng đúng tồn.
- Quản lý tạo tài khoản nhân viên; nhân viên đăng nhập được.
- Staff bị chặn tại trang quản lý, kho, danh mục, sửa sản phẩm; không đọc đơn của quản lý, không hủy đơn kể cả đơn của mình.
- Quản lý/nhân viên tạo đơn, chuyển tới hóa đơn, tính tổng và trừ tồn đúng.
- Quản lý hủy đơn, hủy lặp không hoàn hai lần; bán vượt tồn trả về form với lỗi dễ hiểu.
- Khóa tài khoản vô hiệu cookie đang đăng nhập ngay ở request tiếp theo.
- Đăng xuất vô hiệu phiên đăng nhập.

Chi tiết: `work/TestResults/http-results.json`; log server thử: `work/TestResults/http-server.log`.

## 4. Kiểm tra bằng trình duyệt

- Đăng nhập quản lý, xem dashboard và trạng thái chưa có doanh thu.
- Thêm cà phê 18.000đ và bánh 22.000đ: giỏ 40.000đ; khách đưa 50.000đ, tiền thừa 10.000đ.
- Tăng cà phê lên 2: giỏ 58.000đ; nút “Khách đưa vừa đủ” điền 58.000đ, tiền thừa 0.
- Tìm “ca phe” trả về sản phẩm “Cà phê”.
- Xóa giỏ làm nút xác nhận vô hiệu.
- Thử viewport 390 × 844: nội dung không tràn ngang (scrollWidth 375 <= viewport 390); giỏ xếp bên dưới danh sách.
- Không có lỗi JavaScript được ghi nhận trong các thao tác đã thử.
- Chỉ thao tác giỏ tại trình duyệt, không phát sinh đơn thật trong database demo.

Ảnh minh họa: `docs/screenshots/dashboard.png`, `docs/screenshots/pos.png`.

## 5. Giới hạn xác minh

Chưa thử in ra máy in vật lý, HTTPS/IIS production, nhiều trình duyệt/điện thoại thực hoặc kiểm thử tải. Không kiểm thử mọi tổ hợp dữ liệu có thể nhập. Chức năng đổi mật khẩu đã build và trang form được tải; chưa có ca tự động cho toàn bộ vòng đổi mật khẩu. Các giới hạn sản phẩm và lưu ý triển khai được ghi trong README.

## 6. Kiểm tra launcher — 11/11/2026

Sau phản hồi về việc ứng dụng dừng ở chế độ chạy trong console, Start.ps1 được đổi sang chạy nền, thêm Stop.ps1 và giữ tùy chọn -Foreground. Không xác định được nguồn phát lệnh shutdown từ log cũ nên không quy kết nguyên nhân.

Đã kiểm tra bằng Windows PowerShell 5.1:

- Build thành công, 0 lỗi/cảnh báo.
- HTTP trang đăng nhập trả 200 sau khi script/PowerShell khởi tạo đã kết thúc.
- Chạy Start lần hai giữ nguyên PID, không khởi động trùng.
- Stop chỉ dừng tiến trình đã ghi nhận; Start sau đó khởi động lại thành công.
- Tất cả 5 file .ps1 qua kiểm tra cú pháp PowerShell 5.1.

Đây là thay đổi script vận hành; mã nghiệp vụ và database không thay đổi.

## 7. Đăng nhập bằng tên tài khoản — 11/11/2026

Form đăng nhập nhận tên tài khoản hoặc email. Đã kiểm tra tài khoản admin hiện có đăng nhập thành công qua cả hai cách bằng mật khẩu người dùng chọn; giữ nguyên ID, quyền và dữ liệu nghiệp vụ. Build 0 lỗi/cảnh báo; chạy lại 65 bước kiểm tra HTTP đạt. Bản lưu thông tin đăng nhập local được cập nhật bằng Windows DPAPI; mật khẩu không được ghi vào mã nguồn.

