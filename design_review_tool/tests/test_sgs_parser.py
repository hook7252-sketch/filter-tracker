import openpyxl

from design_review_tool.parsers.sgs_parser import parse_sgs


def _build_sgs_workbook(path):
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "내역서"
    rows = [
        ["1. 포 장 공", "", "", ""],
        ["아스팔트포장", "t=5cm", 100.0, "m2"],
        ["미끄럼방지포장", "칼라", 50.0, "m2"],
        ["2. 부 대 공", "", "", ""],
        ["집수정", "700x700", 3.0, "개소"],
        ["소제목만있는행", "", "", ""],  # 숫자 접두사 없어 대분류행도 아니고, 수량 없어 데이터행도 아님
    ]
    for row in rows:
        ws.append(row)
    wb.save(path)


def test_parse_sgs_assigns_category_and_skips_non_data_rows(tmp_path):
    path = tmp_path / "sgs.xlsx"
    _build_sgs_workbook(path)

    items = parse_sgs(str(path))

    assert len(items) == 3
    assert items[0].공종 == "아스팔트포장"
    assert items[0].대분류 == items[1].대분류  # 포장공 카테고리 공유
    assert items[2].공종 == "집수정"
    assert items[2].대분류 != items[0].대분류  # 부대공은 다른 카테고리


def test_parse_sgs_combined_text_merges_name_and_spec(tmp_path):
    path = tmp_path / "sgs.xlsx"
    _build_sgs_workbook(path)

    items = parse_sgs(str(path))

    assert items[0].결합텍스트 == "아스팔트포장t=5cm"
