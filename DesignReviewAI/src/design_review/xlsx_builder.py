"""분석 결과를 업로드된 템플릿과 동일한 6개 시트 구조의 엑셀로 저장한다.

시트 구성: 종합요약 · 페이지별검토 · 정합성검증 · 공사비영향 · 구조검토 · 기준참고
"""
from __future__ import annotations

from pathlib import Path

from openpyxl import Workbook
from openpyxl.formatting.rule import CellIsRule
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter
from openpyxl.worksheet.worksheet import Worksheet

from . import config
from .schemas import PageReview, ProjectMeta, ReviewAnalysis

FONT_NAME = "Arial"
TITLE_FONT = Font(name=FONT_NAME, size=14, bold=True)
SUBTITLE_FONT = Font(name=FONT_NAME, size=9, italic=True, color="FF555555")
HEADER_FONT = Font(name=FONT_NAME, size=10, bold=True, color="FFFFFFFF")
HEADER_FILL = PatternFill("solid", fgColor="FF2F5597")
BODY_FONT = Font(name=FONT_NAME, size=10)
BODY_ALIGN = Alignment(vertical="top", wrap_text=True)
THIN_BORDER = Border(*(Side(style="thin", color="FFD9D9D9") for _ in range(4)))
LARGE_ROW_RANGE = 5000  # 조건부서식/COUNTIF에 사용할 넉넉한 범위 상한


def _write_title(ws: Worksheet, row: int, text: str, ncols: int) -> None:
    ws.cell(row=row, column=1, value=text)
    ws.merge_cells(start_row=row, start_column=1, end_row=row, end_column=ncols)
    ws.cell(row=row, column=1).font = TITLE_FONT


def _write_subtitle(ws: Worksheet, row: int, text: str, ncols: int) -> None:
    if not text:
        return
    ws.cell(row=row, column=1, value=text)
    ws.merge_cells(start_row=row, start_column=1, end_row=row, end_column=ncols)
    c = ws.cell(row=row, column=1)
    c.font = SUBTITLE_FONT
    c.alignment = Alignment(wrap_text=True)


def _write_header_row(ws: Worksheet, row: int, headers: list[str]) -> None:
    for col, h in enumerate(headers, start=1):
        c = ws.cell(row=row, column=col, value=h)
        c.font = HEADER_FONT
        c.fill = HEADER_FILL
        c.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
    ws.freeze_panes = ws.cell(row=row + 1, column=1).coordinate


def _write_row(ws: Worksheet, row: int, values: list) -> None:
    for col, v in enumerate(values, start=1):
        c = ws.cell(row=row, column=col, value=v)
        c.font = BODY_FONT
        c.alignment = BODY_ALIGN
        c.border = THIN_BORDER


def _set_widths(ws: Worksheet, widths: dict[int, int]) -> None:
    for col, w in widths.items():
        ws.column_dimensions[get_column_letter(col)].width = w


def _verdict_conditional_formatting(ws: Worksheet, col_letter: str, first_row: int, last_row: int) -> None:
    rng = f"{col_letter}{first_row}:{col_letter}{last_row}"
    for verdict, color in config.VERDICT_FILL_COLORS.items():
        ws.conditional_formatting.add(
            rng,
            CellIsRule(operator="equal", formula=[f'"{verdict}"'], fill=PatternFill("solid", fgColor=color)),
        )


# --------------------------------------------------------------------------
# 페이지별검토
# --------------------------------------------------------------------------

def _build_page_review_sheet(wb: Workbook, meta: ProjectMeta, reviews: list[PageReview]) -> None:
    ws = wb.create_sheet("페이지별검토")
    headers = ["PDF쪽", "구분", "원문 식별", "검토 초점", "판정", "세부 검토 결과", "요구조치", "관련쪽", "이슈ID"]
    _write_title(ws, 1, f"{meta.project_name} 설계도서 — 전페이지 검토", len(headers))
    _write_subtitle(
        ws, 2,
        f"원본: {meta.source_filename} | 검토일: {meta.review_date} | "
        "페이지별 원문 식별·수량·도면·계산서 상호대조",
        len(headers),
    )
    _write_subtitle(
        ws, 3,
        "판정 의미: 승인보류=보완 전 발주/시공 승인 곤란 · 재계산=선행 입력 오류로 결과 교체 필요 · "
        "중대보완=안전/수량/계약상 핵심 보완 · 조건부확인=형식 검토 완료, 선행사항 해결 후 확정 · "
        "일반보완=문서 품질 보완 · 확인=특이사항 없음 · 공백=구분 페이지",
        len(headers),
    )
    header_row = 5
    _write_header_row(ws, header_row, headers)
    row = header_row + 1
    for r in reviews:
        _write_row(
            ws, row,
            [r.page, r.category, r.source_excerpt, r.review_focus, r.verdict, r.detail,
             r.required_action, r.related_pages, r.issue_ids],
        )
        row += 1
    last_data_row = max(row - 1, header_row + 1)
    _verdict_conditional_formatting(ws, "E", header_row + 1, last_data_row)
    _set_widths(ws, {1: 7, 2: 12, 3: 42, 4: 18, 5: 12, 6: 42, 7: 36, 8: 12, 9: 10})


# --------------------------------------------------------------------------
# 정합성검증
# --------------------------------------------------------------------------

def _build_consistency_sheet(wb: Workbook, analysis: ReviewAnalysis) -> None:
    ws = wb.create_sheet("정합성검증")
    headers = ["ID", "중요도", "항목", "기준 1", "기준 2", "차이·문제", "영향/판정", "필수 조치", "비용 연계"]
    _write_title(ws, 1, "핵심 정합성 검증", len(headers))
    _write_subtitle(
        ws, 2,
        analysis.consistency_note or "도면 ↔ 수량산출 ↔ 내역 ↔ 구조계산의 동일 항목을 대조한 결과입니다.",
        len(headers),
    )
    header_row = 4
    _write_header_row(ws, header_row, headers)
    row = header_row + 1
    for i in analysis.issues:
        ref1 = f"{i.ref1_pages}: {i.ref1_value}" if i.ref1_value else i.ref1_pages
        ref2 = f"{i.ref2_pages}: {i.ref2_value}" if i.ref2_value else i.ref2_pages
        _write_row(
            ws, row,
            [i.id, i.importance, i.item, ref1, ref2, i.difference, i.impact_verdict,
             i.required_action, i.cost_linkage],
        )
        row += 1
    last_data_row = max(row - 1, header_row + 1)
    _verdict_conditional_formatting(ws, "B", header_row + 1, last_data_row)
    _set_widths(ws, {1: 6, 2: 12, 3: 26, 4: 22, 5: 22, 6: 34, 7: 26, 8: 30, 9: 10})


# --------------------------------------------------------------------------
# 공사비영향
# --------------------------------------------------------------------------

def _build_cost_sheet(wb: Workbook, analysis: ReviewAnalysis) -> None:
    ws = wb.create_sheet("공사비영향")
    headers = ["항목", "현재 수량", "검토 수량", "수량차", "단위", "단가(원)",
               "직접공사비 영향(원)", "확실성", "확정합계 반영", "근거/비고"]
    ncols = len(headers)
    _write_title(ws, 1, "공사비 영향 검토", ncols)
    _write_subtitle(
        ws, 2,
        analysis.cost_note or
        "부호 기준: (+) 현재 내역 대비 증액 필요, (−) 현재 내역 대비 감액 가능. "
        "간접비·이윤·부가세는 미반영한 직접공사비 기준입니다.",
        ncols,
    )

    ws.cell(row=4, column=1, value="확정 항목 순증감(원)").font = Font(name=FONT_NAME, bold=True)
    total_cell = ws.cell(row=4, column=2)
    total_cell.font = Font(name=FONT_NAME, bold=True)

    header_row = 8
    _write_header_row(ws, header_row, headers)
    row = header_row + 1
    first_data_row = row
    for c in analysis.cost_rows:
        include_flag = "Y" if c.include_in_total else "N"
        ws.cell(row=row, column=1, value=c.item)
        ws.cell(row=row, column=2, value=c.current_qty)
        ws.cell(row=row, column=3, value=c.review_qty)
        ws.cell(row=row, column=4, value=f"=C{row}-B{row}")
        ws.cell(row=row, column=5, value=c.unit)
        ws.cell(row=row, column=6, value=c.unit_price)
        ws.cell(row=row, column=7, value=f"=D{row}*F{row}")
        ws.cell(row=row, column=8, value=c.certainty)
        ws.cell(row=row, column=9, value=include_flag)
        ws.cell(row=row, column=10, value=c.basis)
        for col in range(1, ncols + 1):
            cell = ws.cell(row=row, column=col)
            cell.font = BODY_FONT
            cell.alignment = BODY_ALIGN
            cell.border = THIN_BORDER
            if col in (2, 3, 4, 6, 7):
                cell.number_format = "#,##0.###"
        row += 1
    last_data_row = max(row - 1, first_data_row)
    total_cell.value = f'=SUMIFS(G{first_data_row}:G{last_data_row},I{first_data_row}:I{last_data_row},"Y")'
    total_cell.number_format = "#,##0"

    # 미가격 누락 물량 섹션
    if analysis.missing_quantity_rows:
        sect_row = last_data_row + 3
        _write_title(ws, sect_row, "미가격 누락 물량", ncols)
        per_unit_label = analysis.missing_quantity_rows[0].per_unit_label or "단위당 수량"
        total_label = analysis.missing_quantity_rows[0].total_label or "총 수량"
        mq_headers = ["항목", per_unit_label, total_label, "단위", "근거"]
        _write_header_row(ws, sect_row + 1, mq_headers)
        r = sect_row + 2
        for m in analysis.missing_quantity_rows:
            _write_row(ws, r, [m.item, m.per_unit_value, m.total_value, m.unit, m.basis])
            r += 1
        note_row = r + 1
        ws.cell(
            row=note_row, column=1,
            value="주의: 위 수량은 도면 표기값을 단순 환산한 최소 식별량입니다. 현장 여건을 반영한 "
            "최종 수량산출서가 필요하며, 시나리오형 항목은 서로 대안이므로 합산하면 안 됩니다.",
        )
        ws.merge_cells(start_row=note_row, start_column=1, end_row=note_row, end_column=ncols)
        ws.cell(row=note_row, column=1).font = SUBTITLE_FONT
        ws.cell(row=note_row, column=1).alignment = Alignment(wrap_text=True)

    _set_widths(ws, {1: 26, 2: 12, 3: 12, 4: 10, 5: 8, 6: 12, 7: 16, 8: 10, 9: 10, 10: 30})


# --------------------------------------------------------------------------
# 구조검토
# --------------------------------------------------------------------------

def _build_structural_sheet(wb: Workbook, analysis: ReviewAnalysis) -> None:
    ws = wb.create_sheet("구조검토")
    headers = ["시설", "검토부위", "도면/수량", "계산서", "검토결과", "영향", "요구자료·조치", "판정", "의견 여부"]
    _write_title(ws, 1, "구조검토 핵심 쟁점", len(headers))
    _write_subtitle(
        ws, 2,
        "구조계산의 산술 정확성보다 '설계대상·하중·형상·재료·지반 입력이 도면과 일치하는가'를 "
        "우선 판정했습니다.",
        len(headers),
    )
    header_row = 4
    _write_header_row(ws, header_row, headers)
    row = header_row + 1
    for s in analysis.structural_rows:
        _write_row(
            ws, row,
            [s.facility, s.part, s.drawing_ref, s.calc_ref, s.review_result, s.impact,
             s.required_action, s.verdict, "ㅇ" if s.has_opinion else ""],
        )
        row += 1
    last_data_row = max(row - 1, header_row + 1)
    _verdict_conditional_formatting(ws, "H", header_row + 1, last_data_row)
    _set_widths(ws, {1: 12, 2: 14, 3: 14, 4: 14, 5: 30, 6: 22, 7: 30, 8: 12, 9: 10})


# --------------------------------------------------------------------------
# 기준참고
# --------------------------------------------------------------------------

def _build_standards_sheet(wb: Workbook, meta: ProjectMeta, analysis: ReviewAnalysis) -> None:
    ws = wb.create_sheet("기준참고")
    headers = ["분야", "기준/자료", "본 검토와의 관련성", "URL", "확인일"]
    _write_title(ws, 1, "검토 기준·참고자료", len(headers))
    _write_subtitle(
        ws, 2,
        "아래 항목은 AI가 제안한 참고 경로입니다. URL을 확신할 수 없는 항목은 비워두었으니, "
        "최종 설계자는 적용판·개정일·프로젝트 조건을 반드시 다시 확인해야 합니다.",
        len(headers),
    )
    header_row = 4
    _write_header_row(ws, header_row, headers)
    row = header_row + 1
    for s in analysis.standards:
        url = s.url or "(확인 필요)"
        _write_row(ws, row, [s.field, s.reference, s.relevance, url, meta.review_date])
        row += 1
    _set_widths(ws, {1: 14, 2: 34, 3: 40, 4: 40, 5: 12})


# --------------------------------------------------------------------------
# 종합요약
# --------------------------------------------------------------------------

def _build_summary_sheet(
    wb: Workbook, meta: ProjectMeta, reviews: list[PageReview], analysis: ReviewAnalysis
) -> None:
    ws = wb.create_sheet("종합요약", 0)
    ncols = 10  # A~J

    _write_title(ws, 1, f"{meta.project_name} 설계도서 전페이지 검토 — 종합요약", ncols)

    ws.cell(row=3, column=1, value="원본").font = Font(name=FONT_NAME, bold=True)
    ws.cell(row=3, column=2, value=meta.source_filename)
    ws.cell(row=4, column=1, value="검토범위").font = Font(name=FONT_NAME, bold=True)
    ws.cell(row=4, column=2, value=meta.review_scope)
    ws.cell(row=5, column=1, value="검토일").font = Font(name=FONT_NAME, bold=True)
    ws.cell(row=5, column=2, value=meta.review_date)
    for r in (3, 4, 5):
        ws.merge_cells(start_row=r, start_column=2, end_row=r, end_column=ncols)
        ws.cell(row=r, column=2).alignment = Alignment(wrap_text=True)

    ws.cell(row=7, column=1, value=analysis.overall_judgement)
    ws.merge_cells(start_row=7, start_column=1, end_row=9, end_column=ncols)
    c = ws.cell(row=7, column=1)
    c.font = Font(name=FONT_NAME, bold=True, size=11)
    c.alignment = Alignment(wrap_text=True, vertical="top")
    c.fill = PatternFill("solid", fgColor="FFFCE5CD")

    # 페이지 판정 현황 (좌측) + 핵심 승인조건 (우측)
    ws.cell(row=11, column=1, value="페이지 판정 현황").font = Font(name=FONT_NAME, bold=True, size=12)
    ws.cell(row=11, column=4, value="핵심 승인조건").font = Font(name=FONT_NAME, bold=True, size=12)

    status_headers = ["판정", "페이지 수"]
    _write_header_row(ws, 12, status_headers)
    issue_headers = ["순번", "구분", "핵심 쟁점", "관련쪽", "판정", "필수 산출물", "비고"]
    for col, h in enumerate(issue_headers, start=4):
        cell = ws.cell(row=12, column=col, value=h)
        cell.font = HEADER_FONT
        cell.fill = HEADER_FILL
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
    ws.freeze_panes = "A13"

    status_row = 13
    for level in config.VERDICT_LEVELS:
        ws.cell(row=status_row, column=1, value=level)
        ws.cell(
            row=status_row, column=2,
            value=f'=COUNTIF(페이지별검토!$E$6:$E${6 + LARGE_ROW_RANGE},"{level}")',
        )
        status_row += 1
    ws.cell(row=status_row, column=1, value="합계").font = Font(name=FONT_NAME, bold=True)
    ws.cell(row=status_row, column=2, value=f"=SUM(B13:B{status_row - 1})").font = Font(name=FONT_NAME, bold=True)
    for r in range(13, status_row + 1):
        ws.cell(row=r, column=1).font = BODY_FONT
        ws.cell(row=r, column=2).font = BODY_FONT
    _verdict_conditional_formatting(ws, "A", 13, status_row - 1)

    priority = config.VERDICT_PRIORITY
    top_issues = sorted(analysis.issues, key=lambda i: priority.get(i.importance, 99))[:10]
    irow = 13
    for idx, issue in enumerate(top_issues, start=1):
        related = issue.related_pages or f"{issue.ref1_pages}, {issue.ref2_pages}"
        values = [idx, issue.category or "-", issue.item, related,
                  issue.importance, issue.required_action, issue.difference]
        for offset, v in enumerate(values):
            cell = ws.cell(row=irow, column=4 + offset, value=v)
            cell.font = BODY_FONT
            cell.alignment = BODY_ALIGN
            cell.border = THIN_BORDER
        irow += 1
    _verdict_conditional_formatting(ws, "H", 13, max(irow - 1, 13))

    # 공사비·수량 핵심
    cost_title_row = status_row + 2
    ws.cell(row=cost_title_row, column=1, value="공사비·수량 핵심").font = Font(name=FONT_NAME, bold=True, size=12)
    hdr_row = cost_title_row + 1
    _write_header_row(ws, hdr_row, ["항목", "현재", "검토값", "직접공사비 영향"])

    r = hdr_row + 1
    ws.cell(row=r, column=1, value="확정 항목 순증감")
    ws.cell(row=r, column=4, value="=공사비영향!B4")
    ws.cell(row=r, column=4).number_format = "#,##0"
    for col in (1, 4):
        ws.cell(row=r, column=col).font = BODY_FONT
    r += 1

    for m in analysis.missing_quantity_rows:
        total_txt = f"{m.total_value} {m.unit}" if m.total_value is not None else m.basis
        _write_row(ws, r, [m.item, "내역 없음", total_txt, "미가격"])
        r += 1

    for c in analysis.cost_rows:
        if c.certainty == "확정":
            continue  # 확정 3항목은 위 합계로 이미 반영
        current_txt = f"{c.current_qty} {c.unit}"
        review_txt = f"{c.review_qty} {c.unit}"
        impact_txt = f"검토 필요 ({c.certainty})"
        _write_row(ws, r, [c.item, current_txt, review_txt, impact_txt])
        r += 1

    limitation_row = r + 2
    ws.cell(
        row=limitation_row, column=1,
        value="검토 한계: " + (
            analysis.review_limitation
            or "본 결과는 제출된 설계도서 내부의 완결성·정합성·주요 구조입력·수량/비용을 검토한 것입니다. "
            "현장조사·지반조사·원본 구조해석 파일·제조사 성능시험 자료가 제공되지 않았으므로 독립 상세설계 "
            "또는 책임기술자의 최종 구조확인을 대체하지 않습니다."
        ),
    )
    ws.merge_cells(start_row=limitation_row, start_column=1, end_row=limitation_row + 2, end_column=ncols)
    lc = ws.cell(row=limitation_row, column=1)
    lc.font = Font(name=FONT_NAME, italic=True, size=9)
    lc.alignment = Alignment(wrap_text=True, vertical="top")

    _set_widths(ws, {1: 16, 2: 12, 3: 18, 4: 30, 5: 12, 6: 12, 7: 12, 8: 30, 9: 12, 10: 16})


def build_workbook(
    output_path: str | Path,
    meta: ProjectMeta,
    reviews: list[PageReview],
    analysis: ReviewAnalysis,
) -> Path:
    wb = Workbook()
    wb.remove(wb.active)  # 기본 빈 시트 제거

    _build_page_review_sheet(wb, meta, reviews)
    _build_consistency_sheet(wb, analysis)
    _build_cost_sheet(wb, analysis)
    _build_structural_sheet(wb, analysis)
    _build_standards_sheet(wb, meta, analysis)
    _build_summary_sheet(wb, meta, reviews, analysis)  # index=0 로 맨 앞에 삽입

    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    wb.save(output_path)
    return output_path
