import openpyxl

from design_review_tool.parsers.quantity_parser import parse_quantity_sheet

COL_MAP = {"공종": 0, "규격": 15, "단위": 25, "수량": 30}


def _row_with_offsets(gongjong, gyugyeok, danwi, suryang):
    row = [""] * 31
    row[0] = gongjong
    row[15] = gyugyeok
    row[25] = danwi
    row[30] = suryang
    return row


def _build_qty_workbook(path, sheet_name):
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = sheet_name
    ws.append(_row_with_offsets("소제목", "", "", ""))  # 수량 없는 소제목 행 -> 제외
    ws.append(_row_with_offsets("아스팔트포장", "t=5cm", "m2", 100.0))
    wb.save(path)


def test_parse_quantity_sheet_skips_subtitle_rows_without_quantity(tmp_path):
    path = tmp_path / "2-00_포장공수량집계_test.xlsx"
    _build_qty_workbook(path, "포장공내역서적용수량")

    items = parse_quantity_sheet(str(path), "포장공내역서적용수량", COL_MAP, category="포장공")

    assert len(items) == 1
    assert items[0].공종 == "아스팔트포장"
    assert items[0].대분류 == "포장공"
    assert items[0].출처파일 == path.name


def test_parse_quantity_sheet_uses_custom_column_positions(tmp_path):
    path = tmp_path / "custom.xlsx"
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "Sheet1"
    ws.append(["아스팔트포장", "t=5cm", "m2", 100.0])
    wb.save(path)

    col_map = {"공종": 0, "규격": 1, "단위": 2, "수량": 3}
    items = parse_quantity_sheet(str(path), "Sheet1", col_map, category="포장공")

    assert len(items) == 1
    assert items[0].수량 == 100.0
