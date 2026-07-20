import openpyxl

from design_review_tool.features.quantity_vs_sgs.report import save_report, to_dataframe
from design_review_tool.matching.engine import MatchGrade, match_items
from design_review_tool.parsers.quantity_parser import parse_quantity_sheet
from design_review_tool.parsers.sgs_parser import parse_sgs


def _build_sgs(path):
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "내역서"
    for row in [
        ["1. 포 장 공", "", "", ""],
        ["아스팔트포장", "t=5cm", 100.0, "m2"],
        ["미끄럼방지포장", "칼라", 50.0, "m2"],
        ["자재비", "", 1.0, "식"],  # 대분류 밖(관급자재 등 정상 범위 밖) 취급될 잔여 항목
    ]:
        ws.append(row)
    wb.save(path)


def _build_qty(path):
    row_asphalt = [""] * 31
    row_asphalt[0], row_asphalt[15], row_asphalt[25], row_asphalt[30] = "아스팔트포장", "t=5cm", "m2", 100.0

    row_extra = [""] * 31
    row_extra[0], row_extra[15], row_extra[25], row_extra[30] = "수량산출서에만있는항목", "", "m2", 30.0

    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "포장공내역서적용수량"
    ws.append(row_asphalt)
    ws.append(row_extra)
    wb.save(path)


def test_end_to_end_pipeline_grades_match_expectations(tmp_path):
    sgs_path = tmp_path / "sgs.xlsx"
    qty_path = tmp_path / "2-00_포장공수량집계.xlsx"
    _build_sgs(sgs_path)
    _build_qty(qty_path)

    sgs_items = parse_sgs(str(sgs_path))
    qty_items = parse_quantity_sheet(
        str(qty_path), "포장공내역서적용수량",
        col_map={"공종": 0, "규격": 15, "단위": 25, "수량": 30},
        category="포장공",
    )

    results = match_items(qty_items, sgs_items)
    grades = {r.source.공종: r.grade for r in results if r.source}

    assert grades["아스팔트포장"] == MatchGrade.CONFIRMED
    assert grades["수량산출서에만있는항목"] == MatchGrade.UNMATCHED_SOURCE_ONLY

    unmatched_targets = [r.target.공종 for r in results if r.grade == MatchGrade.UNMATCHED_TARGET_ONLY]
    assert "미끄럼방지포장" in unmatched_targets
    assert "자재비" in unmatched_targets

    df = to_dataframe(results)
    assert len(df) == len(results)

    out_path = tmp_path / "out.xlsx"
    save_report(results, str(out_path))
    assert out_path.exists()

    csv_path = tmp_path / "out.csv"
    save_report(results, str(csv_path))
    assert csv_path.exists()
