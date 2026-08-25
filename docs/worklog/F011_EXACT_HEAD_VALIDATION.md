# F011 Exact-Head Validation

Commit này xác nhận nguồn F011 đã được áp dụng trên nhánh và chủ động kích hoạt ma trận CI đầy đủ bằng thông tin xác thực người dùng, thay vì dựa vào push nội bộ từ `GITHUB_TOKEN`.

F011 gồm đúng 10 hàm mới:

`LOOKUP`, `OFFSET`, `ROW`, `ROWS`, `SHEET`, `SHEETS`, `SORTBY`, `TAKE`, `TOCOL`, `TOROW`.

Mốc chỉ được khóa khi CI của chính HEAD này xanh.
