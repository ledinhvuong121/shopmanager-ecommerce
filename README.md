# ShopManager

Ứng dụng quản lý bán hàng cho một cửa hàng nhỏ. Dự án cá nhân fullstack C#; giao diện tiếng Việt, responsive. Hỗ trợ bán tiền mặt tại quầy và đặt hàng online thanh toán khi nhận hàng (COD), chưa có chatbot AI, cổng thanh toán, đa chi nhánh hoặc báo cáo lợi nhuận.

## Công nghệ

C# / .NET 10, ASP.NET Core MVC, Razor, Bootstrap 5 (lưu local), JavaScript thuần, EF Core 10, SQL Server, ASP.NET Core Identity, xUnit. Phiên bản NuGet được cố định trong csproj. Không cần Node/npm để chạy.

## Bắt đầu trên máy này

Mở PowerShell ở `V:\Real_Project`:

```powershell
.\Start.ps1
```

Truy cập http://localhost:5179. Database mặc định: `ShopManager` trên SQL Server `.` bằng Windows Authentication. Tài khoản quản lý cục bộ là `admin` (vẫn có thể dùng email `admin@shopmanager.local`). Mật khẩu do người dùng chọn được bảo vệ bằng Windows DPAPI tại `work/local-access.clixml` (bị loại khỏi Git, chỉ tài khoản Windows đã tạo mới giải mã được). Xem thông tin đăng nhập bằng `./Show-LocalAccess.ps1`; đổi mật khẩu qua menu sau khi đăng nhập. Bản mã chỉ dùng cho lần bàn giao ban đầu, không tự cập nhật sau khi đổi mật khẩu.

## Cập nhật database hiện có cho đơn hàng online

Dừng ứng dụng bằng `./Stop.ps1`, chạy `./Initialize.ps1 -MigrateOnly` để áp dụng migration, rồi chạy `./Start.ps1`. Chế độ này giữ nguyên tài khoản và mật khẩu hiện có.

Khách đăng ký ở trang đăng nhập, chọn sản phẩm, nhập địa chỉ giao và đặt hàng COD. Đơn chờ giao giữ số lượng đã đặt trong kho. Quản lý xác nhận giao hàng và thu tiền hoặc hủy đơn để hoàn tồn. Doanh thu chỉ ghi nhận khi đơn hoàn tất.

## Ảnh sản phẩm và mua hàng online

Danh mục hiển thị số lượng đã bán từ các đơn hoàn tất và điểm đánh giá trung bình. Mỗi khách có thể đánh giá một lần cho mỗi sản phẩm đã nhận. Giỏ hàng hỗ trợ voucher mẫu `SHOP10` giảm 10%, tối đa 30.000 ₫.

Ảnh được lưu theo SKU trong `wwwroot/images/products`. Nguồn ảnh mẫu:

- [DR001](https://cdn1.concung.com/2022/05/57542-88864/ca-phe-sua-da-lon-highlands-lon-235ml.jpg)
- [DR002](https://product.hstatic.net/1000288770/product/tra_o_long_tea_plus_chai_455ml_c8c37f369efd441eb74dd044cde70e06_master.jpg)
- [DR003](https://cdn.tgdd.vn/Products/Images/2563/76401/nuoc-khoang-la-vie-500ml-1-600x600.jpg)
- [FD001](https://bizweb.dktcdn.net/100/514/431/products/mi-hao-hao-tom-chua-cay.jpg?v=1716271249223)
- [FD002](https://foodbazaar.co.uk/cdn/shop/files/OREO-VANILLA-133G-1_1.jpg?v=1716052681)
- [FD003](https://img.tgdd.vn/imgt/ecom/f_webp%2Cfit_outside%2Cquality_95/https%3A/cdn.tgdd.vn/Products/Images/2386/79312/sttt-vinamilk-dan-bo-cd-1l-2-11.jpg)
- [PC001](https://www.lottemart.vn/media/catalog/product/cache/0x0/8/9/8934839127732-1-1.jpg.webp)
- [PC002](https://storage.beautyfulls.com/uploads-1/avatar/product/1200x/2023/08/11/figure-1691727789860.png)
- [HH001](https://bizweb.dktcdn.net/thumb/1024x1024/100/478/273/products/208819.jpg?v=1730360226403)
- [HH002](https://product.hstatic.net/200000077081/product/pu-facial_tissue_pulppy_180_b16f86876e5b4369b10fcc853a059e7a_1024x1024.jpg)

## Chatbot miễn phí chạy trên máy

Cài [Ollama cho Windows](https://docs.ollama.com/windows) và chạy `ollama pull qwen3:8b`. Ollama chạy tại cổng 11434. Có thể đặt biến `Ollama__Model` thành tên mô hình đã tải trước khi chạy ShopManager, ví dụ `qwen2.5:1.5b` cho máy ít RAM.

Khách dùng **Tư vấn AI** để hỏi về sản phẩm và giá. Quản lý dùng **Trợ lý AI** để đọc doanh thu 7 ngày, đơn chờ giao và hàng sắp hết. Chatbot chạy cục bộ, không cần API key, chỉ đọc dữ liệu và không nhận thông tin định danh khách hàng từ đơn hàng.

## Thanh toán thử qua MoMo sandbox

MoMo sandbox chỉ dùng giao dịch thử. Lấy thông tin merchant test từ [MoMo Developers](https://developers.momo.vn/v3/docs/payment/api/wallet/onetime/) và đặt `Momo__PartnerCode`, `Momo__AccessKey`, `Momo__SecretKey`, `Momo__PublicBaseUrl` trong môi trường chạy ứng dụng. URL gốc phải là HTTPS công khai tới ứng dụng, để MoMo gọi `/Wallet/Notify`. Cấu hình triển khai cần cho phép hostname đó trong `AllowedHosts`. Giữ khóa sandbox ngoài Git.

Áp dụng migration bằng `./Initialize.ps1 -MigrateOnly` trước khi chạy phiên bản này. Chọn ví MoMo khi đặt đơn, rồi mở trang thanh toán thử từ chi tiết đơn. ShopManager giữ tồn khi đặt đơn, chỉ chuyển sang chờ giao sau khi nhận thông báo thành công có chữ ký hợp lệ. Các mã thất bại cuối được hỗ trợ sẽ hủy đơn và hoàn tồn. Khi trạng thái chưa rõ, quản lý đối soát trong sandbox trước khi xử lý. Tích hợp chưa có hoàn tiền hoặc đối soát tự động và chỉ trỏ tới máy chủ thử của MoMo.

## Cài đặt trên máy khác

1. Cài .NET SDK 10 và SQL Server (Developer/Express hoặc LocalDB). Visual Studio phải hỗ trợ .NET 10; cũng có thể dùng VS Code.
2. Chọn instance qua biến môi trường, không cần sửa mã:

```powershell
# Ví dụ LocalDB; bỏ dòng này nếu sử dụng SQL Server mặc định .
$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\MSSQLLocalDB;Database=ShopManager;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False'
.\Initialize.ps1 -SampleData
.\Start.ps1
```

`Initialize.ps1` hỏi mật khẩu bằng ô nhập ẩn, tạo schema bằng migration, tạo vai trò và quản lý đầu tiên. `-SampleData` thêm 10 sản phẩm, 4 danh mục, 2 khách hàng và các phiếu nhập mẫu, không tạo doanh thu giả. Chạy lại không đổi mật khẩu tài khoản đã có. Các dữ liệu mẫu chỉ được thêm khi chưa có sản phẩm. Chỉ định database mới/dành riêng cho ứng dụng.

Nếu kết nối báo lỗi, kiểm tra tên instance trong SQL Server Management Studio (ví dụ `.\SQLEXPRESS`) và quyền tài khoản Windows. Không tự động sửa cấu hình SQL Server hay tài khoản hệ thống.

## Chạy trong IDE

Mở `ShopManager.slnx`, đặt `ShopManager.Web` làm startup project, chạy profile `http`. Lần restore đầu cần Internet. Các script đặt cache NuGet trong `work/packages`; nếu dùng IDE, NuGet có thể tải vào cache mặc định của người dùng.

## Phạm vi chức năng

- Identity: đăng nhập/đăng xuất, đổi mật khẩu, khóa đăng nhập sai 5 lần trong 5 phút; Manager/Staff/Customer.
- Danh mục, sản phẩm: tạo/sửa, SKU duy nhất, ngừng bán, tìm kiếm/phân trang.
- Khách hàng: tạo/sửa, tìm tên/số điện thoại, phân trang; đơn lưu snapshot tên/điện thoại.
- Nhập kho và sổ kho có người thực hiện, thời gian, nội dung.
- Quầy bán hàng: tìm không dấu, giỏ hàng, thanh toán tiền mặt, tính tiền thừa, in phiếu bán hàng.
- Đơn hàng: lọc/tìm/phân trang, Staff chỉ thấy đơn của mình; Manager hủy và hoàn tồn một lần.
- Dashboard: doanh thu theo khoảng ngày Việt Nam, đơn hoàn tất, top sản phẩm, hàng tồn thấp.
- Quản lý nhân viên: tạo tài khoản Staff, khóa/mở tài khoản; cookie của tài khoản bị khóa bị từ chối ở request kế tiếp.

## Bảo toàn dữ liệu

`OrderService` lấy giá từ SQL Server và so sánh giá nhân viên đã xem. Giá thay đổi thì từ chối để xác nhận lại. Tạo đơn + trừ tồn + nhật ký kho là một transaction; hủy đơn cũng vậy. SQL Server `rowversion` trên Product/Order chống ghi đè đồng thời. Mã yêu cầu unique bảo vệ thao tác gửi lặp; xung đột đồng thời trả lỗi để người dùng kiểm tra lại, không tự retry gây nhân đôi. SKU và giá tại thời điểm bán được lưu ở OrderItem.

Tồn ban đầu là 0 khi thêm sản phẩm bằng giao diện; nhập hàng để tăng tồn. Không cho sửa trực tiếp tồn hoặc xóa lịch sử giao dịch. Tiền dùng decimal và số nguyên VND. Có anti-forgery token cho POST, kiểm tra quyền server, ViewModel chống over-posting, đăng ký công khai chỉ tạo tài khoản Customer.

## Cấu trúc

```text
docs/SPECIFICATION.md           Đặc tả, ERD, quy tắc, nghiệm thu
docs/TESTING.md                 Kết quả và quy trình kiểm thử
src/ShopManager.Web/
  Controllers/                 HTTP, validation, phân quyền
  Models/Domain.cs             Entities và vai trò
  ViewModels/Forms.cs          Form và dữ liệu màn hình
  Services/OrderService.cs     Bán/hủy/nhập kho
  Services/ReportService.cs    Báo cáo theo giờ Việt Nam
  Data/                       DbContext, seed, EF migrations
  Views/                      Giao diện Razor
  wwwroot/                    CSS, JS, Bootstrap local
tests/ShopManager.Tests/        Kiểm thử SQL Server/xUnit
```

## Kiểm thử và migrations

```powershell
.\Test.ps1
# Nếu SQL Server kiểm thử dùng instance khác:
$env:SHOPMANAGER_TEST_SQL = 'Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True'
.\Test.ps1

dotnet tool restore
dotnet ef migrations add TenThayDoi --project src/ShopManager.Web
dotnet ef database update --project src/ShopManager.Web
```

Tests tạo database độc lập `ShopManager_Tests_<guid>` rồi xóa đúng database đó sau khi kết thúc, không xóa dữ liệu ShopManager. Cần tài khoản SQL có quyền tạo/xóa database test. Chạy trên SQL Server thật vì EF InMemory không kiểm tra transaction/rowversion chính xác. Nếu test bị kill đột ngột, database test có thể còn lại; kiểm tra tên và xóa thủ công nếu cần.

## Demo mua hàng online cho CV

1. Mở trang chủ trong cửa sổ riêng tư, lọc theo danh mục và thêm sản phẩm vào giỏ.
2. Tải lại trang, đăng ký rồi đăng nhập. Giỏ vẫn giữ sản phẩm trong cùng tab.
3. Nhập địa chỉ giao hàng, áp dụng `SHOP10` và đặt đơn COD.
4. Dùng tài khoản quản lý để mở đơn chờ giao và xác nhận đã giao hàng, thu tiền.
5. Quay lại tài khoản khách, mở chi tiết đơn hoàn tất và gửi đánh giá. Kiểm tra lượt đã bán và điểm sao trong danh mục.

Gợi ý mô tả CV: **ShopManager — dự án thương mại điện tử cá nhân.** Xây dựng danh mục công khai, giỏ hàng, voucher, đặt hàng COD và đánh giá sau mua bằng ASP.NET Core MVC, EF Core và SQL Server. Dùng transaction, rowversion và mã yêu cầu để xử lý đồng thời và tránh ghi trùng đơn.

## Demo và giải thích khi phỏng vấn

1. Đăng nhập quản lý; xem tồn và dashboard.
2. Tạo sản phẩm, nhập 10; tạo một nhân viên.
3. Đăng nhập nhân viên, bán 3; in hóa đơn và xác nhận tồn 7.
4. Thử bán quá tồn và truy cập trang nhân viên bằng tài khoản Staff.
5. Quản lý đổi giá; mở hóa đơn cũ để chứng minh snapshot giá.
6. Hủy đơn: tồn về 10, doanh thu loại đơn đó; không thể hủy lần hai.
7. Giải thích transaction, rowversion, mã request và kiểm thử cạnh tranh hàng cuối.

## Giới hạn và triển khai

Ứng dụng hiện chạy cục bộ, chưa triển khai public. Danh sách chọn tại quầy bán hàng tải toàn bộ sản phẩm còn hàng/khách hàng, phù hợp cửa hàng nhỏ; khi dữ liệu lớn cần tìm kiếm phân trang từ server. Chưa có trả một phần, giảm giá, VAT, nhà cung cấp/giá vốn, xuất Excel, email khôi phục mật khẩu, MFA, sao lưu tự động hoặc hóa đơn điện tử pháp lý. Tên/điện thoại khách và giá sản phẩm được snapshot, tên nhân viên lấy từ tài khoản hiện tại. Sửa thông tin danh mục/khách hàng áp dụng lần ghi cuối.

Trước khi đưa vào cửa hàng thật: cấu hình HTTPS với chứng chỉ hợp lệ, `AllowedHosts`, connection string qua secret, tài khoản DB tối thiểu quyền, nơi lưu data protection keys và sao lưu; không dùng database thử hay mật khẩu demo. `TrustServerCertificate=True` dành cho SQL Server phát triển cục bộ, production cần xác thực chứng chỉ phù hợp. Khởi tạo/migrate là bước triển khai tường minh, ứng dụng không tự migrate lúc chạy web.

## Tham khảo chính thức

- https://learn.microsoft.com/aspnet/core/mvc/overview?view=aspnetcore-10.0
- https://learn.microsoft.com/aspnet/core/security/authentication/identity?view=aspnetcore-10.0
- https://learn.microsoft.com/ef/core/saving/concurrency
- https://learn.microsoft.com/ef/core/saving/transactions

Khi ghi CV, mô tả đây là dự án cá nhân và chỉ ghi nhận phần bạn đã tự tìm hiểu, kiểm chứng và có thể giải thích.

## Kết quả bàn giao

- Build: 0 lỗi, 0 cảnh báo.
- 13/13 ca xUnit trên SQL Server và 65 bước kiểm tra HTTP đạt.
- Chi tiết và giới hạn: docs/TESTING.md.
- Hướng dẫn đọc mã nguồn, câu hỏi phỏng vấn và mẫu CV: docs/LEARNING.md.
- Ảnh giao diện: docs/screenshots/.
- Có thể chạy kiểm tra HTTP độc lập sau khi build bằng: python tests/http_smoke.py.


## Khởi động trên Windows PowerShell 5.1 (cập nhật 11/11/2026)

`Start.ps1` mặc định biên dịch rồi chạy ShopManager ở nền, chỉ báo `DA SAN SANG` sau khi trang đăng nhập phản hồi HTTP 200. Sau đó có thể đóng PowerShell. Chạy lại lệnh khi ứng dụng đang chạy không mở thêm tiến trình. Script không tự dừng một chương trình khác đang chiếm cổng 5179.

```powershell
cd V:\Real_Project
.\Start.ps1
# Dừng máy chủ chạy nền:
.\Stop.ps1
# Sau khi sửa code: Stop rồi Start để biên dịch bản mới.
# Nếu muốn xem log trực tiếp và dừng bằng Ctrl+C:
.\Start.ps1 -Foreground
```

Chỉ dùng `-Foreground` sau khi đã dừng bản chạy nền. Log bản nền ở `work/runtime/stdout.log` và `stderr.log`; thông tin nhận dạng tiến trình ở `server.json`. Stop kiểm tra cả đường dẫn chương trình và thời gian tạo tiến trình để tránh dừng nhầm PID đã được tái sử dụng. Đây là tiến trình local, không phải Windows Service và không tự khởi động cùng Windows.

Các script được lưu UTF-8 có BOM để Windows PowerShell 5.1 đọc đúng. Nếu đóng cửa sổ khi dùng `-Foreground`, ứng dụng sẽ dừng; chế độ nền mặc định không phụ thuộc cửa sổ đó.
