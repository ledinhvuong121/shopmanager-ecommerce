"""HTTP acceptance checks. Creates and drops only a unique ShopManager_HttpTests_* database.
Requires: Python 3.10+, dotnet, sqlcmd, SQL Server Windows Authentication.
Run from project root after dotnet build: python tests/http_smoke.py
"""
import html
import http.cookiejar
import json
import os
from pathlib import Path
import re
import secrets
import socket
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from html.parser import HTMLParser

ROOT = Path(__file__).resolve().parents[1]
WEB = ROOT / 'src' / 'ShopManager.Web'
DLL = WEB / 'bin' / 'Debug' / 'net10.0' / 'ShopManager.Web.dll'
DB = 'ShopManager_HttpTests_' + uuid.uuid4().hex
SERVER = os.environ.get('SHOPMANAGER_HTTP_SQL_SERVER', '.')
EMAIL = 'manager@http-test.local'
PASSWORD = 'Aa1!' + secrets.token_urlsafe(24)
checks = []


def check(condition, label):
    if not condition:
        raise AssertionError(label)
    checks.append(label)
    print('PASS ' + label, flush=True)


class Fields(HTMLParser):
    def __init__(self, body):
        super().__init__()
        self.values = {}
        self.feed(body)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == 'input' and 'name' in attrs:
            self.values[attrs['name']] = attrs.get('value', '')


class Client:
    def __init__(self, base):
        self.base = base
        self.opener = urllib.request.build_opener(
            urllib.request.ProxyHandler({}),
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(self, path, data=None):
        url = self.base + path if path.startswith('/') else path
        payload = None if data is None else urllib.parse.urlencode(data).encode()
        try:
            response = self.opener.open(url, payload, timeout=20)
        except urllib.error.HTTPError as response_error:
            response = response_error
        with response:
            body = response.read().decode('utf-8-sig')
            return response.status, response.url, body

    def get(self, path):
        status, url, body = self.request(path)
        check(status == 200 and 'Chưa thể hoàn tất yêu cầu' not in html.unescape(body), 'GET ' + path)
        return body

    def post(self, path, data, form_page=None):
        body = self.get(form_page or path)
        token = Fields(body).values['__RequestVerificationToken']
        return self.request(path, {**data, '__RequestVerificationToken': token})

    def login(self, email, password):
        status, url, body = self.post('/Account/Login', {'Identifier': email, 'Password': password})
        check(status == 200 and '/Account/Login' not in url, 'Login ' + email)


def catalog(client):
    body = client.get('/Orders/Create')
    match = re.search(r'<script id="catalog-data"[^>]*>(.*?)</script>', body, re.S)
    return json.loads(match.group(1)), Fields(body).values['RequestId']


def sale(client, product, quantity):
    body = client.get('/Orders/Create')
    fields = Fields(body).values
    return client.request('/Orders/Create', {
        '__RequestVerificationToken': fields['__RequestVerificationToken'],
        'RequestId': fields['RequestId'],
        'CashReceived': 1000000,
        'Lines[0].ProductId': product['id'],
        'Lines[0].Quantity': quantity,
        'Lines[0].ExpectedPrice': product['price']
    })


def run(base):
    manager = Client(base)
    status, url, _ = manager.request('/Products')
    check(status == 200 and '/Account/Login' in url, 'Anonymous user is redirected to login')
    manager.login(EMAIL, PASSWORD)
    for path in ['/', '/Products', '/Products/Edit', '/Categories', '/Categories/Edit',
                 '/Customers', '/Customers/Edit', '/Inventory', '/Inventory/Receive',
                 '/Orders', '/Orders/Create', '/Staff', '/Staff/Create', '/Account/ChangePassword',
                 '/Products?page=999&search=DR', '/Orders?status=Cancelled']:
        manager.get(path)
    status, _, _ = manager.request('/Categories/Edit', {'Name': 'No CSRF'})
    check(status == 400, 'POST without anti-forgery token is rejected')

    status, _, body = manager.post('/Customers/Edit', {'Name': 'Khách HTTP', 'Phone': '0900000099', 'Address': 'Dữ liệu kiểm thử'})
    check(status == 200 and 'Khách HTTP' in html.unescape(body), 'Customer create')
    status, _, body = manager.post('/Categories/Edit', {'Name': 'Danh mục HTTP'})
    check(status == 200 and 'Danh mục HTTP' in html.unescape(body), 'Category create')
    product_form = manager.get('/Products/Edit')
    category_id = re.search(r'<option value="(\d+)">Danh m&#x1EE5;c HTTP</option>', product_form)
    if category_id is None:
        category_id = re.search(r'<option value="(\d+)">Danh mục HTTP</option>', html.unescape(product_form))
    check(category_id is not None, 'New category is selectable')
    product_data = {'Sku': 'HTTP001', 'Name': 'Sản phẩm HTTP', 'CategoryId': category_id.group(1), 'Price': 12000, 'LowStockThreshold': 3, 'IsActive': 'true'}
    status, _, body = manager.post('/Products/Edit', product_data)
    check(status == 200 and 'Sản phẩm HTTP' in html.unescape(body), 'Product create')
    status, _, body = manager.post('/Products/Edit', product_data)
    check(status == 200 and 'Mã sản phẩm đã tồn tại' in html.unescape(body), 'Duplicate SKU validation')
    product_list = manager.get('/Products?search=HTTP001')
    product_id = re.search(r'href="/Products/Edit/(\d+)"', product_list).group(1)
    status, _, body = manager.post('/Inventory/Receive', {
        'RequestId': str(uuid.uuid4()), 'ProductId': product_id, 'Quantity': 10, 'Note': 'Nhập HTTP'
    })
    check(status == 200 and 'Nhập HTTP' in html.unescape(body), 'Inventory receive')
    products, _ = catalog(manager)
    product = next(p for p in products if p['sku'] == 'HTTP001')
    check(product['stock'] == 10, 'Receipt sets stock to 10')

    staff_email, staff_password = 'staff@http-test.local', 'Aa1!' + secrets.token_urlsafe(24)
    status, _, body = manager.post('/Staff/Create', {'FullName': 'Nhân viên HTTP', 'Email': staff_email, 'Password': staff_password})
    check(status == 200 and staff_email in body, 'Manager creates Staff account')
    staff = Client(base)
    staff.login(staff_email, staff_password)
    for path in ['/Staff', '/Inventory', '/Categories', '/Products/Edit']:
        status, _, _ = staff.request(path)
        check(status == 403, 'Staff cannot access ' + path)
    status, admin_order_url, _ = sale(manager, product, 2)
    check(status == 200 and '/Orders/Details/' in admin_order_url, 'Manager sale saves and redirects to invoice')
    admin_order_id = admin_order_url.rsplit('/', 1)[-1]
    status, _, _ = staff.request('/Orders/Details/' + admin_order_id)
    check(status == 403, 'Staff cannot read another user order')
    status, staff_order_url, body = sale(staff, product, 3)
    check(status == 200 and '/Orders/Details/' in staff_order_url and '36.000' in body, 'Staff sale stores correct total')
    staff_order_id = staff_order_url.rsplit('/', 1)[-1]
    products, _ = catalog(manager)
    check(next(p for p in products if p['sku'] == 'HTTP001')['stock'] == 5, 'Two sales deduct stock to 5')
    status, _, _ = staff.post('/Orders/Cancel/' + staff_order_id, {'reason': 'Forbidden'}, '/Orders/Details/' + staff_order_id)
    check(status == 403, 'Staff cannot cancel even their own order')
    status, _, body = manager.post('/Orders/Cancel/' + staff_order_id, {'reason': 'Hủy kiểm thử HTTP'}, '/Orders/Details/' + staff_order_id)
    check(status == 200 and 'Đã hủy' in html.unescape(body), 'Manager can cancel order')
    manager.post('/Orders/Cancel/' + staff_order_id, {'reason': 'Lặp'}, '/Orders/Details/' + staff_order_id)
    products, _ = catalog(manager)
    check(next(p for p in products if p['sku'] == 'HTTP001')['stock'] == 8, 'Repeated cancellation only restocks once')
    status, url, body = sale(staff, product, 99)
    check(status == 200 and '/Orders/Create' in url and 'chỉ còn 8' in html.unescape(body), 'Overselling validation returns usable form')
    manager.get('/')
    staff_listing = manager.get('/Staff')
    staff_id = re.search(r'name="id" value="([^"]+)"', staff_listing).group(1)
    manager.post('/Staff/SetActive', {'id': staff_id, 'active': 'false'}, '/Staff')
    status, url, _ = staff.request('/Orders')
    check(status == 200 and '/Account/Login' in url, 'Locking Staff invalidates existing cookie')
    manager.post('/Account/Logout', {}, '/')
    status, url, _ = manager.request('/Products')
    check(status == 200 and '/Account/Login' in url, 'Logout invalidates session')


def main():
    assert re.fullmatch(r'ShopManager_HttpTests_[a-f0-9]{32}', DB)
    if not DLL.exists():
        raise RuntimeError('Build ShopManager.slnx before running HTTP checks.')
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    env = os.environ.copy()
    env['ConnectionStrings__DefaultConnection'] = f'Server={SERVER};Database={DB};Trusted_Connection=True;TrustServerCertificate=True'
    env['Seed__AdminEmail'], env['Seed__AdminPassword'] = EMAIL, PASSWORD
    env['ASPNETCORE_ENVIRONMENT'] = 'Development'
    results = ROOT / 'work' / 'TestResults'
    results.mkdir(parents=True, exist_ok=True)
    process = None
    success = False
    try:
        subprocess.run(['dotnet', str(DLL), '--initialize', '--sample-data'], cwd=WEB, env=env, check=True, timeout=60, stdout=subprocess.DEVNULL)
        env.pop('Seed__AdminPassword')
        with (results / 'http-server.log').open('w', encoding='utf-8') as log:
            process = subprocess.Popen(['dotnet', str(DLL), '--urls', base], cwd=WEB, env=env, stdout=log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
            ready = False
            for _ in range(50):
                try:
                    with urllib.request.build_opener(urllib.request.ProxyHandler({})).open(base + '/Account/Login', timeout=2) as response:
                        ready = response.status == 200
                    if ready:
                        break
                except (urllib.error.URLError, ConnectionError):
                    time.sleep(0.2)
            if not ready:
                raise RuntimeError('HTTP server did not start; inspect work/TestResults/http-server.log')
            run(base)
            success = True
    finally:
        if process:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
        # The database identifier is generated above, validated, and never read from user input.
        subprocess.run(['sqlcmd', '-S', SERVER, '-E', '-C', '-b', '-Q',
                        f"IF DB_ID(N'{DB}') IS NOT NULL BEGIN ALTER DATABASE [{DB}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DB}]; END"],
                       check=True, timeout=30, stdout=subprocess.DEVNULL)
        (results / 'http-results.json').write_text(json.dumps({'passed': success, 'checks': checks}, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'HTTP acceptance: {len(checks)} checks passed. Temporary database removed.')


if __name__ == '__main__':
    main()
