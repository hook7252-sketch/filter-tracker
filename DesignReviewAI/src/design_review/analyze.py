"""설계도서 검토 분석 파이프라인 오케스트레이션.

3단계로 진행한다.
  1) extract  : 배치 단위로 페이지 원문을 구조화 추출 (구분/발췌/핵심수치)
  2) cross    : 추출 결과 전체를 한 번에 검토하여 정합성 이슈·구조검토·
                공사비영향·기준참고·종합판정을 생성 (이슈 레지스트리)
  3) verdict  : 이슈 레지스트리를 반영해 페이지별 최종 판정을 배치로 확정
"""
from __future__ import annotations

import json
from dataclasses import asdict
from pathlib import Path

from tqdm import tqdm

from . import config
from .llm_client import LLMClient, build_page_message
from .schemas import (
    CROSS_ANALYSIS_TOOL,
    PAGE_EXTRACTION_TOOL,
    PAGE_VERDICT_TOOL,
    ConsistencyIssue,
    CostRow,
    KeyValue,
    MissingQuantityRow,
    PageContent,
    PageExtract,
    PageReview,
    ReviewAnalysis,
    StandardRef,
    StructuralRow,
)

EXTRACTION_SYSTEM = """\
당신은 국내 토목/건축 설계도서(설계설명서, 시방서, 내역서, 단가산출서, 수량산출서, \
도면, 구조계산서 등)를 검토하는 전문 감리 엔지니어입니다.
전달된 PDF 페이지(들)의 내용을 사실에 근거해 구조화하세요.
- 원문에 없는 내용을 추정하거나 창작하지 마세요.
- 수치(수량, 규격, 금액, 치수 등)는 반드시 원문 표기 그대로 key_values에 기록하세요. \
  이는 다른 페이지와의 교차검증에 사용됩니다.
- 도면/스캔 이미지 페이지는 이미지를 보고 제목, 축척, 표시된 치수·수량을 읽어내세요.
- 반드시 record_page_extractions 도구를 호출해 응답하세요.
"""

CROSS_ANALYSIS_SYSTEM = """\
당신은 국내 토목/건축 설계도서 검토를 총괄하는 책임기술자입니다.
아래는 설계도서 전체 페이지를 1차로 구조화 추출한 결과(JSON)입니다. \
이를 근거로 프로젝트 전체를 교차검증하세요.

작업 지침:
1. 서로 다른 페이지에 등장하는 동일 항목(수량, 규격, 자재, 치수, 하중, 기준 등)의 \
   불일치를 찾아 issues 목록으로 정리하세요. 각 이슈는 관련된 두 근거(ref1/ref2, \
   각각 페이지 범위와 값)를 명시하고, importance(판정 등급), 비용 연계 여부를 표시하세요.
2. 이슈 ID는 I01, I02... 순서로 부여하고, 각 이슈의 related_pages에 영향을 받는 \
   모든 페이지 번호/범위를 적으세요 (페이지별 최종 판정에 역반영됩니다).
3. 구조계산서·구조 관련 도면이 있다면 structural_rows에 시설별/부위별로 \
   '설계대상·하중·형상이 도면과 일치하는가'를 우선 판정하세요. 산술 정확성보다 \
   입력값의 정합성을 중시하세요.
4. 확정적으로 수량 차이를 계산할 수 있는 항목은 cost_rows에 current_qty/review_qty/ \
   unit_price를 숫자로 기입하세요 (금액은 프로그램이 자동 계산합니다. 직접 곱하지 마세요).
5. 원문에 없어 전량 누락된 물량은 missing_quantity_rows에 별도로 기록하세요.
6. standards에는 이슈와 관련된 국내 설계기준(KDS/KCS), 지침, 법령을 실제로 아는 \
   범위에서만 기입하고, URL을 확신할 수 없으면 빈 문자열로 두고 relevance에 \
   '검색 필요'라고 표시하세요. 근거 없는 URL을 만들어내지 마세요.
7. overall_judgement에는 전체 종합판정을, review_limitation에는 본 검토가 \
   현장조사·지반조사·원본 해석파일 등을 대체하지 않는다는 한계를 명시하세요.
8. 반드시 record_review_analysis 도구를 호출해 응답하세요.
"""

VERDICT_SYSTEM = """\
당신은 설계도서 검토 보고서를 작성하는 엔지니어입니다.
아래에 (a) 이슈 레지스트리 전체와 (b) 이번에 판정할 페이지들의 1차 추출 결과를 줍니다.
각 페이지에 대해 검토 초점, 최종 판정, 세부 검토 결과, 요구조치, 관련쪽, 이슈ID를 \
확정하세요.

판정 등급(심각도 순): 승인보류 > 재계산 > 중대보완 > 조건부확인 > 일반보완 > 확인 > 공백.
- 그 페이지가 이슈 레지스트리의 related_pages에 포함되면 해당 이슈의 importance를 \
  참고해 판정하세요 (여러 이슈에 걸리면 가장 심각한 등급을 채택).
- 이슈와 무관하고 내용이 명확하면 '확인', 표기 정정 등 경미한 사항만 있으면 '일반보완'.
- 목차/구분 페이지 등 실질 내용이 없으면 '공백'.
- detail(세부 검토 결과)은 1~2문장, required_action(요구조치)은 실행 가능한 한 문장으로.
- issue_ids는 관련 이슈ID를 세미콜론으로 연결 (없으면 빈 문자열).
반드시 record_page_verdicts 도구를 호출해 응답하세요.
"""


def _chunked(items: list, size: int) -> list[list]:
    return [items[i : i + size] for i in range(0, len(items), size)]


def extract_all_pages(
    pages: list[PageContent],
    client: LLMClient,
    batch_size: int = config.DEFAULT_BATCH_SIZE,
    project_context: str = "",
) -> list[PageExtract]:
    results: list[PageExtract] = []
    batches = _chunked(pages, batch_size)
    for batch in tqdm(batches, desc="1/3 페이지 추출"):
        content: list[dict] = []
        if project_context:
            content.append({"type": "text", "text": project_context})
        for p in batch:
            content.extend(build_page_message(p))
        content.append(
            {
                "type": "text",
                "text": f"위 {len(batch)}개 페이지({[p.page for p in batch]})를 각각 구조화하여 "
                "record_page_extractions 도구로 응답하세요.",
            }
        )
        output = client.call_tool(EXTRACTION_SYSTEM, content, PAGE_EXTRACTION_TOOL)
        for row in output.get("pages", []):
            results.append(
                PageExtract(
                    page=row["page"],
                    category=row["category"],
                    source_excerpt=row["source_excerpt"],
                    key_values=[KeyValue(**kv) for kv in row.get("key_values", [])],
                    notes=row.get("notes", ""),
                )
            )
    results.sort(key=lambda r: r.page)
    return results


def run_cross_analysis(
    extracts: list[PageExtract],
    client: LLMClient,
    project_context: str = "",
) -> ReviewAnalysis:
    compact_index = [
        {
            "page": e.page,
            "category": e.category,
            "excerpt": e.source_excerpt,
            "key_values": [asdict(kv) for kv in e.key_values],
            "notes": e.notes,
        }
        for e in extracts
    ]
    content = [
        {"type": "text", "text": project_context},
        {
            "type": "text",
            "text": "설계도서 전체 페이지 1차 추출 결과(JSON):\n" + json.dumps(compact_index, ensure_ascii=False),
        },
    ]
    output = client.call_tool(CROSS_ANALYSIS_SYSTEM, content, CROSS_ANALYSIS_TOOL, max_tokens=16000)

    return ReviewAnalysis(
        issues=[ConsistencyIssue(**i) for i in output.get("issues", [])],
        structural_rows=[StructuralRow(**r) for r in output.get("structural_rows", [])],
        cost_rows=[CostRow(**r) for r in output.get("cost_rows", [])],
        missing_quantity_rows=[MissingQuantityRow(**r) for r in output.get("missing_quantity_rows", [])],
        standards=[StandardRef(**s, checked_date="") for s in output.get("standards", [])],
        overall_judgement=output.get("overall_judgement", ""),
        review_limitation=output.get("review_limitation", ""),
        consistency_note=output.get("consistency_note", ""),
        cost_note=output.get("cost_note", ""),
    )


def finalize_page_reviews(
    extracts: list[PageExtract],
    analysis: ReviewAnalysis,
    client: LLMClient,
    batch_size: int = config.DEFAULT_BATCH_SIZE * 3,
) -> list[PageReview]:
    issue_registry = [asdict(i) for i in analysis.issues]
    results: list[PageReview] = []
    by_page = {e.page: e for e in extracts}
    batches = _chunked(extracts, batch_size)
    for batch in tqdm(batches, desc="3/3 페이지별 판정 확정"):
        content = [
            {"type": "text", "text": "이슈 레지스트리(JSON):\n" + json.dumps(issue_registry, ensure_ascii=False)},
            {
                "type": "text",
                "text": "이번에 판정할 페이지 1차 추출 결과(JSON):\n"
                + json.dumps(
                    [
                        {
                            "page": e.page,
                            "category": e.category,
                            "excerpt": e.source_excerpt,
                            "key_values": [asdict(kv) for kv in e.key_values],
                            "notes": e.notes,
                        }
                        for e in batch
                    ],
                    ensure_ascii=False,
                ),
            },
        ]
        output = client.call_tool(VERDICT_SYSTEM, content, PAGE_VERDICT_TOOL, max_tokens=8000)
        for row in output.get("pages", []):
            e = by_page.get(row["page"])
            if e is None:
                continue
            results.append(
                PageReview(
                    page=row["page"],
                    category=e.category,
                    source_excerpt=e.source_excerpt,
                    review_focus=row["review_focus"],
                    verdict=row["verdict"],
                    detail=row["detail"],
                    required_action=row["required_action"],
                    related_pages=row["related_pages"],
                    issue_ids=row.get("issue_ids", ""),
                )
            )
    results.sort(key=lambda r: r.page)
    return results


def save_json(obj, path: str | Path) -> None:
    Path(path).write_text(json.dumps(obj, ensure_ascii=False, indent=2, default=asdict), encoding="utf-8")


def load_extracts_cache(path: str | Path) -> list[PageExtract] | None:
    p = Path(path)
    if not p.exists():
        return None
    data = json.loads(p.read_text(encoding="utf-8"))
    return [
        PageExtract(
            page=d["page"],
            category=d["category"],
            source_excerpt=d["source_excerpt"],
            key_values=[KeyValue(**kv) for kv in d.get("key_values", [])],
            notes=d.get("notes", ""),
        )
        for d in data
    ]
