# ADR 0005: Spreadsheet không được xây trên DataGrid data model

- Trạng thái: Accepted
- Ngày: 2026-08-14

## Quyết định

Spreadsheet và DataGrid là hai control riêng. Chúng chỉ chia sẻ foundation, command, editor infrastructure, theme và rendering primitives.

## Lý do

Spreadsheet dùng ô/range/công thức/merge; DataGrid dùng record/schema/items source. Ép spreadsheet thành DataGrid sẽ làm sai mô hình và gây chi phí virtualization/layout khó kiểm soát.
